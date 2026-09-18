# EquipFlow Agentic Workflow Documentation

## 1. Executive Summary

EquipFlow implements a **Sequential Supervisor Orchestration Pattern** (ADR-001) to execute the D5 (Industrial Field Maintenance) multi-step diagnostic workflow. The system uses three specialized AI agents coordinated by a central orchestrator, with strict boundaries enforced by the Application and Domain layers.

**Core Principle:** The LLM is never the authoritative source of maintenance facts or safety decisions. It acts as a synthesizer over retrieved evidence, while the backend deterministically enforces safety prerequisites, approval gates, and cost controls.

---

## 2. Orchestration Pattern: Sequential Supervisor

### 2.1 Why Sequential Supervisor? (ADR-001)

The D5 Industrial Field Maintenance workflow is inherently sequential:
1. **Symptom Matching** → Identify equipment and probable faults
2. **Diagnostic & Safety Planning** → Generate diagnostic steps and safety prerequisites
3. **Work Order Generation** → Compose a draft work order

**Alternatives Considered:**
- **Dynamic Planner-Executor:** Rejected because D5 workflow is predictable and fixed. Dynamic planning adds latency and cost without business value.
- **Parallel Agents:** Rejected because each step depends on the previous step's output (typed contracts).
- **Single Monolithic Agent:** Rejected because it violates the Single Responsibility Principle and makes tool allow-listing impossible.

**Trade-off:** Higher latency vs parallel execution, mitigated by parallel tool calls within each agent step and aggressive caching of equipment specs.

---

## 3. Agent Specifications

### 3.1 SymptomMatcherAgent

**Role:** Match user-reported symptoms to equipment and historical fault patterns.

**Input Contract:**
```csharp
record SymptomMatchInput(string SymptomDescription, Guid? EquipmentIdHint);
```

**Output Contract:**
```csharp
record SymptomMatchOutput(
    Guid EquipmentId,
    string ManualRevision,
    IReadOnlyList<MatchedSymptom> MatchedSymptoms,
    IReadOnlyList<Citation> EvidenceChunks);
```

**Allowed Tools:**
- `SearchManuals` (RAG retrieval)
- `QueryFaultHistory` (read fault database)

**Termination Condition:**
- Returns ranked faults with citations, OR
- `IsGrounded = false` if insufficient evidence (triggers graceful degradation to plain RAG).

**Failure Behavior:**
- Retrieval outage → Degraded mode (plain RAG answer with "retrieval degraded" warning).

---

### 3.2 DiagnosticSafetyPlannerAgent

**Role:** Generate evidence-based diagnostic sequences and explicit safety prerequisites.

**Input Contract:**
```csharp
record DiagnosticPlanInput(
    Guid EquipmentId,
    string ManualRevision,
    IReadOnlyList<MatchedSymptom> MatchedSymptoms);
```

**Output Contract:**
```csharp
record DiagnosticPlanOutput(
    string DiagnosticStepsMarkdown,
    IReadOnlyList<SafetyPrerequisite> SafetyPrerequisites,
    IReadOnlyList<Citation> Citations);
```

**Allowed Tools:**
- `GetEquipmentSpecs` (read equipment data)
- `GenerateSafetyChecklist` (RAG retrieval for safety procedures)

**Termination Condition:**
- Plan emitted where every factual claim has a citation OR is marked "unsupported".

**Failure Behavior:**
- Schema-validation failure → one retry → Degraded mode.

---

### 3.3 WorkOrderGeneratorAgent

**Role:** Compose a draft work order from validated diagnostic findings.

**Input Contract:**
```csharp
record WorkOrderInput(
    Guid EquipmentId,
    DiagnosticPlanOutput DiagnosticPlan);
```

**Output Contract:**
```csharp
record WorkOrderOutput(
    string Title,
    string Description,
    IReadOnlyList<SafetyPrerequisite> SafetyPrerequisites,
    decimal EstimatedCost);
```

**Allowed Tools:**
- `ValidateBudget` (Cost Governor query)
- `DraftWorkOrder` (compose only, no persistence)

**Termination Condition:**
- Draft DTO returned, OR
- Budget refusal via cascade (HTTP 402 structured refusal).

**Failure Behavior:**
- Budget exhausted → `BudgetExhausted` structured refusal.

---

## 4. Orchestrator Lifecycle

The `SequentialSupervisorOrchestrator` manages the end-to-end workflow:

```
Initialized → PreFlight → SymptomMatching → Planning → Drafting → Completed
                                              ↓
                            Degraded | Refused | BudgetExhausted | Failed | Cancelled
```

### 4.1 Responsibilities

1. **Mint Correlation ID** and emit `AgentRunStarted` event.
2. **Per-step Cost Governor pre-flight** → invoke agent → schema-validate → bounded retry (max 1 retry).
3. **Enforce `MaxIterations`** inside each agent's tool loop (breaker = 3 iterations).
4. **Per-step timeout** (45s) + **global workflow timeout** (120s).
5. **Graceful degradation** to plain RAG with citations on unrecoverable failure.
6. **Close run**: emit `AgentRunCompleted`, reconcile tokens/cost.

### 4.2 Typed Contracts (Not Free-Form Text)

Agents communicate through **C# records** (strongly typed), not free-form text. This ensures:
- Schema validation at compile time
- No prompt injection via inter-agent messages
- Deterministic handoff logic

---

## 5. Tool System Architecture

### 5.1 Static Tool Registry

The `StaticAgentToolRegistry` defines which tools each agent can invoke. This prevents **Excessive Agency** (OWASP LLM Top 10).

