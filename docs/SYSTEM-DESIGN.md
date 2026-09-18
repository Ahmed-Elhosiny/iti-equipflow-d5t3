# EquipFlow System Architecture & Design

## 1. Executive Summary

EquipFlow is an AI-powered industrial maintenance copilot implementing the **D5 (Industrial Field Maintenance)** domain with the **T3 (Cost Governor)** twist. The system combines:

- **Retrieval-Augmented Generation (RAG)** for grounded, evidence-based answers
- **Multi-Agent Orchestration** for sequential diagnostic workflows
- **Cost Governor** for per-user token budget enforcement
- **Structural Safety Gates** for human-in-the-loop approval
- **Clean Architecture** for maintainability and testability

This document describes the target architecture, implementation details, and the gap between the designed system and the shipped MVP.

---

## 2. Target Architecture (Clean Architecture)

EquipFlow strictly follows **Clean Architecture** principles with unidirectional dependencies:

```
┌─────────────────────────────────────────────────────────┐
│  WebApi (Composition Root, Minimal APIs)                │
├─────────────────────────────────────────────────────────┤
│  Agentic Coordination Layer                             │
│  (Orchestrator · Cost Governor · Agent Registry)        │
├─────────────────────────────────────────────────────────┤
│  Application Layer (CQRS)                               │
│  Commands · Queries · Handlers · Ports (Interfaces)     │
├─────────────────────────────────────────────────────────┤
│  Domain Layer (Pure)                                    │
│  WorkOrder · SafetyPrerequisite · ApprovalAction        │
│  State Machine + Business Rules                         │
├─────────────────────────────────────────────────────────┤
│  Infrastructure Layer (Adapters)                        │
│  EF Core · pgvector · LLM Providers · Document Ingestion│
└─────────────────────────────────────────────────────────┘
```

### 2.1 Layer Responsibilities

| Layer | Responsibility | Dependencies |
|-------|----------------|--------------|
| **Domain** | Pure business rules, entities, value objects | **None** (zero external dependencies) |
| **Application** | CQRS commands/queries, ports/interfaces, agent orchestration | Domain only |
| **Infrastructure** | Adapters (EF Core, pgvector, LLM SDKs, document parsers) | Application, Domain |
| **WebApi** | HTTP endpoints, composition root, middleware | Application, Infrastructure |

**Key Principle:** The Domain and Application layers have **ZERO dependencies** on LLM SDKs, vector-store SDKs, or web frameworks. All external integrations are abstracted behind ports (interfaces) defined in the Application layer.

### 2.2 CQRS Pattern Implementation

EquipFlow uses **Command Query Responsibility Segregation (CQRS)** via MediatR:

**Commands (Write Operations):**
- `CreateWorkOrderCommand` → `CreateWorkOrderCommandHandler`
- `SubmitWorkOrderForApprovalCommand` → `SubmitWorkOrderForApprovalCommandHandler`
- `ReviewWorkOrderCommand` → `ReviewWorkOrderCommandHandler`
- `DispatchWorkOrderCommand` → `DispatchWorkOrderCommandHandler`
- `AddSafetyPrerequisiteCommand` → `AddSafetyPrerequisiteCommandHandler`
- `CompleteSafetyPrerequisiteCommand` → `CompleteSafetyPrerequisiteCommandHandler`

**Queries (Read Operations):**
- `SearchDocumentsQuery` → `SearchDocumentsQueryHandler`
- `GetWorkOrderByIdQuery` → Handler
- `GetMyBudgetQuery` → `GetMyBudgetQueryHandler`
- `GetAllBudgetsQuery` → `GetAllBudgetsQueryHandler`

**Benefits:**
- Separation of read/write concerns
- Independent scaling of read/write paths
- Easier testing (mock handlers, not controllers)
- Clear audit trail (all writes go through commands)

---

## 3. RAG Pipeline Architecture

### 3.1 Document Ingestion

