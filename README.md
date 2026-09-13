# EquipFlow — Industrial Maintenance Copilot (D5T3)

> **ITI Technical Instructor Assessment**
> **Assigned Variant:** D5T3 (Domain: Industrial Field Maintenance | Twist: Cost Governor)

An AI-powered equipment maintenance assistant for EquipTech Manufacturing. It ingests technical documentation, answers questions with verifiable citations, and executes a multi-step diagnostic workflow through a team of specialised AI agents — while a human Supervisor approves anything consequential and a Cost Governor enforces per-user token budgets.

---

## 🎯 Variant Derivation
- **Domain:**  **D5** (Industrial Field Maintenance)
- **Twist:** **T3** (Cost Governor)

---

## 🏗️ Architecture

EquipFlow follows **Clean Architecture** with strict layer separation and CQRS at the Application layer.

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

**Non-negotiable rule:** The Domain and Application layers have zero dependencies on any LLM SDK, vector-store SDK, or web framework. Swapping the LLM provider requires configuration plus one adapter — not changes to business logic.

---

## 🤖 LLM Provider Abstraction

The system implements a provider-agnostic LLM abstraction (`ILLMGenerationPort`) with three working implementations, selectable by configuration with a documented fallback chain.

| Provider | Completion | Streaming | Tool Calling | Purpose |
|----------|:----------:|:---------:|:------------:|---------|
| **OpenAI** | ✅ | ✅ | 🔜 | Primary hosted API provider |
| **Ollama** | ✅ | ✅ | 🔜 | Local/alternative provider (free, offline-capable) |
| **Mock** | ✅ | ✅ | — | Deterministic testing without live LLM calls |

### Provider Selection & Fallback

Providers are registered as **.NET Keyed Services** and resolved dynamically via `ILLMProviderFactory`. The Cost Governor uses this factory for budget-aware routing:

1. **Pre-flight estimation** → check user budget
2. **Under budget** → route to primary provider (OpenAI)
3. **Over budget / failure** → fallback cascade:
   - Try cheaper/local provider (Ollama)
   - Try cached semantic match
   - Structured refusal with escalation options

### Configuration

```json
{
  "LLM": {
    "OpenAI": {
      "ApiKey": "your-api-key-here",
      "Model": "gpt-4o-mini",
      "Endpoint": null
    },
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "Model": "llama3.1"
    }
  }
}
```

> **Note:** API keys should be stored in User Secrets or environment variables, never in `appsettings.json`.

---

## 📋 Current Implementation Status

| Capability | Status | Notes |
|---|---|---|
| Clean Architecture + CQRS | ✅ Implemented | Strict layer separation |
| Work Order Lifecycle | ✅ Implemented | Draft → PendingApproval → Approved/Rejected → Dispatched |
| Safety Prerequisites | ✅ Implemented | Structural enforcement — unresolved prerequisites block dispatch |
| Approval Gate | ✅ Implemented | Supervisor approve/reject/edit-and-approve, fully audited |
| Cost Governor Core | ✅ Implemented | Per-user budgets, pre-flight estimation, hard cut-off |
| RAG Foundation | ✅ Implemented | Document ingestion, chunking, embeddings, pgvector |
| RAG Retrieval | ✅ Implemented | Hybrid search (dense + keyword), RRF fusion, citations |
| LLM Provider Abstraction | ✅ Implemented | OpenAI + Ollama + Mock, streaming support |
| Provider Factory | ✅ Implemented | Keyed service resolution for budget-aware routing |
| Multi-Agent Workflow | 🔜 In Progress | Symptom Matcher · Diagnostic & Safety Planner · Work Order Generator |
| Tool Calling | 🔜 Planned | Structured tool invocation via LLM adapters |
| Evaluation Harness | 🔜 Planned | Golden set, adversarial cases, metrics |

---

## 🚀 Quick Start

*(Coming soon: Docker Compose setup, seed instructions, and 5-minute demo path)*

### Prerequisites

- Docker & Docker Compose
- .NET 10 SDK (for local development)
- PostgreSQL 18 with pgvector extension (included in Docker)

### Environment Variables

```bash
# .env.example (coming soon)
# OpenAI API Key (optional — system falls back to Ollama if not provided)
LLM__OpenAI__ApiKey=your-key-here
LLM__OpenAI__Model=gpt-4o-mini

# Ollama (for local/offline mode)
LLM__Ollama__BaseUrl=http://localhost:11434
LLM__Ollama__Model=llama3.1
```

---

## 📚 Documentation

- [Business Requirements Document (BRD)](docs/BRD.md)
- [System Design Document](docs/SYSTEM-DESIGN.md)
- [Architecture & ADRs](docs/ARCHITECTURE.md)
- [Security Controls](docs/SECURITY.md) *(coming soon)*
- [Evaluation Report](docs/EVALUATION.md) *(coming soon)*
- [Agentic Workflow](docs/AGENTIC-WORKFLOW.md) *(coming soon)*
- [AI Usage Log](docs/AI-USAGE-LOG.md) *(coming soon)*

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
| Telemetry | OpenTelemetry | latest |
| Testing | xUnit + NSubstitute + Testcontainers | latest |

---

## 📄 License

MIT License — see [LICENSE](LICENSE) for details.

