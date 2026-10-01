# EquipFlow — Industrial Maintenance Copilot (D5T3)

> **ITI Technical Instructor Assessment**
> **Assigned Variant:** D5T3 (Domain: Industrial Field Maintenance | Twist: Cost Governor)
> **Variant Derivation:** Domain D5 = (National ID last two digits) mod 7 | Twist T3 = (Sum of all National ID digits) mod 8. *(Replace with your actual derivation if required by your specific invitation).*

An AI-powered equipment maintenance assistant for EquipTech Manufacturing. It ingests technical documentation, answers questions with verifiable citations, and executes a multi-step diagnostic workflow through a team of specialised AI agents — while a human Supervisor approves anything consequential and a Cost Governor enforces per-user token budgets.

---

## 🏗️ Architecture

EquipFlow follows **Clean Architecture** with strict layer separation and CQRS at the Application layer.

```text
┌────────────────────────────────────────────────────────┐
│  WebApi (Composition Root, Minimal APIs)               │
├────────────────────────────────────────────────────────┤
│  Agentic Coordination Layer                            │
│  (Orchestrator · Cost Governor · Agent Registry)       │
├────────────────────────────────────────────────────────┤
│  Application Layer (CQRS)                              │
│  Commands · Queries · Handlers · Ports (Interfaces)    │
├────────────────────────────────────────────────────────┤
│  Domain Layer (Pure)                                   │
│  WorkOrder · SafetyPrerequisite · ApprovalAction       │
│  State Machine + Business Rules                        │
├────────────────────────────────────────────────────────┤
│  Infrastructure Layer (Adapters)                       │
│  EF Core · pgvector · LLM Providers · Document Ingestion│
└────────────────────────────────────────────────────────┘
```

**Non-negotiable rule:** The Domain and Application layers have zero dependencies on any LLM SDK, vector-store SDK, or web framework. Swapping the LLM provider requires configuration plus one adapter — not changes to business logic.

---

## 🤖 LLM Provider Abstraction

The system implements a provider-agnostic LLM abstraction (`ILLMGenerationPort`) with three working implementations, selectable by configuration with a documented fallback chain.

| Provider | Completion | Streaming | Tool Calling | Purpose |
|----------|:----------:|:---------:|:------------:|---------|
| **OpenAI** | ✅ | ✅ | ✅ | Primary hosted API provider |
| **Ollama** | ✅ | ✅ | ✅ | Local/alternative provider (free, offline-capable) |
| **Mock** | ✅ | ✅ | — | Deterministic testing without live LLM calls |

### Provider Selection & Fallback

Providers are registered as **.NET Keyed Services** and resolved dynamically via `ILLMProviderFactory`. The Cost Governor uses this factory for budget-aware routing:

1. **Pre-flight estimation** → check user budget using configured model pricing
2. **Under budget** → route to primary provider (OpenAI)
3. **Over budget / failure** → fallback cascade:
   - Try cheaper/local provider (Ollama)
   - Try cached semantic match
   - Structured refusal with escalation options

---

## 📋 Current Implementation Status

| Capability | Status | Notes |
|---|---|---|
| Clean Architecture + CQRS | ✅ Implemented | Strict layer separation, MediatR |
| Work Order Lifecycle | ✅ Implemented | Draft → PendingApproval → Approved/Rejected → Dispatched |
| Safety Prerequisites | ✅ Implemented | Structural enforcement — unresolved prerequisites block dispatch |
| Approval Gate | ✅ Implemented | Supervisor approve/reject/edit-and-approve, fully audited |
| Cost Governor Core | ✅ Implemented | Per-user budgets, config-driven pre-flight, hard cut-off |
| Per-Step Budget Enforcement | ✅ Implemented | Pre-flight and reconciliation before/after *each* agent step (ADR-004) |
| RAG Foundation | ✅ Implemented | Document ingestion, structure-aware chunking (ADR-002), embeddings |
| RAG Retrieval | ✅ Implemented | Hybrid search (dense + keyword), RRF fusion, citations |
| LLM Provider Abstraction | ✅ Implemented | OpenAI + Ollama + Mock, streaming & tool-calling support |
| Multi-Agent Workflow | ✅ Implemented | Symptom Matcher · Diagnostic Planner · Work Order Generator |
| Observability & Attribution | ✅ Implemented | Genuine per-step cost/model attribution in event store (AGENT-DESIGN §8) |
| Evaluation Harness | ✅ Implemented | Golden set (28 cases), retrieval & refusal metrics |
| Docker & Seed | ✅ Implemented | One-command local runtime with baseline data |

---

## 🚀 Quick Start

EquipFlow is fully containerized. You can run the entire stack (API, PostgreSQL 18 + pgvector) and seed baseline data using Docker Compose.