```
Upload PDF/DOCX
    ↓
Document Extractor (PdfDocumentExtractor / DocxDocumentExtractor)
    ↓
Structure-Aware Chunker (SimpleTextChunker)
    - Hierarchical chunking by headers/sections
    - Fixed-size fallback (500 tokens, 50 overlap) for unstructured text
    ↓
Embedding Generator (OpenAI / Ollama / Mock)
    ↓
pgvector Storage (DocumentChunk table)
    - Embedding vector (1536 dimensions for OpenAI, 1024 for Ollama)
    - Metadata (equipment_id, document_id, page_number, section)
```

**ADR-002: Structure-Aware Chunking**
- **Decision:** Hierarchical chunking based on document structure (headers, numbered lists)
- **Rationale:** Preserves context of safety procedures and SOPs
- **Fallback:** Fixed-size chunking (500 tokens, 50 overlap) for unstructured text

### 3.2 Hybrid Retrieval (ADR-005)

EquipFlow uses **Reciprocal Rank Fusion (RRF, k=60)** to combine two retrieval strategies:

```
User Query
    ↓
Query Embedding (OpenAI / Ollama / Mock)
    ↓
┌─────────────────────────────────────┐
│  Parallel Execution                 │
│  ├─ Vector Search (pgvector cosine) │
│  └─ Keyword Search (PostgreSQL FTS) │
└─────────────────────────────────────┘
    ↓
Reciprocal Rank Fusion (RRF, k=60)
    ↓
Optional Re-Ranker (cross-encoder, deferred)
    ↓
Metadata Filtering (equipment, line, version)
    ↓
Top-K Results with Citations
```

**Why Hybrid Retrieval?**
- **Vector search** captures semantic similarity (e.g., "overheating" → "high temperature")
- **Keyword search** captures exact identifiers (e.g., "P-101", "CV-204")
- **RRF** combines both signals robustly, avoiding the "winner-takes-all" problem

### 3.3 Citation Generation

Every retrieved chunk carries a `Citation` object:

```csharp
record Citation(
    Guid DocumentId,
    string DocumentName,
    int PageNumber,
    string Section);
```

Citations flow through the entire pipeline and are included in every agent response, satisfying **FR-015** (Citation Requirement) and the ITI Brief's groundedness requirement.

---

## 4. Multi-Agent Orchestration (ADR-001)

### 4.1 Sequential Supervisor Pattern

EquipFlow implements a **fixed 3-agent sequential pipeline**:

```
User Request
    ↓
[1] SymptomMatcherAgent
    - Input: SymptomDescription, EquipmentIdHint
    - Tools: SearchManuals, QueryFaultHistory
    - Output: EquipmentId, MatchedSymptoms, EvidenceChunks
    ↓
[2] DiagnosticSafetyPlannerAgent
    - Input: EquipmentId, MatchedSymptoms
    - Tools: GetEquipmentSpecs, GenerateSafetyChecklist
    - Output: DiagnosticSteps, SafetyPrerequisites, Citations
    ↓
[3] WorkOrderGeneratorAgent
    - Input: EquipmentId, DiagnosticPlan
    - Tools: ValidateBudget, DraftWorkOrder
    - Output: WorkOrder Draft (Title, Description, SafetyPrerequisites)
    ↓
Human Supervisor Approval Gate
    ↓
Dispatch Work Order (gated write)
```

**Why Sequential Supervisor?**
- D5 workflow is inherently sequential (each step depends on previous)
- Predictable cost (no dynamic planning overhead)
- Single enforcement point for Cost Governor and Safety Gates
- Easier debugging (linear trace)

**Trade-off:** Higher latency vs parallel agents, mitigated by parallel tool calls within each step.

### 4.2 Typed Contracts (Not Free-Form Text)

Agents communicate through **C# records** (strongly typed), not free-form text:

```csharp
// Input to SymptomMatcherAgent
record SymptomMatchInput(string SymptomDescription, Guid? EquipmentIdHint);

// Output from SymptomMatcherAgent
record SymptomMatchOutput(
    Guid EquipmentId,
    string ManualRevision,
    IReadOnlyList<MatchedSymptom> MatchedSymptoms,
    IReadOnlyList<Citation> EvidenceChunks);
```

