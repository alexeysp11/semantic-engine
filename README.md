# 🤖 semantic-engine

![.NET Version](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Architecture](https://img.shields.io/badge/Architecture-Event--Driven%20(EDA)-orange)
![Orchestration](https://img.shields.io/badge/Orchestrator-MassTransit%20%7C%20RabbitMQ-red?logo=rabbitmq&logoColor=white)
![AI Framework](https://img.shields.io/badge/AI%20Core-Microsoft.Extensions.AI-blue?logo=microsoft&logoColor=white)
![Vector DB](https://img.shields.io/badge/Vector%20Storage-PostgreSQL%20%2B%20pgvector-emerald?logo=postgresql&logoColor=white)

Distributed Event-Driven Multi-Agent Runtime built on .NET 10.

## 📋 Overview
**semantic-engine** is a distributed, asynchronous execution runtime and infrastructure backend core for autonomous AI agents. The project is designed around an Operating System metaphor, where agents function as isolated system processes, and the message broker serves as a reactive system bus.

Unlike fragile, linear automation scripts and traditional synchronous RAG systems, this platform provides a resilient, enterprise-grade ecosystem. It enables running a local reasoning loop (Thought -> Action -> Observation) in a fully asynchronous, non-blocking mode with guaranteed context delivery, strict safety controls over tool execution, and end-to-end tracing of agent thought processes.

## 🏗️ Architecture & Core Components
The system is built as an Event-Driven Microservice Architecture (EDA) on the **.NET 10** platform, orchestrated via MassTransit and RabbitMQ. Each stage of model reasoning, code execution, or agent interaction is isolated and does not block web server threads.

1. 💻 **Client Application (CLI Client):**
   * Serves as the primary user interface, providing a native console experience that emulates a local AI environment.
   * Abstracted from the core business logic, communicating with the backend exclusively via HTTP requests and SSE streams.
   * Architecturally decoupled, allowing seamless future integration of Web dashboards, Telegram bots, or IDE extensions without changes to the backend.
2. 🚪 **Gateway Service (Minimal API):**
   * Provides the entry point for user or system triggers.
   * Manages sessions, security context (JWT), and real-time streaming of agent responses via Server-Sent Events (SSE).
3. 🧠 **Agent Orchestrator Service (Kernel Core):**
   * The backend core of the system, implementing the agent execution loop (Agent Loop) on top of `Microsoft.Extensions.AI` (`IChatClient`) abstractions.
   * Responsible for orchestrating the hierarchy of agent processes and managing distributed state (State Management).
   * **Advanced Context Management:** Dynamically optimizes the LLM context window to prevent performance degradation on CPU runtimes. Implements active history purging, context sliding, and asynchronous summarization (collapsing deep message histories into concise state-summary checkpoints stored in PostgreSQL).
4. 🛠️ **Distributed Tool Execution Worker:**
   * A highly capable, isolated sandboxed environment acting as both the **Executor** and the **Critic** for the AI Agent.
   * **Execution Capabilities:** Performs atomic file system operations (targeted file editing, path creation), executes local CLI commands, and manages external HTTP/API communication.
   * **Validation & Criticism Loop:** Automatically validates agent outputs by triggering compilation pipelines (`dotnet build`), running test suites (`dotnet test`), and analyzing runtime diagnostics or build errors to pipe structured feedback back into the Orchestrator loop.
5. 🧩 **Data & Embedding Worker:**
   * A background service for parsing, tokenizing, and semantically analyzing incoming data streams.
   * Responsible for generating vector embeddings via local Ollama models (`nomic-embed-text`) and indexing them.

### Integration Flow
```text
       [ Clients Layer: CLI App / Web / Telegram BOTS ]
                             │
                             ▼ (HTTP / SSE Requests)
                     [ Gateway / SSE ] 
                             ▲
                             │ (User/System Trigger)
                             ▼
            [ Agent Orchestrator (IChatClient Loop) ]
             │                                     │
             ▼ (MassTransit Events)                ▼ (ExecuteToolCommand)
[ Data / Embedding Worker ]             [ Distributed Tool Worker ]
             │                                     │
             ▼                                     ▼
[ PostgreSQL + pgvector ] (OLTP State)  [ Local Files / DB / Foreign APIs ]
```

---

## 🔀 Interaction Pattern: Event-Driven Agentic Choreography & Sagas
Component interaction is based on pure event choreography (Choreography Pattern). Agents are designed as autonomous consumers of messages within the RabbitMQ bus, ensuring loose coupling and strict adherence to the Open-Closed Principle.

To build guaranteed execution sequences and rigid pipelines—where the output of one agent is transactionally passed to the next—the platform utilizes the **MassTransit Sagas (State Machine)** pattern.

### Distributed Agent Loop & Send-Safety
Instead of synchronously waiting for a local LLM response within an HTTP thread, `semantic-engine` implements an asynchronous, distributed reasoning loop:

1. **Orchestrator** initializes a reasoning iteration via `IChatClient`.
2. The model generates an intent to call an external capability (Tool Calling) to interact with the local system.
3. **Orchestrator** intercepts the call and passes it through **Tool Middleware (Interceptors)**, performing:
   * **Send-Safety Validation:** Checking arguments for prompt injections, validating resource ownership, and filtering allowed commands.
4. **Orchestrator** publishes an `ExecuteToolCommand` to the bus, creates a state snapshot (State Snapshotting), and freezes the agent process in the database.
5. **Distributed Tool Worker** consumes the command, safely executes the backend code, and returns a `ToolExecutedEvent` (Observation) to the bus.
6. **Orchestrator** activates upon receiving the event, restores the agent state from the snapshot, feeds the execution result back into `IChatClient`, and continues the loop until the final artifact is produced.

```text
[IChatClient] ──(1. Tool Call)──> [Tool Middleware] ──(2. Send-Safety)──> [MassTransit]
                                                                                │
[IChatClient] ◄──(4. Resume Loop)◄── [Orchestrator] ◄──(3. ToolExecuted) ◄──────┘
```

### Runtime Benefits
* **Resilient Scalability:** Slow text generation by a local CPU-bound Ollama instance or temporary unavailability of external APIs does not lead to system failures. Messages are buffered within RabbitMQ queues, Kestrel threads remain unblocked, and process memory leaks are eliminated.
* **Production-Grade Observability:** The lifecycle of each executed task is tagged with a unique `CorrelationId`. Distributed tracing logs are exported via OpenTelemetry into Jaeger, allowing second-by-second analysis of the exact toolchains invoked by each agent.

---

## 💾 Hybrid Storage Strategy (Polyglot Persistence)
The system separates transactional runtime load from heavy analytical data streams.

### 1. OLTP Context & State: PostgreSQL + pgvector
* Used for storing document structures, metadata, user sessions, chat histories, and versioned snapshots of agent states between asynchronous execution iterations.
* The `pgvector` extension is used for vector similarity search when retrieving semantic context.
* Database interaction is fully driven by Entity Framework Core on .NET 10.

### 2. OLAP Text Stream & Thought Logs: ClickHouse
* Collects and aggregates distributed system logs across the entire multi-agent runtime via Serilog.
* Functions as a persistent analytical storage for **Thought Logs** (granular LLM reasoning traces). Every step of the agent loop, including the internal monologue, chosen tool arguments, and execution metrics, is streamed into ClickHouse.
* Leverages ClickHouse's built-in vector functions (such as `cosineDistance`) to perform background anomaly detection within agent behavior and automate MAS quality audits without degrading the performance of the primary transactional database.
