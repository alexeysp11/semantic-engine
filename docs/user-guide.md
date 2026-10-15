# User Guide

This guide explains how to install, start and use `semantic-engine`, and how to inspect and troubleshoot it. For the internals, see the [Architecture Reference](architecture.md).

**Contents**

1. [What the system does](#1-what-the-system-does)
2. [Prerequisites](#2-prerequisites)
3. [Installation](#3-installation)
4. [Starting the system](#4-starting-the-system)
5. [Using the terminal client](#5-using-the-terminal-client)
6. [Working with sessions and workspaces](#6-working-with-sessions-and-workspaces)
7. [Using the HTTP API directly](#7-using-the-http-api-directly)
8. [Configuration reference](#8-configuration-reference)
9. [Inspecting what the agent did](#9-inspecting-what-the-agent-did)
10. [Running the tests](#10-running-the-tests)
11. [Troubleshooting](#11-troubleshooting)
12. [Current limitations](#12-current-limitations)

---

## 1. What the system does

`semantic-engine` is an autonomous coding agent that runs on your machine with a local LLM (`qwen2.5-coder:7b` through Ollama). You describe a task or paste code in the terminal client. The agent then works in a loop:

1. It thinks and answers, or asks to use a **tool**.
2. A separate worker runs the tool (read or write a file, call an HTTP API, run `dotnet build` or `dotnet test`) and returns the result.
3. The agent reads the result and continues, until it gives you a final answer.

Typical use: paste a C# class that does not compile. The agent writes it to its workspace, builds it, reads the compiler errors, fixes the code, builds again and finally shows you the corrected class.

Everything runs locally; no code leaves your machine except HTTP requests that the agent itself is asked to make.

---

## 2. Prerequisites

| Requirement | Notes |
|---|---|
| .NET SDK 10 | Needed to build and run every service, and by the agent itself for `dotnet build`. |
| Docker with Docker Compose | Runs RabbitMQ, PostgreSQL with `pgvector`, ClickHouse and Metabase. |
| Ollama | Runs the chat and embedding models. Must listen on `http://localhost:11434`. |
| Hardware | The 7B model needs roughly 5 GB of memory. A GPU is optional; on a CPU, responses are slow. The first request after starting Ollama also loads the model into memory and can take several minutes. |
| Terminal | A terminal that supports ANSI colors and reports **Ctrl+Enter** (Windows Terminal and conhost do). |

---

## 3. Installation

Run the following commands from the repository root.

### 3.1 Start the infrastructure

```bash
docker compose up -d
```

This starts:

| Service | Address | Credentials |
|---|---|---|
| RabbitMQ | `localhost:5672`; management UI `http://localhost:15672` | `guest` / `guest` |
| PostgreSQL | `localhost:5432`, database `semantic_hub` | `admin` / `secret_password` |
| ClickHouse | HTTP `http://localhost:8123`, native `localhost:9000` | `admin` / `secret_password` |
| Metabase | `http://localhost:3000` | created on first start |

The ClickHouse table `semantic_engine.thought_logs` is created automatically from `infra/clickhouse/init/01-thought-logs.sql` on the first start. The credentials above are development placeholders; change them in `docker-compose.yml` and in the matching `appsettings.json` keys before using the stack outside a local machine.

### 3.2 Pull the models

```bash
ollama pull qwen2.5-coder:7b
ollama pull all-minilm:l6-v2
```

`qwen2.5-coder:7b` is the reasoning model (about 4.7 GB). `all-minilm:l6-v2` is the embedding model (about 90 MB of memory).

### 3.3 Build

```bash
dotnet build Semantic.Engine.slnx
```

---

## 4. Starting the system

### Start everything with one command

On Windows, the script `scripts/start-all.ps1` starts the infrastructure, pulls missing models, builds the solution, opens one console window per backend service and then starts the terminal client in the current window:

```powershell
./scripts/start-all.ps1
```

Parameters: `-SkipInfrastructure` (Docker is already running), `-SkipModels` (do not check Ollama) and `-NoClient` (start the backend only). Close the service windows to stop the services.

### Start the services manually

Start each service in its own terminal, from the repository root. The services can start in any order; RabbitMQ delivers messages when a consumer comes up.

```bash
dotnet run --project src/Services/Semantic.Engine.Orchestrator
dotnet run --project src/Services/Semantic.Engine.Workers.Tools
dotnet run --project src/Services/Semantic.Engine.Workers.Data
dotnet run --project src/Api/Semantic.Engine.Gateway
```

On the first start, the Orchestrator and the Data Worker create their database tables and the `pgvector` extension (`Database:ApplyMigrationsOnStartup`). The Orchestrator logs one `Failed executing DbCommand` message about `__orchestrator_migrations` on the very first start; it is the migration check running before the table exists and is expected.

The Gateway listens on `http://localhost:5145`. In the `Development` environment it also serves the OpenAPI document at `/openapi/v1.json` and an interactive Scalar API reference at `/scalar/v1`.

Then start the client:

```bash
dotnet run --project src/Clients/Semantic.Engine.Client.Cli
```

To use a Gateway on another address:

```bash
dotnet run --project src/Clients/Semantic.Engine.Client.Cli -- --Gateway:BaseUrl=http://gateway-host:5145
```

The same setting can be provided as the environment variable `SEMANTIC_ENGINE_Gateway__BaseUrl`.

---

## 5. Using the terminal client

### 5.1 Entering a prompt

The prompt editor supports multi-line input:

| Key | Action |
|---|---|
| Any character | Inserted at the end of the prompt. |
| **Enter** | Inserts a line break. It does **not** submit. |
| **Ctrl+Enter** | Submits the prompt. |
| **Backspace** | Removes the last character, including line breaks. |
| Tab | Inserted as four spaces. |

Because Enter does not submit, you can paste a multi-line code snippet and it arrives as one prompt. Press **Ctrl+Enter** when you are done.

### 5.2 Commands

Type a command as the whole prompt and submit it:

| Command | Effect |
|---|---|
| `/new` | Forgets the current session. The next prompt starts a fresh session with an empty history. |
| `/exit` (or `exit`) | Quits the client. |

**Ctrl+C** also quits.

### 5.3 What you see while the agent works

| Display | Meaning |
|---|---|
| Yellow spinner: `Agent is thinking...` | The model is generating a response. |
| Blue spinner: `Executing dotnet build...` | A tool is running in the Tool Worker (`dotnet build`, `dotnet test` or another tool name). |
| Grey spinner: `Summarizing conversation context...` | The history is being condensed because it became too long. |
| Cyan panel titled `Agent` | The streamed answer. It grows while the model writes. |
| Grey panel with a language label | A fenced code block inside the answer. C# code is syntax-highlighted. |
| Red `Agent failed: ...` | The turn failed. The session stays usable; you can send another prompt. |
| Red `Error: ...` | The client lost the connection to the Gateway. The next prompt starts a new session. |

A single request can contain several spinner and panel phases, for example: text, then `Executing dotnet build...`, then more text, then the final answer.

### 5.4 Example: fix a broken class

Paste the following and press **Ctrl+Enter**:

````text
Fix this C# class so that it compiles. Use your tools.

public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b
    }

    public int Multiply(int a int b)
    {
        return a * b;
    }
}
````

Expected sequence:

1. The agent writes the code to a file in its workspace (`write_file`).
2. It runs `dotnet_build` and receives the compiler errors (for example `CS1002: ; expected`).
3. It fixes the code (`write_file` or `patch_file`) and builds again.
4. When the build succeeds, it answers with an explanation and the corrected code in a `csharp` block.

The number of iterations and the quality of the fix depend on the model. A 7B model can make mistakes; always review the result.

### 5.5 Follow-up prompts

After an answer, you can keep working in the same session, for example: `Add XML documentation to every public member and build again.` The agent keeps the full history of the session and the files it wrote. Send the next prompt only after the current turn has finished; a prompt sent while the agent is busy is rejected with `The agent is busy`.

---

## 6. Working with sessions and workspaces

* A **session** starts with the first prompt after launching the client or after `/new`. Its state is stored in PostgreSQL and survives restarts of the Orchestrator.
* Each session has its own **workspace** directory in the Tool Worker. It is located at `<ToolsWorker:WorkspaceRoot>/<session id without dashes>`. The default root is the relative directory `workspaces` inside the working directory of the Tool Worker process.
* The agent can only read and write inside its workspace. To give the agent existing files, copy them into the session workspace, then ask the agent to read them (`list_files`, `read_file`).
* Files the agent writes stay in the workspace after the session ends. To get its result, copy the files out of the workspace.
* `dotnet build` and `dotnet test` run on a temporary copy, so the workspace never contains `bin` or `obj` folders created by the agent.

The session id is shown by the HTTP API response (section 7). The client does not print it; you can find the newest workspace directory by its modification time.

---

## 7. Using the HTTP API directly

The client is only one possible front-end. Any HTTP client can drive the Gateway.

### Start a session

```bash
curl -s -X POST http://localhost:5145/api/agent/session/start \
  -H "Content-Type: application/json" \
  -d '{"prompt": "Write a C# record Person with Name and Age and build it."}'
```

Response (`202 Accepted`):

```json
{ "sessionId": "70fed602-b6fa-4d71-a166-e87a8ece1760" }
```

An empty prompt returns `400 Bad Request` with validation details. When the Gateway has `Security:ApiKey` configured, add `-H "X-Api-Key: <key>"` to every request; requests without the correct key receive `401 Unauthorized`.

### Read the event stream

```bash
curl -N http://localhost:5145/api/agent/session/<sessionId>/stream
```

The connection stays open for the whole session. Events (`token`, `status`, `done`) and their format are described in the [Architecture Reference](architecture.md#10-gateway-streaming-bridge). Events produced before you connect are buffered for `Streaming:UnclaimedStreamTtlMinutes` minutes. Only one reader can be connected to a session at a time; a request for an unknown session, or for a session that already has a reader, returns `404`.

### Send a follow-up prompt

```bash
curl -s -X POST http://localhost:5145/api/agent/prompt \
  -H "Content-Type: application/json" \
  -d '{"sessionId": "<sessionId>", "prompt": "Add a validation rule: Age must not be negative."}'
```

Response: `202 Accepted`. Wait for the `done` event before sending the next prompt.

The file `src/Api/Semantic.Engine.Gateway/Semantic.Engine.Gateway.http` contains the same requests for the HTTP client of your IDE.

---

## 8. Configuration reference

Every value is configured; none is hard-coded. Edit the `appsettings.json` next to each project, or override a key with an environment variable that uses `__` as the section separator (for example `ToolsWorker__DotnetTimeoutSeconds=300`). A missing or out-of-range value stops the service at startup with an explanatory error.

### Orchestrator (`src/Services/Semantic.Engine.Orchestrator/appsettings.json`)

| Key | Meaning |
|---|---|
| `ConnectionStrings:Postgres` | PostgreSQL connection string. |
| `Database:ApplyMigrationsOnStartup` | Apply EF Core migrations at startup. |
| `RabbitMq:Host`, `VirtualHost`, `Username`, `Password` | Broker connection. |
| `AiSettings:Endpoint` | Ollama URL. |
| `AiSettings:ModelId` | Chat model. |
| `AiSettings:AgentPromptPath` | Path of the agent system prompt file, relative to the application directory. |
| `AiSettings:SummarizerPromptPath` | Path of the summarizer system prompt file. |
| `AiSettings:Temperature` | Sampling temperature of the agent (0 to 2). Low values make tool calls more reliable. |
| `AiSettings:SummarizerTemperature` | Sampling temperature of the summarizer. |
| `AiSettings:MaxRejectedToolCalls` | Invalid tool calls tolerated within one turn before the turn fails. |
| `AiSettings:RequestTimeoutMinutes` | Timeout of one Ollama request. |
| `ContextManagement:MaxMessages` | Message count that triggers summarization. |
| `ContextManagement:MaxEstimatedTokens` | Estimated token count that triggers summarization. |
| `ContextManagement:KeepRecentMessages` | Messages that are never summarized. |
| `ContextManagement:CharactersPerToken` | Characters per token used for the estimate. |
| `ContextManagement:MaxToolOutputCharactersInTranscript` | Tool output length kept in the summarization transcript. |
| `ToolWatchdog:ToolTimeoutSeconds` | How long a session may wait for a tool result before the call is reported as timed out. |
| `ToolWatchdog:ScanIntervalSeconds` | How often waiting sessions are checked. |
| `ClickHouse:Enabled` | Enable thought logging. |
| `ClickHouse:Endpoint`, `Database`, `User`, `Password` | ClickHouse HTTP connection. |
| `ClickHouse:TimeoutSeconds` | Timeout of one insert. |

### Tool Worker (`src/Services/Semantic.Engine.Workers.Tools/appsettings.json`)

| Key | Meaning |
|---|---|
| `RabbitMq:*` | Broker connection. |
| `ToolsWorker:WorkspaceRoot` | Directory that holds the session workspaces. |
| `ToolsWorker:ScaffoldProjectPath` | Project template used when a workspace has no project file. |
| `ToolsWorker:MaxFileSizeBytes` | Largest file the agent may read or write. |
| `ToolsWorker:MaxOutputCharacters` | Longest tool output returned to the model. |
| `ToolsWorker:MaxListEntries` | Entries returned by `list_files`. |
| `ToolsWorker:MaxReportedErrors`, `MaxReportedWarnings` | Compiler diagnostics returned by `dotnet_build`. |
| `ToolsWorker:DotnetTimeoutSeconds` | Time limit of `dotnet build` and `dotnet test`. |
| `ToolsWorker:HttpTimeoutSeconds` | Time limit of `http_request`. |
| `ToolsWorker:AllowPrivateNetworkTargets` | Allow `http_request` to reach loopback and private addresses. Keep `false` unless you need it. |

### Data Worker (`src/Services/Semantic.Engine.Workers.Data/appsettings.json`)

| Key | Meaning |
|---|---|
| `ConnectionStrings:Postgres`, `Database:ApplyMigrationsOnStartup`, `RabbitMq:*` | As above. |
| `DataWorker:OllamaEndpoint` | Ollama URL. |
| `DataWorker:EmbeddingModelId` | Embedding model. It must produce 384-dimensional vectors. |
| `DataWorker:ChunkSizeCharacters` | Maximum chunk length. |
| `DataWorker:ChunkOverlapCharacters` | Overlap between chunks; must be smaller than the chunk size. |
| `DataWorker:SearchDefaultTopK` | Fragments returned by `search_memory` when the model gives no `top_k`. |
| `DataWorker:SearchMaxTopK` | Largest number of fragments `search_memory` may return. |
| `DataWorker:SearchMaxOutputCharacters` | Longest `search_memory` result. |

### Gateway (`src/Api/Semantic.Engine.Gateway/appsettings.json`)

| Key | Meaning |
|---|---|
| `RabbitMq:*` | Broker connection. |
| `Security:ApiKey` | API key required in the `X-Api-Key` header. Empty disables authentication. |
| `Streaming:HeartbeatSeconds` | Interval of keep-alive comments on idle SSE connections. |
| `Streaming:UnclaimedStreamTtlMinutes` | How long events of a session are buffered while no client is connected. |
| `Streaming:SweepIntervalMinutes` | How often unclaimed streams are cleaned up. |

The listening address is set with the standard `ASPNETCORE_URLS` variable or `Properties/launchSettings.json`.

### Client (`src/Clients/Semantic.Engine.Client.Cli/appsettings.json`)

| Key | Meaning |
|---|---|
| `Gateway:BaseUrl` | Gateway address. |
| `Gateway:ApiKey` | API key sent in the `X-Api-Key` header. Must match `Security:ApiKey` of the Gateway; empty when the Gateway has no key. |
| `Gateway:RequestTimeoutSeconds` | Timeout of start and prompt requests. The event stream has no timeout. |
| `Rendering:LiveRefreshMilliseconds` | Minimum interval between redraws of the streaming panel. |

### System prompts

The system prompts are plain Markdown files under `src/Services/Semantic.Engine.Orchestrator/Prompts/`:

* `agent.system.md` — rules for tool calling, the engineering workflow and C# output.
* `summarizer.system.md` — instructions for condensing the history.

To change the agent behavior, edit these files and restart the Orchestrator. To use files from another location, set `AiSettings:AgentPromptPath` and `AiSettings:SummarizerPromptPath` (absolute paths are accepted).

---

## 9. Inspecting what the agent did

### Thought logs (ClickHouse)

Every reasoning step is stored. Show the steps of the latest session:

```bash
docker exec semantic-engine-clickhouse clickhouse-client \
  --user admin --password secret_password \
  --query "SELECT timestamp, step_type, tool_name, substring(content, 1, 120) AS content
           FROM semantic_engine.thought_logs
           WHERE session_id = (SELECT session_id FROM semantic_engine.thought_logs ORDER BY timestamp DESC LIMIT 1)
           ORDER BY timestamp"
```

Useful questions:

| Question | Query fragment |
|---|---|
| Which tools were used and how often? | `SELECT tool_name, count() FROM semantic_engine.thought_logs WHERE step_type = 'tool_call' GROUP BY tool_name` |
| Which tool calls were rejected? | `WHERE step_type = 'tool_rejected'` |
| How long did the model take per step? | `SELECT avg(duration_ms) FROM semantic_engine.thought_logs WHERE step_type = 'assistant_message'` |
| Which turns failed? | `WHERE step_type = 'turn_failed'` |

### Session state (PostgreSQL)

```bash
docker exec semantic-engine-postgres psql -U admin -d semantic_hub \
  -c "SELECT \"CorrelationId\", \"CurrentState\", \"UpdatedAtUtc\" FROM agent_sessions ORDER BY \"UpdatedAtUtc\" DESC LIMIT 5"
```

`CurrentState` is `Active` (waiting for a prompt), `Thinking` or `AwaitingTool`.

### Stored memories (PostgreSQL)

```bash
docker exec semantic-engine-postgres psql -U admin -d semantic_hub \
  -c "SELECT \"SessionId\", \"Source\", \"ChunkIndex\", left(\"Content\", 80) FROM memory_chunks ORDER BY \"CreatedAtUtc\" DESC LIMIT 10"
```

The Orchestrator stores the user prompt (`Source = user-prompt`) and every conversation checkpoint (`Source = summary-checkpoint`). The agent can query these memories with the `search_memory` tool.

### Message bus

Open the RabbitMQ management UI at `http://localhost:15672` to see queues (`run-agent-turn`, `execute-tool`, `index-text`, `agent-session-state`, `tool-result-log`) and their message counts. A message that stays in `run-agent-turn` is a turn that is still running.

### Service logs

Each service writes structured logs to its console. The Tool Worker logs every tool call and whether it succeeded; the Orchestrator logs rejected tool calls and failed turns.

---

## 10. Running the tests

```bash
dotnet test Semantic.Engine.slnx
```

The suite contains unit tests for the saga transitions (including tool routing) (with the MassTransit test harness), send-safety validation, the text tool-call parser, context pruning, workspace path safety, atomic patching, network address policy, text chunking, the SSE hub and the client input, parsing and rendering helpers. The tests need no Docker, Ollama or network.

---

## 11. Troubleshooting

### The client shows `Agent is thinking...` for a very long time

* The first request after Ollama starts loads the model into memory. On a machine with little GPU memory this can take several minutes. Check the Ollama log, or run `ollama ps`; the model appears there once it is loaded.
* Reduce the load by using a smaller model: set `AiSettings__ModelId=qwen2.5-coder:1.5b`. Smaller models follow the tool-calling rules less reliably.
* Check the queue `run-agent-turn` in the RabbitMQ UI. A message with one consumer means the turn is being processed.
* If a request exceeds `AiSettings:RequestTimeoutMinutes`, the turn fails and the client prints `Agent failed`.

### `Configuration error` or a service stops right after start

A configuration value is missing or out of range. The error message names the section and the property. Compare your `appsettings.json` with the [configuration reference](#8-configuration-reference).

### The Orchestrator or the Data Worker cannot reach PostgreSQL or RabbitMQ

```bash
docker compose ps
docker compose up -d
```

Verify the host names and credentials in `ConnectionStrings:Postgres` and `RabbitMq:*`.

### `Agent failed: The model repeatedly produced invalid tool calls.`

The model produced calls that failed send-safety validation more than `AiSettings:MaxRejectedToolCalls` times. Send the prompt again, make the task more specific, lower `AiSettings:Temperature`, or use a larger model. The rejection reasons are stored as `tool_rejected` rows in the thought log.

### `Agent failed: Reasoning turn failed: ...`

The reasoning call raised an error, usually because Ollama is not running or the model is not pulled. Check:

```bash
ollama list
curl http://localhost:11434/api/tags
```

### The client prints `Error: The connection to the Gateway was lost.`

The Gateway restarted or the network failed. Start the Gateway and send the next prompt; the client starts a new session automatically. Session state in PostgreSQL is not reconnected to the new stream.

### `The agent is busy`

A prompt was sent while the agent was still working. Wait for the turn to end and send the prompt again.

### A session stays in `AwaitingTool`

The Tool Worker (or the Data Worker for `search_memory`) was not running when the tool call was published, or it stopped during the call. Start the worker; RabbitMQ delivers a command that is still queued. If no result arrives within `ToolWatchdog:ToolTimeoutSeconds`, the Orchestrator reports a timeout to the agent, which then continues. Lower this value to react faster; raise it when builds are slow.

### `dotnet_build` reports `timed out`

Increase `ToolsWorker:DotnetTimeoutSeconds`. The first build in a fresh environment can be slow because the .NET SDK warms up.

### `http_request` reports `is not a public internet address and is blocked`

This is the network safety control. To let the agent call a service on your own network, set `ToolsWorker:AllowPrivateNetworkTargets` to `true` and restart the Tool Worker. Understand that the agent can then reach every service that is reachable from that host.

### Ctrl+Enter inserts a line break instead of submitting

Your terminal does not report the Control modifier for Enter. Use Windows Terminal or conhost, or send prompts through the HTTP API (section 7).

### Resetting everything

```bash
docker compose down -v
```

This deletes the PostgreSQL, RabbitMQ, ClickHouse and Metabase volumes, including all sessions, memories and thought logs. Session workspaces on disk are not affected; delete the `workspaces` directory of the Tool Worker manually if you no longer need it.

---

## 12. Current limitations

* The project is a proof of concept under active development, executed according to the [PoC roadmap](proof-of-concept/roadmap.md). It is not a finished or production-ready product.
* Authentication is a single shared API key that is disabled by default; the Gateway serves plain HTTP.
* The agent runs one tool call at a time and one reasoning turn at a time.
* The agent searches only memories of its own session (`search_memory`); there is no cross-session search.
* The agent loop does not ask the user questions in the middle of a task; the human-in-the-loop state and the `task_session.xml` workflow described in [Agentic Workflow](agent-workflow.md) are not implemented yet.
* Answer quality, speed and tool-calling reliability depend on the local model and the hardware.
* A prompt cannot be queued while the agent is busy.
* Sessions do not expire automatically, and workspaces are not deleted automatically.