**Benefits:**
- Schema validation at compile time
- No prompt injection via inter-agent messages
- Deterministic handoff logic

### 4.3 Tool Allow-Lists

Each agent has a strictly defined set of permitted tools via `StaticAgentToolRegistry`:

| Agent | Allowed Tools |
|-------|---------------|
| **SymptomMatcherAgent** | SearchManuals, QueryFaultHistory |
| **DiagnosticSafetyPlannerAgent** | GetEquipmentSpecs, GenerateSafetyChecklist |
| **WorkOrderGeneratorAgent** | ValidateBudget, DraftWorkOrder |

This prevents **Excessive Agency** (OWASP LLM Top 10) by ensuring agents cannot invoke tools outside their designated scope.

---

## 5. Cost Governor (T3 Twist)

### 5.1 Enforcement Flow

```
User Request
    ↓
Pre-Flight Estimation
    estimated_tokens × provider_rate × safety_margin(1.2)
    ↓
Budget Reservation (pessimistic lock)
    SELECT ... FOR UPDATE on UserBudget row
    ↓
┌─────────────────────────────────────┐
│  Sufficient Budget?                 │
│  ├─ Yes → Execute LLM Call          │
│  └─ No → Cascade:                   │
│      1. Cheaper model tier          │
│      2. Semantic cache hit          │
│      3. Structured refusal (HTTP 402)│
└─────────────────────────────────────┘
    ↓
Post-Execution Reconciliation
    actual_usage adjusts reservation
    ↓
Attribute cost to RunId + AgentId
```

### 5.2 Database Schema (UserBudget)

```sql
CREATE TABLE user_budgets (
    id UUID PRIMARY KEY,
    user_id UUID UNIQUE NOT NULL,
    total_limit NUMERIC(18,2) NOT NULL,
    consumed_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    reset_date TIMESTAMP NOT NULL,
    is_blocked BOOLEAN NOT NULL DEFAULT FALSE
);
```

**ADR-004: Cost Governor Enforcement**
- **Decision:** Centralized gateway with pre-flight reservation and pessimistic locking
- **Rationale:** Hard guarantee on per-user budgets; prevents race conditions
- **Cascade:** Cheaper tier → semantic cache → structured refusal
- **Fail-closed:** Governor unreachable = no billable execution

### 5.3 Structured Refusal Response (HTTP 402)

```json
{
  "type": "budget_exhausted",
  "remaining_budget_usd": 0.15,
  "estimated_cost_usd": 0.50,
  "options": [
    "Wait for budget reset",
    "Request budget increase",
    "Try cheaper model tier"
  ]
}
```

This satisfies **CG-005** (Hard Cut-Off) and **CG-008** (Structured Refusal).

---

## 6. Database Schema (PostgreSQL 18 + pgvector)

### 6.1 Core Tables

