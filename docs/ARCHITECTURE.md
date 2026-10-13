# EquipFlow Architecture Document

## 1. Overview
EquipFlow is an AI-powered industrial maintenance copilot implementing the D5 (Industrial Field Maintenance) domain with the T3 (Cost Governor) twist. This document provides a high-level architectural overview of the system using the C4 model, sequence diagrams, data-flow diagrams, and entity-relationship diagrams.

## 2. C4 Model

### 2.1 Level 1: System Context
The System Context diagram shows the high-level interaction between EquipFlow, its users, and external systems.

```mermaid
graph TD
    User[("Maintenance Staff<br/>(Technicians, Engineers, Planners, Supervisors)")]
    EquipFlow["EquipFlow Copilot"]
    LLM["LLM Providers<br/>(OpenAI, Ollama)"]
    DB[("PostgreSQL + pgvector<br/>(Relational & Vector Data)")]

    User -->|Interacts via REST API| EquipFlow
    EquipFlow -->|API Calls| LLM
    EquipFlow -->|Queries & Stores| DB
```

### 2.2 Level 2: Container
The Container diagram shows the high-level technical building blocks of the system.

```mermaid
graph TD
    subgraph "EquipFlow System"
        WebApi["Web API<br/>(ASP.NET Core Minimal APIs)"]
        App["Application Layer<br/>(CQRS, MediatR, Orchestrator)"]
        Domain["Domain Layer<br/>(Entities, Value Objects)"]
        Infra["Infrastructure Layer<br/>(EF Core, Adapters)"]
    end
    
    DB[("PostgreSQL + pgvector")]
    LLM["External LLM APIs"]

    WebApi -->|Routes & Auth| App
    App -->|Business Rules| Domain
    App -->|Ports & Interfaces| Infra
    Infra -->|EF Core & SDKs| DB
    Infra -->|HTTP Clients| LLM
```

### 2.3 Level 3: Component (Agentic Coordination Layer)
The Component diagram details the internal structure of the Agentic Coordination Layer within the Application/Infrastructure layers.

```mermaid
graph TD
    subgraph "Application Layer"
        Orchestrator["Sequential Supervisor Orchestrator"]
        CostGov["Cost Governor Service"]
        SymptomAgent["Symptom Matcher Agent"]
        PlannerAgent["Diagnostic Safety Planner Agent"]
        WOAgent["Work Order Generator Agent"]
    end

    subgraph "Infrastructure Layer"
        ToolDispatcher["Tool Dispatcher Chain<br/>(Validating -> Authorizing -> Core)"]
        RAGPipeline["Hybrid RAG Pipeline<br/>(Vector + Keyword + RRF)"]
    end

    Orchestrator -->|Pre-flight check| CostGov
    Orchestrator -->|Execute Step| SymptomAgent
    Orchestrator -->|Execute Step| PlannerAgent
    Orchestrator -->|Execute Step| WOAgent
    
    SymptomAgent -->|Invoke Tools| ToolDispatcher
    PlannerAgent -->|Invoke Tools| ToolDispatcher
    WOAgent -->|Invoke Tools| ToolDispatcher
    
    ToolDispatcher -->|Search| RAGPipeline
```

## 3. Agentic Workflow Sequence
This sequence diagram illustrates the multi-agent orchestration process, including the Cost Governor pre-flight checks, tool execution, and the mandatory human approval gate.

```mermaid
sequenceDiagram
    actor User
    participant API as Web API
    participant Orch as Orchestrator
    participant CG as Cost Governor
    participant Agent1 as Symptom Matcher
    participant Agent2 as Safety Planner
    participant Agent3 as WO Generator
    participant DB as Database

    User->>API: POST /runs (Symptom Description)
    API->>Orch: Start Run
    Orch->>CG: Pre-flight Token Estimate
    CG-->>Orch: Reservation OK
    
    Orch->>Agent1: Execute (SearchManuals)
    Agent1-->>Orch: Ranked Faults + Citations
    
    Orch->>Agent2: Execute (GetEquipmentSpecs)
    Agent2-->>Orch: Diagnostic Plan + Safety Prerequisites
    
    Orch->>Agent3: Execute (DraftWorkOrder)
    Agent3-->>Orch: Work Order Draft
    
    Orch-->>API: Run Completed (Draft)
    API-->>User: Work Order Draft + Citations
    
    Note over User,DB: Human-in-the-Loop Approval Gate
    User->>API: POST /work-orders/{id}/approve
    API->>DB: Dispatch Work Order
```

## 4. Data-Flow & Trust Boundaries
This diagram highlights the data flow through the system and the trust boundaries enforced by Clean Architecture and security middleware.

```mermaid
graph LR
    subgraph "Public Internet (Untrusted)"
        Client[Client App]
    end

    subgraph "Trust Boundary: Edge"
        Auth[JWT Auth Middleware]
    end

    subgraph "Internal Network (Trusted)"
        App[Application Layer]
        Domain[Domain Layer]
    end

    subgraph "Trust Boundary: External SaaS"
        LLM[LLM Provider]
    end

    subgraph "Trust Boundary: Data Store"
        DB[(PostgreSQL)]
    end

    Client -->|HTTPS + JWT| Auth
    Auth -->|Authenticated Context| App
    App -->|Pure Business Rules| Domain
    App -->|LLM Prompts| LLM
    App -->|SQL + pgvector| DB
```

## 5. Entity-Relationship (ER) Diagram
The ER diagram illustrates the core domain entities and their relationships as defined in the EF Core DbContext.

```mermaid
erDiagram
    EQUIPMENT ||--o{ WORK_ORDER : "has"
    WORK_ORDER ||--o{ SAFETY_PREREQUISITE : "requires"
    WORK_ORDER ||--o{ APPROVAL_ACTION : "audited by"
    
    USER ||--|| USER_BUDGET : "owns"
    USER ||--o{ BUDGET_INCREASE_REQUEST : "requests"
    USER ||--o{ WORK_ORDER : "creates"
    
    DOCUMENT ||--o{ DOCUMENT_CHUNK : "contains"
    
    USER ||--o{ CONVERSATION : "participates in"
    CONVERSATION ||--o{ CONVERSATION_MESSAGE : "has"
    
    USER_BUDGET ||--o{ RUN_SPEND : "tracks"

    EQUIPMENT {
        guid id PK
        string name
        string serial_number
    }
    WORK_ORDER {
        guid id PK
        guid equipment_id FK
        string title
        string status
    }
    USER_BUDGET {
        guid id PK
        guid user_id FK
        decimal total_limit
        decimal consumed_amount
    }
```

## 6. Layer-Dependency Diagram (Clean Architecture)
EquipFlow strictly adheres to Clean Architecture principles. Dependencies only point inwards towards the Domain.

```mermaid
graph TD
    WebApi["WebApi<br/>(Composition Root, Minimal APIs)"]
    Infra["Infrastructure<br/>(EF Core, LLM Adapters, Search)"]
    App["Application<br/>(CQRS, Ports, Orchestration)"]
    Domain["Domain<br/>(Entities, Value Objects, Rules)"]

    WebApi -->|Depends on| App
    WebApi -->|Depends on| Infra
    Infra -->|Depends on| App
    Infra -->|Depends on| Domain
    App -->|Depends on| Domain
    
    Domain -.->|ZERO Dependencies| None((None))
```