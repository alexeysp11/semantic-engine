# Architecture Reference

This document describes how `semantic-engine` works: the services, the messages they exchange, the session state machine, the agent loop, the storage model and the safety controls. For installation and day-to-day usage, see the [User Guide](user-guide.md). For the implementation plan, see the [PoC roadmap](proof-of-concept/roadmap.md).

---

## 1. System Overview

`semantic-engine` is an event-driven runtime for an autonomous coding agent. A user sends a prompt from a terminal client; the agent reasons with a local LLM, calls tools (file operations, HTTP, `dotnet build`/`dotnet test`) through a message bus, observes the results and continues until it produces a final answer.

```text
 ┌────────────────────┐   HTTP (JSON) + SSE    ┌──────────────────────┐
 │ Semantic.Engine.Cli│ ─────────────────────▶ │ Semantic.Engine.     │
 │ (Spectre.Console)  │ ◀───────────────────── │ Gateway (Minimal API)│
 └────────────────────┘                        └──────────┬───────────┘
                                                  publish │ ▲ consume
                                                          ▼ │ token/status events
                                            ┌─────────────────────────────┐
                                            │      RabbitMQ (MassTransit) │
                                            └───┬──────────┬──────────┬───┘
                                                │          │          │
                       ┌────────────────────────▼──┐  ┌────▼─────────────────┐  ┌──▼─────────────────────┐
                       │ Semantic.Engine.          │  │ Semantic.Engine.     │  │ Semantic.Engine.       │
                       │ Orchestrator              │  │ Workers.Tools        │  │ Workers.Data           │
                       │ saga + reasoning loop     │  │ files / HTTP / dotnet│  │ chunking + embeddings  │
                       └───┬───────────┬───────────┘  └──────────┬───────────┘  └──────────┬─────────────┘
                           │           │                         │                         │
                  Ollama (chat)   ClickHouse             session workspaces         Ollama (embeddings)
                  PostgreSQL      (thought logs)         (local directories)        PostgreSQL + pgvector
                  (saga state)
```

| Component | Project | Responsibility |
|---|---|---|
| Client | `src/Clients/Semantic.Engine.Client.Cli` | Terminal UI. Talks to the Gateway over HTTP and Server-Sent Events only. |
| Gateway | `src/Api/Semantic.Engine.Gateway` | Accepts prompts, publishes commands to the bus and streams agent events back to the client. |
| Orchestrator | `src/Services/Semantic.Engine.Orchestrator` | Owns the session state machine (saga), runs the reasoning loop against the LLM, manages the context window and writes thought logs. |
| Tool Worker | `src/Services/Semantic.Engine.Workers.Tools` | Executes tool calls inside per-session workspaces. |
| Data Worker | `src/Services/Semantic.Engine.Workers.Data` | Splits text into chunks, creates embeddings and stores them in PostgreSQL with `pgvector`. |
| Contracts | `src/Shared/Semantic.Engine.Contracts` | Message contracts, tool names and the saga state class shared by all services. |
| Unit tests | `tests/Semantic.Engine.UnitTests` | Tests for the logic of every component. |

Infrastructure (see `docker-compose.yml`): RabbitMQ, PostgreSQL with `pgvector`, ClickHouse and Metabase. The LLM runtime is Ollama, which runs outside Docker.

---

## 2. Messages

All messages are `sealed record` types with `get; init;` properties, defined in `Semantic.Engine.Contracts`.

### Commands

| Message | Published by | Consumed by | Purpose |
|---|---|---|---|
| `StartAgentSessionCommand` | Gateway | Orchestrator saga | Creates a session with the first prompt. `SessionId` is the saga correlation id. |
| `SubmitAgentPromptCommand` | Gateway | Orchestrator saga | Sends a follow-up prompt into an idle session. |
| `RunAgentTurnCommand` | Orchestrator saga | Orchestrator `RunAgentTurnConsumer` | Requests one reasoning iteration. Carries the current conversation history. |
| `ExecuteToolCommand` | Orchestrator saga | Tool Worker | Requests execution of one tool call. |
| `IndexTextCommand` | Orchestrator | Data Worker | Requests embedding and storage of a text. |
| `SearchMemoryCommand` | Orchestrator saga | Data Worker | Requests a semantic search for the `search_memory` tool. The Data Worker answers with a `ToolExecutedEvent`. |