### Prerequisites
- Docker & Docker Compose
- (Optional) Ollama running locally if you want to use the free local LLM tier instead of OpenAI.

### 1. Environment Setup
Copy the example environment file and configure your keys:
```bash
cp .env.example .env
```
*Edit `.env` to add your `OpenAI__ApiKey` if you wish to use the hosted API. If left blank, the system will route to Ollama or the Mock provider based on the Cost Governor cascade.*

### 2. Start the Stack & Seed Data
This command starts the database, applies EF Core migrations, populates 12 equipment instances, 4 user budgets, and the document corpus.
```bash
docker compose up -d
docker compose --profile tools run --rm seed
```

### 3. Access the API
The API is now running at `http://localhost:5000`.
- **Swagger UI:** `http://localhost:5000/swagger`
- **Health Check:** `http://localhost:5000/health`

---

## 🧪 Testing & Evaluation Harness

EquipFlow includes a comprehensive evaluation harness to measure RAG quality, groundedness, and cost compliance against a curated golden test set (28 cases including adversarial and prompt injection).

```bash
# Run all unit and integration tests
dotnet test

# Run the Evaluation Harness specifically (Retrieval Hit-Rate & Refusal Correctness)
dotnet test tests/EquipFlow.WebApi.IntegrationTests --filter "EvaluationHarnessTests"

# Run Cost Governor Compliance Tests
dotnet test tests/EquipFlow.IntegrationTests --filter "CostGovernorEvalTests"
```

---

## 🎬 5-Minute Demo Path

Follow this numbered script to experience every core capability of the D5T3 variant:

1. **Authenticate:** Use the `/api/auth/login` endpoint in Swagger to get a JWT token for a `Technician` (password: `password`).
2. **Ingest a Document:** POST a PDF to `/api/documents` to observe the structure-aware chunking and idempotent ingestion pipeline.
3. **Ask a Grounded Question:** Send a POST request to `/api/ai/chat` with: *"What are the common causes of P-101 overheating?"* Observe the verifiable citations in the response.
4. **Run the Multi-Agent Workflow:** POST to `/api/ai/analyze` with the symptom: *"Pump P-101 is drawing 18% more current than normal and discharge pressure is low."* Observe the 3-agent sequential execution (Symptom → Diagnostic → Work Order).
5. **Act on the Approval Gate:** Take the `WorkOrderId` from the previous step and POST to `/api/workorders/{id}/submit`. Then, log in as a `Supervisor` and POST to `/api/workorders/{id}/approve`.
6. **Test Safety Guardrails (Adversarial):** Try to dispatch the work order *without* completing the mandatory safety prerequisites, or send a prompt injection attempt. Observe the system structurally refuse the request.
7. **Inspect the Trace:** Use the `X-Correlation-Id` from the response headers to query `/api/runs/{runId}` and inspect the step-by-step agent execution, tool invocations, and genuine token costs.
8. **Check the Cost Governor:** Query `/api/cost/spend` to see how the T3 Cost Governor tracked the token usage and enforced the budget.

---

## 📚 Documentation & Teaching Pack

Comprehensive documentation is provided to explain the business context, system design, security controls, and evaluation results:

- **[Business Requirements Document (BRD)](docs/BRD.md)** — Context, personas, objectives, and traceability matrix.
- **[System Design Document](docs/SYSTEM-DESIGN.md)** — Target architecture, implemented MVP, ADRs, and gap table.
- **[Security Controls](docs/SECURITY.md)** — OWASP Web Top 10 and OWASP LLM Top 10 mitigations.
- **[Evaluation Report](docs/EVALUATION.md)** — Golden set baseline metrics, retrieval hit-rate, and refusal correctness.
- **[Agentic Workflow](docs/AGENTIC-WORKFLOW.md)** — Sequential supervisor orchestration, agent contracts, and tool dispatch.
- **[AI Usage Log](docs/AI-USAGE-LOG.md)** — Honest log of AI delegation, verification, and mistakes during development.
- **[Architecture & ADRs](docs/architecture/)** — C4 diagrams and Architecture Decision Records.
- **[Teaching Pack](teaching/)** — 90-minute post-graduate session slides, hands-on lab sheet, and common trainee mistakes.

---

## 🛠️ Tech Stack

| Layer | Technology | Version |
|---|---|---|
| Runtime | .NET | 10 LTS |
| Web Framework | ASP.NET Core Minimal APIs | 10.x |
| ORM | EF Core | 10.x |
| Database | PostgreSQL | 18 |
| Vector Search | pgvector | latest |
| LLM Abstraction | Custom `ILLMGenerationPort` | — |
| Telemetry | OpenTelemetry / Custom Event Store | latest |
| Testing | xUnit + NSubstitute + Testcontainers | latest |

---

## 📄 License

MIT License — see [LICENSE](LICENSE) for details.