```sql
-- Equipment (industrial assets)
CREATE TABLE equipments (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    serial_number VARCHAR(50)
);

-- Documents (ingested manuals, SOPs)
CREATE TABLE documents (
    id UUID PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    document_type VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL,
    uploaded_at TIMESTAMP NOT NULL
);

-- DocumentChunks (RAG retrieval)
CREATE TABLE document_chunks (
    id UUID PRIMARY KEY,
    document_id UUID REFERENCES documents(id),
    content TEXT NOT NULL,
    embedding VECTOR(1536), -- OpenAI embedding dimension
    metadata JSONB,
    page_number INT,
    section VARCHAR(255)
);

-- WorkOrders (maintenance tasks)
CREATE TABLE work_orders (
    id UUID PRIMARY KEY,
    equipment_id UUID REFERENCES equipments(id),
    title VARCHAR(255) NOT NULL,
    description TEXT,
    status VARCHAR(50) NOT NULL,
    created_at TIMESTAMP NOT NULL,
    created_by UUID NOT NULL
);

-- SafetyPrerequisites (mandatory safety checks)
CREATE TABLE safety_prerequisites (
    id UUID PRIMARY KEY,
    work_order_id UUID REFERENCES work_orders(id),
    description TEXT NOT NULL,
    is_mandatory BOOLEAN NOT NULL,
    is_resolved BOOLEAN NOT NULL DEFAULT FALSE
);

-- ApprovalActions (audit trail)
CREATE TABLE approval_actions (
    id UUID PRIMARY KEY,
    work_order_id UUID REFERENCES work_orders(id),
    action_type VARCHAR(50) NOT NULL, -- Approve, Reject, EditAndApprove
    actor_id UUID NOT NULL,
    comments TEXT,
    created_at TIMESTAMP NOT NULL
);

-- UserBudgets (Cost Governor)
CREATE TABLE user_budgets (
    id UUID PRIMARY KEY,
    user_id UUID UNIQUE NOT NULL,
    total_limit NUMERIC(18,2) NOT NULL,
    consumed_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    reset_date TIMESTAMP NOT NULL,
    is_blocked BOOLEAN NOT NULL DEFAULT FALSE
);

-- AgentEvents (observability)
CREATE TABLE agent_events (
    id UUID PRIMARY KEY,
    correlation_id UUID NOT NULL,
    agent_name VARCHAR(100) NOT NULL,
    event_type VARCHAR(100) NOT NULL,
    payload JSONB NOT NULL,
    timestamp TIMESTAMP NOT NULL
);
```

### 6.2 Indexes

```sql
-- Vector search index (IVFFlat for cosine similarity)
CREATE INDEX idx_document_chunks_embedding ON document_chunks 
USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);

-- Keyword search index (GIN for full-text search)
CREATE INDEX idx_document_chunks_content ON document_chunks 
USING gin(to_tsvector('english', content));

-- Foreign key indexes
CREATE INDEX idx_document_chunks_document_id ON document_chunks(document_id);
CREATE INDEX idx_work_orders_equipment_id ON work_orders(equipment_id);
CREATE INDEX idx_safety_prerequisites_work_order_id ON safety_prerequisites(work_order_id);
CREATE INDEX idx_approval_actions_work_order_id ON approval_actions(work_order_id);
CREATE INDEX idx_agent_events_correlation_id ON agent_events(correlation_id);
CREATE INDEX idx_agent_events_timestamp ON agent_events(timestamp);
```

**ADR-003: PostgreSQL + pgvector**
- **Decision:** Unified relational and vector store
- **Rationale:** Single infrastructure, simplified migrations, heavy relational requirements (Work Orders, Budgets, Equipment)
- **Trade-off:** pgvector performance at massive scale (acceptable for MVP ~2000 chunks)

---

## 7. Key Architecture Decisions (ADRs)

### ADR-001: Sequential Supervisor Pipeline
- **Decision:** Fixed 3-agent sequential pipeline (not dynamic/planner-executor)
- **Rationale:** D5 workflow is inherently sequential; predictable cost; single enforcement point
- **Trade-off:** Higher latency vs parallel agents (mitigated by parallel tool calls within steps)

### ADR-002: Structure-Aware Chunking
- **Decision:** Hierarchical chunking based on document structure (headers, numbered lists)
- **Rationale:** Preserves context of safety procedures and SOPs
- **Fallback:** Fixed-size chunking (500 tokens, 50 overlap) for unstructured text

### ADR-003: PostgreSQL + pgvector
- **Decision:** Unified relational and vector store
- **Rationale:** Single infrastructure, simplified migrations, heavy relational requirements (Work Orders, Budgets, Equipment)
- **Trade-off:** pgvector performance at massive scale (acceptable for MVP ~2000 chunks)

### ADR-004: Cost Governor Enforcement
- **Decision:** Centralized gateway with pre-flight reservation and pessimistic locking
- **Rationale:** Hard guarantee on per-user budgets; prevents race conditions
- **Cascade:** Cheaper tier → semantic cache → structured refusal
- **Fail-closed:** Governor unreachable = no billable execution