### Events

| Message | Published by | Consumed by | Purpose |
|---|---|---|---|
| `AgentTurnCompletedEvent` | `RunAgentTurnConsumer` | Orchestrator saga | Result of a reasoning iteration: updated history plus an optional tool call or a failure reason. |
| `ToolExecutedEvent` | Tool Worker, Data Worker, tool watchdog | Orchestrator saga, `ToolResultLogConsumer` | Observation of a tool call, including compiler errors. |
| `AgentTokenGeneratedEvent` | Orchestrator | Gateway | A fragment of model text; `IsFinal` marks the end of a turn. |
| `AgentStatusChangedEvent` | Orchestrator | Gateway | `Thinking`, `ExecutingTool`, `Summarizing` or `Failed`, with an optional detail. |
| `TextIndexedEvent` | Data Worker | — | Confirms that a text was stored. |

---

## 3. Session State Machine (Saga)

The Orchestrator hosts a MassTransit state machine, `AgentSessionStateMachine`, whose instance is `AgentSessionState`:

| Property | Meaning |
|---|---|
| `CorrelationId` | The session id. |
| `CurrentState` | `Active`, `Thinking` or `AwaitingTool` (stored as a string). |
| `ConversationHistoryJson` | The full conversation history, serialized with the `Microsoft.Extensions.AI` JSON options. The saga is the single owner of the history. |
| `LastStateSnapshot` | JSON of the pending tool call while the agent is frozen; `null` otherwise. |
| `CreatedAtUtc`, `UpdatedAtUtc` | Timestamps. |

States:

* **Active** — the session is open and waits for a prompt.
* **Thinking** — the model is generating a response.
* **AwaitingTool** — the agent is frozen until the Tool Worker reports the result of the requested tool call.

Transitions:

```text
(initial) ──StartAgentSessionCommand──▶ Thinking
Active    ──SubmitAgentPromptCommand──▶ Thinking
Thinking  ──AgentTurnCompletedEvent (no tool call)──▶ Active      publishes AgentTokenGeneratedEvent{IsFinal}
Thinking  ──AgentTurnCompletedEvent (tool call)─────▶ AwaitingTool publishes ExecuteToolCommand
Thinking  ──AgentTurnCompletedEvent (failure)───────▶ Active      publishes AgentStatusChangedEvent{Failed}
AwaitingTool ──ToolExecutedEvent (matching call id)─▶ Thinking
```

Rules:

* A prompt that arrives while the session is `Thinking` or `AwaitingTool` is rejected with `AgentStatusChangedEvent{Failed, "The agent is busy..."}`; it is not queued.
* A `ToolExecutedEvent` whose `ToolCallId` does not match the pending call is ignored.
* Saga state is stored in PostgreSQL (`agent_sessions` table) with pessimistic row locking. Messages published while the saga handles an event leave the process only after the state is saved (in-memory outbox).

### 3.1 Tool watchdog

A session in `AwaitingTool` is frozen until a `ToolExecutedEvent` arrives. If the worker that owns the tool is down or the message was lost, `ToolWatchdogService` (a hosted service in the Orchestrator) scans `agent_sessions` every `ToolWatchdog:ScanIntervalSeconds` seconds. For every session that has been in `AwaitingTool` longer than `ToolWatchdog:ToolTimeoutSeconds`, it reads the pending call from `LastStateSnapshot` and publishes a failed `ToolExecutedEvent` (`The tool '<name>' did not respond within N seconds...`). The saga resumes the model with that observation. Because the state lives in PostgreSQL, sessions that were frozen before an Orchestrator restart are recovered too. A late genuine result for the same call is ignored because the saga has already left `AwaitingTool`.

