# Proof of Concept (PoC) Implementation Roadmap

This roadmap outlines the systematic, step-by-step engineering sequence required to build, integrate, and validate the `semantic-engine` asynchronous multi-agent runtime.

---

## 🏗️ Phase 1: Shared Contracts & Infrastructure Bootstrap
*Objective: Define the shared type system and data models to allow MassTransit and RabbitMQ to route messages between services seamlessly.*

- [ ] **1.1. Create `Semantic.Engine.Contracts` Project**
  * Establish a class library for MassTransit commands, events, and state schemas.
  * Define `StartAgentSessionCommand` (triggered by Gateway).
  * Define `ExecuteToolCommand` and `ToolExecutedEvent` (payload passing between Orchestrator and Tool Worker).
  * Define `AgentTokenGeneratedEvent` (for real-time SSE streaming back to the client).

- [ ] **1.2. Define the Core State Machine (Saga) States**
  * Implement `AgentSessionState` class inheriting from MassTransit's `SagaStateMachineInstance`.
  * Properties must include: `CorrelationId`, `CurrentState`, `ConversationHistoryJson`, `LastStateSnapshot`.

- [ ] **1.3. Configure Docker-Compose Environments**
  * Initialize **RabbitMQ** (with Management plugin enabled).
  * Initialize **PostgreSQL** (and install the `pgvector` extension via migration script).
  * Initialize **ClickHouse** (configure target tables for thought tracking logs using the `Log` or `MergeTree` engines).

---

## 🧠 Phase 2: Agent Orchestrator & Local LLM Integration
*Objective: Build the core reasoning loop using Microsoft.Extensions.AI and implement automated context management.*

- [ ] **2.1. Integrate `qwen2.5-coder:7b` via Ollama**
  * Pull the model locally: `ollama run qwen2.5-coder:7b`.
  * Set up `appsettings.json` in the Orchestrator to map `AiSettingsOptions` cleanly using the .NET Options Pattern.

- [ ] **2.2. Implement the Asynchronous Agent Loop**
  * Create a background service wrapping `IChatClient.GetStreamingResponseAsync()`.
  * Inject a highly rigid, engineering-focused **System Prompt** forcing the model to respect JSON tool-calling constraints and maintain professional C# output.

- [ ] **2.3. Implement the Context Management & Summarization Engine**
  * Write a service that counts context tokens or tracking message thresholds.
  * Implement active history pruning: when thresholds are breached, trigger an internal background summarization prompt to condense old discussion threads into a single checkpoint message, truncating the operational SQL/PostgreSQL database state.

---

## 🛠️ Phase 3: Distributed Workers Implementation
*Objective: Give the agent its "hands and eyes" by establishing isolated, safe environments for system operations and semantic data analysis.*

- [ ] **3.1. Build `Semantic.Engine.Workers.Tools`**
  * Create a Worker Service implementing MassTransit `IConsumer<ExecuteToolCommand>`.
  * **File Tool Implementation:** Add atomic write, read, and patch capabilities (using safe path validations).
  * **Network Tool Implementation:** Integrate a non-blocking `HttpClient` factory allowing the LLM to make target API requests.
  * **Compilation Criticism Loop:** Add a tool that triggers `dotnet build / dotnet test` in a temporary execution directory, intercepts CLI compiler errors, and returns them natively within the `ToolExecutedEvent` payload to force agent self-correction.

- [ ] **3.2. Build `Semantic.Engine.Workers.Data`**
  * Pull the `all-minilm:l6-v2` embedding model via Ollama (~90 MB RAM allocation).
  * Implement background text parsing, tokenization, and vector generation.
  * Configure Entity Framework Core mappings to index generated embeddings directly into PostgreSQL `pgvector` tables.

---

## 🌐 Phase 4: API Gateway & SSE Streaming Pipeline
*Objective: Connect the asynchronous microservice bus back to synchronous client triggers using Minimal APIs and Server-Sent Events.*

- [ ] **4.1. Implement Gateway Endpoint Infrastructure**
  * Build a Minimal API endpoint `POST /api/agent/session/start` that publishes `StartAgentSessionCommand` to RabbitMQ.
  * Implement `POST /api/agent/prompt` to stream subsequent interactions into the existing MassTransit Saga flow.

- [ ] **4.2. Build the SSE Streaming Bridge**
  * Implement a real-time HTTP streaming controller using Server-Sent Events (`Response.WriteAsync`).
  * Create an internal bridge (using a localized MassTransit consumer or an in-memory Pub/Sub like Channels/Redis) that intercepts `AgentTokenGeneratedEvent` from the bus and immediately pumps the text tokens into the client's open HTTP connection.

---

## 💻 Phase 5: Client Application (TUI Engine)
*Objective: Replace volatile standard console reads with a professional Terminal User Interface inspired by Claude Code.*

- [ ] **5.1. Implement Multi-Line Clipboard Input**
  * Refactor `Program.cs` in `Semantic.Engine.Cli` to use an advanced `Console.ReadKey(intercept: true)` loop.
  * Map **Ctrl + Enter** as the explicit submission trigger, ensuring multi-line C# snippets can be cleanly pasted without breaking terminal formatting.

- [ ] **5.2. Integrate `Spectre.Console` Visuals**
  * Replace default console outputs with `AnsiConsole` rendering components.
  * Implement live, animated spinners (`AnsiConsole.Status`) to cleanly display background processing states (e.g., `[yellow]Agent is thinking...[/]`, `[blue]Executing dotnet build...[/]`).
  * Enclose streamed text outputs inside structured panels (`Panel`) with strict language color-coding for readability.

---

## 🏁 Verification Strategy (How to Test the PoC)
1. Spin up the infrastructure via `docker-compose up -d`.
2. Launch `Orchestrator`, `Workers.Tools`, `Workers.Data`, and `Gateway`.
3. Open `Semantic.Engine.Cli` and paste a deliberately broken C# class containing syntax errors.
4. Verify that:
   - The Orchestrator calls the Tool Worker to build the code.
   - The compiler error is caught and piped back to Qwen 7B.
   - Qwen 7B successfully self-corrects the bug, logs its thoughts into ClickHouse, and outputs the pristine refactored version back to your TUI client screen.
