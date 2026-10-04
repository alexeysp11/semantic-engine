# ✨ Agentic Workflow: Automated Code Analysis & Contract Design

This document details the step-by-step asynchronous orchestration process for analyzing the `sqlviewer` microservice architecture and generating distributed data synchronization contracts.

---

## 🔀 Step-by-Step Execution Lifecycle

### 1. Ingestion & Trigger Phase
* **User Action:** The user updates the local `task_session.xml` with a new engineering requirement for the `sqlviewer` system and submits it via `Semantic.Engine.Cli`.
* **API Routing:** The CLI app streams the payload to `Semantic.Engine.Gateway`, which validates the secure token and pushes a `StartAgentSessionCommand` into the RabbitMQ bus.
* **Saga Activation:** The `Semantic.Engine.Orchestrator` consumes the command, spins up a new MassTransit State Machine instance, and transitions into the `Active` state.

### 2. Multi-Step Execution & Decomposition Loop

#### 🔍 Step A: `ReadFile` (Ingesting Requirements)
* **Action:** The Orchestrator requests the initial specification parsing.
* **Worker Execution:** `Distributed Tool Execution Worker` reads the local XML file, extracts the text under the `<instruction>` block, and passes it to the `Reasoning Service`.
* **Memory Registration:** The `Reasoning Service` generates a conceptual summary of the task and fires a `MemorizeEvent` to the `Data & Embedding Worker`, which vectorizes and stores it into the PostgreSQL `pgvector` store.

#### 📁 Step B: `AnalyzeProject` (AST Architecture Scanning)
* **Action:** To prevent token window saturation and CPU thrashing, the system bypasses reading files line-by-line individually.
* **Worker Execution:** `Distributed Tool Execution Worker` runs a native CLI utility to scan directory structures and generate an abstract syntax snapshot of the `sqlviewer` solution (mapping gRPC proto files, Kafka consumer namespaces, and database adapters).
* **Memory Registration:** The resulting architectural landscape is compressed, passed to the LLM for high-level summarization, and pushed to the `Data Worker` for semantic indexing via `all-minilm:l6-v2`.

#### 🏗️ Step C: `ConstructDataStructures` (DTO & Schema Design)
* **Action:** Relying on the vectorized business task summary, the `Reasoning Service` tasks `qwen2.5-coder:7b` with designing Data Transfer Objects for cross-database schema changes.
* **Output:** The model generates strict C# record models or gRPC schema definitions, storing them instantly into the long-term vector state database.

#### 🗂️ Step D: `FindNamespaces` & `FindSimilarClasses` (Target Injection Analysis)
* **Action:** The Orchestrator looks up indexed project metadata to map out where the newly created data structures belong.
* **Verification:** The model evaluates existing database migration schemas to check for naming collisions (e.g., matching a new `SchemaSynchronizedEvent` with older database projection types).

---

## 🛑 Collision Resolution & Human-in-the-Loop Interruption

* **Collision Detected:** If the model discovers duplicate data structures or conflicting gRPC namespaces within `sqlviewer`, the Orchestrator pauses the MassTransit Saga loop and moves to an `AwaitingHumanInput` state.
* **User Notification:** An alert payload is published to RabbitMQ. The Gateway intercepts this and streams a real-time question to the `Semantic.Engine.Cli` app via Server-Sent Events (SSE).
* **User Response:** The developer selects the preferred target namespace or resolves the naming layout directly inside the terminal UI. The response goes back up the pipe, waking up the Saga instance.

---

## 🎯 Final Compilation & Targeted Writing

* **Context Aggregation:** The Orchestrator queries the `Data & Embedding Worker` to pull down the final verified schemas and exact target namespace paths.
* **Target Mapping:** `Distributed Tool Worker` reads the specified destination configuration file, prompts the LLM to locate the exact destination index (e.g., `"at line 45"`), and executes an atomic patch update, inserting the compiled C# contracts.
* **Completion State:** The system runs a validation check (`dotnet build`), fires a `FinalArtifactGeneratedEvent`, and closes the session.

---

## 📄 Task Session Specification Schema

The agent lifecycle is driven strictly by a structured declarative XML state document. This file acts as the persistent context anchor, encapsulating metadata boundaries, deep behavioral instructions, structural output inject targets, and dynamic infrastructure execution traces.

```xml
<?xml version="1.0" encoding="utf-8"?>
<task_session id="sqlviewer-metadata-sync">
    <meta>
        <project_context>Semantic.Engine.Target:alexeysp11/sqlviewer</project_context>
        <target_runtime>.NET 10 Microservice Stack</target_runtime>
        <transport_layer>Apache Kafka &amp; gRPC</transport_layer>
        <primary_storage>PostgreSQL / Redis</primary_storage>
    </meta>

    <instruction>
        Analyze the existing heterogeneous database management solution. 
        Design a set of distributed contracts to synchronize database schema modifications 
        (tables, columns, data types) discovered across different instances.
        The data structures must be designed as immutable C# records, compatible with MassTransit serialization,
        and must be cleanly placed under the correct infrastructure or shared contract namespace.
    </instruction>

    <!-- Runtime execution tracking updated dynamically by the Distributed Tool Worker -->
    <execution_log>
        <step status="Completed" timestamp="2026-10-04T22:30:00Z">
            <name>ReadFile</name>
            <message>Successfully ingested raw XML instruction block.</message>
        </step>
        <step status="Completed" timestamp="2026-10-04T22:31:15Z">
            <name>AnalyzeProject</name>
            <message>AST generation complete. Scanned sqlviewer solution, indexed gRPC projects and Kafka adapters.</message>
        </step>
        <step status="AwaitingVerification" timestamp="2026-10-04T22:32:00Z">
            <name>ConstructDataStructures</name>
            <message>Proposed SchemaSynchronizedEvent record layout created. Awaiting namespace placement validation.</message>
        </step>
    </execution_log>

    <!-- Defines the precise location where the Agent must write the final refactored C# artifact -->
    <output_target>
        <file_path>./src/SqlViewer.Shared/Contracts/SchemaContracts.cs</file_path>
        <target_line>42</target_line>
        <injection_mode>AtomicInsert</injection_mode>
    </output_target>
</task_session>
```