---

## 4. The Agent Loop

One reasoning iteration is executed by `AgentReasoningService`, invoked from `RunAgentTurnConsumer`. The consumer endpoint runs with a concurrency limit of 1 because the local model is the bottleneck.

1. **Prune the context.** `ContextManager` checks the history against the configured thresholds (see section 5) and condenses old messages when they are exceeded.
2. **Build the request.** The system prompt, loaded from a file (see section 9), is placed in front of the history. The nine tool declarations are attached to the request.
3. **Stream the response.** `IChatClient.GetStreamingResponseAsync()` is called. Text fragments are published as `AgentTokenGeneratedEvent` messages, which the Gateway forwards to the client. `ToolCallStreamFilter` withholds text that starts a line with `{` or `<` outside a code fence, because it is probably a tool call written as JSON; it releases the text after the response if it was not a tool call.
4. **Extract a tool call.** If the model returned a native function call, it is used. If the model wrote a call as JSON text (`{"name": "...", "arguments": {...}}`, optionally inside `<tool_call>` tags or a markdown fence), `TextToolCallParser` recovers it. Only the first call of a response is honored.
5. **Validate (send-safety).** `ToolCallGuard` rejects unknown tools, malformed arguments, missing required arguments and path arguments that are absolute or contain `..`. A rejected call is answered with an error tool result *inside the same turn* so that the model can correct itself; after `AiSettings:MaxRejectedToolCalls` consecutive rejections the turn fails.
6. **Return.** The consumer publishes `AgentTurnCompletedEvent` with the updated history and the validated tool call, if any.

The saga then either completes the exchange or sends `ExecuteToolCommand` and freezes the session. When the result arrives, the saga appends it to the history as a tool message and starts the next iteration. The loop ends when the model answers without a tool call.

---

## 5. Context Management

The history grows with every message and tool output, but a CPU-hosted model has a limited context window. `ContextManager` keeps the history bounded:

* **Triggers.** The history is pruned when its message count exceeds `ContextManagement:MaxMessages` or its estimated size exceeds `ContextManagement:MaxEstimatedTokens`. The estimate is the character count divided by `ContextManagement:CharactersPerToken`.
* **Split point.** The most recent `ContextManagement:KeepRecentMessages` messages are always kept verbatim. The split point moves backwards when necessary so that a tool result is never separated from the assistant message that requested it.
* **Summarization.** The older part is rendered as a transcript (tool outputs are truncated to `ContextManagement:MaxToolOutputCharactersInTranscript`) and sent to the model with the summarizer prompt. The answer replaces the old messages as a single message that starts with `[Conversation checkpoint]`.
* **Persistence.** Because the saga stores the history returned by the turn, the pruned history is what remains in PostgreSQL; the original messages are dropped from the operational state.
* **Memory.** The checkpoint text is also published as an `IndexTextCommand`, so it stays searchable in the vector store.
* **Visibility.** While summarization runs, the client shows `Summarizing conversation context...`.

---

## 6. Tools

Tool definitions (names, descriptions and JSON schemas) live in `ToolCatalog` in the Orchestrator. The implementations live in the Tool Worker. All paths are relative to the session workspace.

| Tool | Arguments | Behavior |
|---|---|---|
| `list_files` | `path?` | Lists files and directories, hiding `bin`, `obj` and `.git`. Output is limited to `ToolsWorker:MaxListEntries`. |
| `read_file` | `path` | Returns the UTF-8 content of a file up to `ToolsWorker:MaxFileSizeBytes`. |
| `write_file` | `path`, `content` | Creates or overwrites a file atomically (temporary file in the same directory, then move). |
| `patch_file` | `path`, `old_text`, `new_text` | Replaces one exact fragment atomically. Fails when the fragment is missing or occurs more than once. LF fragments match CRLF files. |
| `insert_at_line` | `path`, `line`, `text` | Inserts text after a 1-based line (`0` inserts at the start) atomically, preserving the file line endings. |
| `http_request` | `method`, `url`, `body?` | Sends a `GET`, `POST`, `PUT` or `DELETE` request and returns the status line and the body, truncated to `ToolsWorker:MaxOutputCharacters`. Redirects are not followed. |
| `search_memory` | `query`, `top_k?` | Semantic search over the stored memories of the session (executed by the Data Worker, see section 6.1). Returns the most similar fragments with their similarity. |
| `dotnet_build` | `project_path?` | Builds a copy of the workspace and returns compiler errors. |
| `dotnet_test` | `project_path?` | Runs tests on a copy of the workspace. |