**Tool Categories:**
- **Read-only tools:** `SearchManuals`, `QueryFaultHistory`, `GetEquipmentSpecs`, `GenerateSafetyChecklist`, `ValidateBudget`
- **Write tools (gated):** `CreateWorkOrderExecutor` (requires Supervisor approval)
- **Control tools:** `EmitAgentEvent` (observability)

### 5.2 Tool Dispatch Chain

Every tool invocation passes through three dispatchers:

```
Agent Request
    ↓
[1] ValidatingToolDispatcher
    - Schema validation (JSON Schema)
    - Type checking
    ↓
[2] AuthorizingToolDispatcher
    - Role-based access control (RBAC)
    - Agent allow-list check
    - Object-level authorization
    ↓
[3] ToolDispatcher (Core)
    - Execute tool
    - Record invocation in AgentEventStore
    - Return structured result
```

### 5.3 Gated Write Tool

The most consequential action (`DispatchWorkOrder`) is **never** executed by the LLM directly. It requires:
1. Work Order in `Approved` state
2. All mandatory `SafetyPrerequisites` resolved
3. Explicit `Supervisor` role approval
4. Audit trail in `ApprovalActions` table

This satisfies **FR-019** (Approval-Gated Write Capability) and **BR-005** (Safety Before Dispatch).

---

## 6. Cost Governor Integration (T3 Twist)

Every agent step is budget-aware:

### 6.1 Pre-Flight Estimation

```csharp
var estimate = estimatedTokens × providerRate × safetyMargin(1.2);
var reservation = await costGovernor.EstimateAndReserveAsync(userId, estimate);
```

### 6.2 Budget-Aware Routing Cascade

If the primary model (e.g., OpenAI gpt-4o) exceeds the user's remaining budget:
1. **Fallback 1:** Cheaper model (e.g., Ollama llama3.2)
2. **Fallback 2:** Semantic cache hit
3. **Fallback 3:** Structured refusal (HTTP 402)

### 6.3 Post-Execution Reconciliation

After the agent completes, actual token usage is reconciled:
```csharp
await costGovernor.CommitAsync(userId, reservationId, actualCost);
```

This prevents race conditions and silent overspending (CG-005).

---

## 7. Observability & Tracing

Every agent run is fully inspectable by `RunId`:

### 7.1 Agent Events

The `AgentEventStore` persists:
- `AgentRunStarted` / `AgentRunCompleted`
- `ToolInvoked` (name, parameters, result summary)
- `LLMCallCompleted` (model, tokens, cost, latency)
- `BudgetReserved` / `BudgetCommitted`

### 7.2 Correlation ID Flow

```
HTTP Request (X-Correlation-Id)
    ↓
Orchestrator (RunId = CorrelationId)
    ↓
Agent Step (AgentEvent with RunId)
    ↓
Tool Invocation (AgentEvent with RunId)
    ↓
LLM Call (AgentEvent with RunId)
    ↓
Budget Deduction (UsageRecord with RunId)
```

This satisfies **OBS-001** (Correlation ID) and **OBS-002** (LLM Tracing).

---

## 8. Graceful Degradation

The system degrades gracefully on failure:

| Failure Mode | Degradation Behavior |
|--------------|---------------------|
| **LLM Provider Unavailable** | Cascade to fallback tier (OpenAI → Ollama → Mock) |
| **Retrieval Outage** | Plain RAG with "retrieval degraded" warning |
| **Schema Validation Failure** | One retry → structured refusal |
| **Budget Exhausted** | HTTP 402 with actionable options |
| **Timeout** | Cancel run, emit `AgentRunFailed`, return partial results if available |

---

## 9. Safety Gates (Structural, Not Prompts)

Unlike systems that rely on "prompting the model to be safe", EquipFlow enforces safety structurally:

1. **Safety Prerequisite Gate:** `WorkOrder.SubmitForApproval()` throws if mandatory prerequisites are unresolved.
2. **Supervisor Approval Gate:** Only `Supervisor` role can approve via `ReviewWorkOrderCommand`.
3. **Dispatch Gate:** `WorkOrder.Dispatch()` requires `Approved` status + all safety checks cleared.

These rules are enforced by the **Domain Aggregate** (`WorkOrder.cs`) and verified by **Domain Unit Tests** (`DispatchTests`, `SubmissionApprovalGateTests`).

---

## 10. How to Inspect a Run

Every run can be inspected by `RunId`:

```bash
# Get run trace
curl -H "Authorization: Bearer <token>" http://localhost:5000/api/runs/{runId}

# Response includes:
{
  "runId": "abc-123",
  "status": "Completed",
  "steps": [
    {
      "agentName": "SymptomMatcherAgent",
      "toolsInvoked": ["SearchManuals", "QueryFaultHistory"],
      "tokensUsed": 1200,
      "costUsd": 0.0024,
      "latencyMs": 1450
    },
    ...
  ],
  "totalCostUsd": 0.0089,
  "citations": [...]
}
```

This satisfies **NFR-002** (Traceability) and **FR-005** (Orchestration inspectability).

---

## 11. Conclusion

EquipFlow's agentic workflow demonstrates that AI-powered industrial tools can be built securely by:
- **Treating the LLM as a synthesizer**, not an authoritative agent
- **Enforcing safety structurally** in the Domain layer
- **Gating write operations** behind deterministic approval gates
- **Governing cost** with pre-flight estimation and hard cut-offs
- **Providing full observability** via Correlation IDs and Agent Events

This architecture satisfies the ITI Brief's requirement for a bounded, auditable, and cost-controlled multi-agent system.