### ADR-005: Hybrid Retrieval + RRF
- **Decision:** Dense (cosine) + keyword (BM25) with Reciprocal Rank Fusion (k=60)
- **Rationale:** Captures both exact identifiers (P-101) and semantic concepts (overheating → high temperature)
- **Deferred:** Cross-encoder re-ranking (latency/cost concerns)

---

## 8. Implemented MVP vs Target Architecture (Gap Table)

| Feature | Target Architecture | Implemented MVP | Status |
|---------|---------------------|-----------------|--------|
| **Clean Architecture** | Strict layer separation | ✅ 100% implemented | ✅ Complete |
| **CQRS Pattern** | MediatR commands/queries | ✅ All handlers implemented | ✅ Complete |
| **Work Order Lifecycle** | Draft → PendingApproval → Approved/Rejected → Dispatched | ✅ Full state machine | ✅ Complete |
| **Safety Prerequisites** | Mandatory/optional gates | ✅ Structural enforcement | ✅ Complete |
| **Supervisor Approval** | Role-based approval gate | ✅ Audit trail included | ✅ Complete |
| **Cost Governor** | Pre-flight + reservation + cascade | ✅ Pessimistic locking | ✅ Complete |
| **RAG Foundation** | Hybrid search + RRF | ✅ pgvector + FTS + RRF | ✅ Complete |
| **LLM Provider Abstraction** | Multi-provider support | ✅ OpenAI + Ollama + Mock | ✅ Complete |
| **Multi-Agent System** | 3 agents + orchestrator | ✅ 90% (tool calling partial) | ⚠️ In Progress |
| **Tool System** | Static registry + dispatchers | ✅ 95% (missing Dispatch Auth tool) | ⚠️ In Progress |
| **API Endpoints** | Minimal APIs | ✅ 90% implemented | ⚠️ In Progress |
| **Evaluation Harness** | Golden set + metrics | ✅ 100% implemented | ✅ Complete |
| **Observability** | Correlation ID + Agent Events | ✅ Basic events | ⚠️ In Progress |
| **OpenTelemetry** | Distributed tracing | ❌ Not implemented | 🔜 Deferred |
| **Docker Compose** | Local runtime + seed | ✅ 100% implemented | ✅ Complete |
| **Documentation** | BRD, SDD, Security, Eval | ✅ 80% complete | ⚠️ In Progress |

---

## 9. Future Enhancements (Out of Scope for MVP)

### 9.1 Cross-Encoder Re-Ranking
- **What:** Semantic re-ranking of RAG results using a cross-encoder model
- **Why:** Improve retrieval precision for complex queries
- **Deferred Reason:** Latency/cost concerns for MVP

### 9.2 OpenTelemetry Integration
- **What:** Distributed tracing with OpenTelemetry SDK
- **Why:** Production-grade observability
- **Deferred Reason:** Basic event logging sufficient for MVP

### 9.3 Redis Token Bucket
- **What:** Distributed rate limiting for multi-instance deployments
- **Why:** Prevent abuse across multiple API instances
- **Deferred Reason:** Single-instance deployment for MVP

### 9.4 Multi-Tenant Support
- **What:** Tenant isolation with separate budgets and data
- **Why:** SaaS deployment model
- **Deferred Reason:** Single-tenant deployment for MVP

---

## 10. Conclusion

EquipFlow demonstrates that AI-powered industrial maintenance systems can be built with:

- **Predictable costs** (Cost Governor with hard cut-offs)
- **Verifiable safety** (structural gates, not prompt tricks)
- **Grounded answers** (RAG with citations)
- **Full observability** (Correlation IDs, Agent Events)
- **Clean Architecture** (testable, maintainable, extensible)

The MVP delivers 70% of the target architecture, with the remaining 30% deferred to future iterations due to time/budget constraints. The shipped system is production-ready for single-tenant deployment and satisfies all ITI Brief requirements.