### 6.1 Memory search

`search_memory` is declared in the same catalog as the other tools but is executed by the Data Worker, because the Data Worker owns the vector store. The saga recognizes the tool name and publishes a `SearchMemoryCommand` instead of an `ExecuteToolCommand`. The consumer embeds the query with the embedding model, orders the `memory_chunks` rows of the session by cosine distance and returns up to `DataWorker:SearchMaxTopK` fragments (default `DataWorker:SearchDefaultTopK`) with their source and similarity. The answer is a normal `ToolExecutedEvent`, so the saga treats it like any other observation. Memory contains the user prompts and the conversation checkpoints of the session.

### Compilation criticism loop

`dotnet_build` and `dotnet_test` run in a temporary directory:

1. The session workspace is copied (without `bin`, `obj`, `.git`).
2. If no `.csproj`, `.sln` or `.slnx` exists and no `project_path` is given, a minimal class library project is created from the template configured in `ToolsWorker:ScaffoldProjectPath`, so loose `.cs` files can be compiled.
3. The `dotnet` CLI runs with a timeout of `ToolsWorker:DotnetTimeoutSeconds`.
4. Compiler diagnostics are extracted, de-duplicated, stripped of the temporary path and returned in the `ToolExecutedEvent`. A failed build is reported with `Succeeded = false` and the list of errors (at most `ToolsWorker:MaxReportedErrors`).
5. The temporary directory is deleted.

The model reads the errors in the next iteration and fixes the code. This is the self-correction loop.

### Failure model

The Tool Worker always answers an `ExecuteToolCommand`. Expected failures (`ToolExecutionException`, invalid JSON, I/O and network errors) become a `ToolExecutedEvent` with `Succeeded = false` and a message for the model.

---

## 7. Safety Controls

| Layer | Control |
|---|---|
| Orchestrator | `ToolCallGuard` validates tool name, arguments and paths before a call is published. |
| Tool Worker, paths | `WorkspacePathResolver` rejects absolute paths, `..` escapes and paths that cross symbolic links or junctions. This is the authoritative check. |
| Tool Worker, files | File size limit; atomic writes; no deletion tool. |
| Tool Worker, network | Targets that resolve to loopback, private, link-local or otherwise internal addresses are blocked unless `ToolsWorker:AllowPrivateNetworkTargets` is `true`. Redirects are disabled so they cannot bypass the check. |
| Tool Worker, processes | The only process the agent can start is `dotnet build` or `dotnet test` with a timeout; the process tree is killed on timeout. |
| Gateway | Rejects empty prompts with `400`. When `Security:ApiKey` is set, every `/api/agent` endpoint requires the `X-Api-Key` header; the key is compared in constant time and a missing or wrong key returns `401`. |

Known limits of the current implementation:

* Authentication is a single shared API key and is disabled while `Security:ApiKey` is empty. The Gateway serves plain HTTP; put it behind a TLS-terminating proxy outside a local machine.
* `dotnet build` and `dotnet test` execute code from the workspace (for example MSBuild tasks, analyzers and tests) with the privileges of the Tool Worker process. Treat the Tool Worker host as a sandbox boundary and run it with limited rights.
* A session that waits for a tool result is unfrozen by the tool watchdog (section 3.1) after `ToolWatchdog:ToolTimeoutSeconds`. The model receives a failed result and can try another approach.
* DNS-based network checks resolve the name once before the request.

---

## 8. Storage

### PostgreSQL (`semantic_hub`)

| Table | Owner | Content |
|---|---|---|
| `agent_sessions` | Orchestrator | Saga state (see section 3). |
| `memory_chunks` | Data Worker | `Id`, `SessionId`, `Source`, `ChunkIndex`, `Content`, `Embedding vector(384)`, `CreatedAtUtc`. An HNSW index on `embedding` uses cosine distance; a B-tree index covers `SessionId`. |
| `__orchestrator_migrations`, `__data_migrations` | EF Core | Migration history, one table per context so the two services migrate independently. |

The `pgvector` extension is created by the first migration of the Data Worker. Both services apply their migrations on startup when `Database:ApplyMigrationsOnStartup` is `true`.

The embedding model `all-minilm:l6-v2` produces 384-dimensional vectors, which is fixed by the schema. Changing the embedding model to one with a different dimensionality requires a new migration.

### ClickHouse (`semantic_engine.thought_logs`)

One row per reasoning step, inserted through the ClickHouse HTTP interface:

| Column | Content |
|---|---|
| `timestamp` | UTC time with millisecond precision. |
| `session_id` | Session id. |
| `step_type` | `assistant_message`, `tool_call`, `tool_rejected`, `tool_result`, `summary_checkpoint` or `turn_failed`. |
| `tool_name` | Tool name for tool-related steps. |
| `content` | Model text, tool arguments, tool output or a summary. |
| `duration_ms` | Duration of the model call for `assistant_message`. |

The table uses the `MergeTree` engine, partitioned by month and ordered by `(session_id, timestamp)`. Thought logging is a diagnostic side channel: if ClickHouse is unavailable, the Orchestrator logs a warning and continues.

---

## 9. Configuration Model

Every tunable value is supplied by configuration. Option classes contain no default values; they are bound from `appsettings.json` (or environment variables) and validated on startup with data annotations, so a missing or out-of-range value stops the service with a clear error. System prompts are files, not code.

Environment variables override `appsettings.json` using the standard `__` separator, for example `AiSettings__ModelId=qwen2.5-coder:1.5b`.

The complete list of keys per service is in the [User Guide](user-guide.md#8-configuration-reference).

---

## 10. Gateway Streaming Bridge

The client receives agent output over Server-Sent Events:

1. `POST /api/agent/session/start` creates the session id, creates an in-memory stream for it (`SessionStreamHub`) and **then** publishes `StartAgentSessionCommand`. Events produced before the client connects are buffered, so no early token is lost.
2. Every Gateway instance owns a private, auto-delete RabbitMQ queue bound to `AgentTokenGeneratedEvent` and `AgentStatusChangedEvent`. One consumer with a concurrency limit of 1 handles both types, which preserves the order of tokens and statuses.
3. `GET /api/agent/session/{sessionId}/stream` attaches a single reader to the session stream and writes events with `Response.WriteAsync`. A keep-alive comment is sent every `Streaming:HeartbeatSeconds` seconds.
4. Streams that no client claimed within `Streaming:UnclaimedStreamTtlMinutes` minutes are removed by a background sweeper.

Wire format:

```text
event: token
data: {"text":"public class"}

event: status
data: {"status":"ExecutingTool","detail":"dotnet_build"}

event: done
data: {}
```

---

## 11. Client Rendering

The client alternates between two Spectre.Console modes because they cannot run at the same time:

* **Waiting** — `AnsiConsole.Status` with a spinner: `Agent is thinking...`, `Executing dotnet build...`, `Summarizing conversation context...`.
* **Streaming** — a live `Panel` that is redrawn at most every `Rendering:LiveRefreshMilliseconds` milliseconds. Fenced code blocks become nested panels labeled with their language; C# blocks are syntax-highlighted (keywords, types, strings, numbers, comments); other languages are rendered as plain text.

A turn can contain several waiting and streaming phases, for example text, a build, then the final answer.
