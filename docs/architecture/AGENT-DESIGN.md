# EquipFlow — Agent Design Document

| Field | Value |
|---|---|
| Status | Approved |
| Version | 1.1 |
| Date | 2026-09-08 |
| Implements | Phase 2 AG-001…AG-010, TL-001…TL-008, FR-021…FR-024 · ITI FR-4, FR-5 |
| References | ADR-001 (Orchestration), ADR-004 (Cost Governor), `docs/references/System Architecture.pdf` §4.2 |

## 1. Purpose & Scope
This document specifies the D5 agentic workflow: three specialised agents plus one
orchestrator, their typed I/O contracts, restricted tool allow-lists, termination
conditions, and the mandatory execution controls. It is the implementation guide for
the Agentic Coordination Layer.

## 2. Boundaries & Invariants
- Agents are **stateless**: typed input in → bounded reasoning loop over allow-listed
  tools → typed output out.
- **No direct data access** (AG-005): all data flows through controlled application capabilities.
- **No authority** (AG-006): agents never decide authorization, approval, or budget.
- **No side-effecting writes**: the only write tool in the system (Dispatch
  Authorization) is outside every agent allow-list and gated by FR-019/FR-026/FR-027.
- **Typed communication only** (AG-004): inter-step payloads are C# records; LLM JSON
  is schema-validated before acceptance. Free-form text never crosses a step boundary.
- **Evidence-carrying findings** (AG-007): every factual claim carries a `Citation` or is
  explicitly marked unsupported.
- The LLM is never the authoritative source of maintenance facts (BR-002).

## 3. Agent Registry
Agents are resolved from a registry of immutable definitions; the Orchestrator enforces
the limits declared per agent.

```csharp
public sealed record AgentDefinition
{
    public required string AgentId { get; init; }
    public required string RoleDescription { get; init; }
    public required IReadOnlyList<string> AllowedTools { get; init; }
    public required string SystemPromptTemplateId { get; init; } // versioned artifact (ENG-003)
    public required ModelTier DefaultModelTier { get; init; }    // Cost Governor routing input
    public int MaxIterations { get; init; } = 3;                 // AG-008 breaker
    public TimeSpan StepTimeout { get; init; } = TimeSpan.FromSeconds(30); // AG-009
}
```

| AgentId | Tier | Allowed tools |
|---|---|---|
| `symptom-matcher` | Standard | `SearchManuals`, `QueryFaultHistory` |
| `diagnostic-safety-planner` | High | `GetEquipmentSpecs`, `GenerateSafetyChecklist` |
| `work-order-generator` | Standard | `ValidateBudget`, `DraftWorkOrder` |

## 4. Orchestrator (Supervisor)
Owns the run lifecycle; performs no domain reasoning itself.

```csharp
public enum AgentRunStatus
{
    Initialized, PreFlight, SymptomMatching, Planning, Drafting,
    Completed, Degraded, Refused, BudgetExhausted, Failed, Cancelled
}
```

Run lifecycle: `Initialized → PreFlight → SymptomMatching → Planning → Drafting →
Completed`, with terminal alternatives `Degraded | Refused | BudgetExhausted | Failed | Cancelled`.

Responsibilities:
1. Open the run: mint Correlation ID (OBS-001), emit `AgentRunStarted`.
2. Per step: Cost Governor pre-flight (ADR-004) → invoke agent under `StepTimeout` →
   schema-validate output → bounded retry on transient failure → advance state.
3. Enforce `MaxIterations` inside each agent's tool loop (breaker).
4. On unrecoverable failure: degrade to plain RAG with citations, or structured refusal (AG-010).
5. Close the run: emit `AgentRunCompleted`, reconcile tokens/cost (CG-006, CG-007).

## 5. Agent Specifications

### 5.1 Symptom Matcher
- **Role:** match reported symptoms to equipment/manual/fault-history evidence (FR-022).
- **Input / Output:**

```csharp
public sealed record SymptomMatchInput(
    string SymptomDescription,
    string EquipmentId,
    string? ProductionLineId);

public sealed record SymptomMatchResult(
    IReadOnlyList<RankedFault> RankedFaults,
    IReadOnlyList<Citation> EvidenceCitations,
    bool IsGrounded);

public sealed record RankedFault(string FaultCode, string Description, double Confidence);
```

- **Termination:** returns ranked faults with citations, or `IsGrounded = false`
  (drives clarification/refusal, FR-016).
- **Failure behaviour:** retrieval outage → `Degraded` (plain RAG answer).

### 5.2 Diagnostic & Safety Planner
- **Role:** evidence-based diagnostic sequence + explicit safety prerequisites (FR-023).
- **Input / Output:**

```csharp
public sealed record DiagnosticPlannerInput(
    SymptomMatchResult SymptomResult,
    string EquipmentId);

public sealed record DiagnosticPlanResult(
    string DiagnosticStepsMarkdown,
    IReadOnlyList<SafetyPrerequisiteDto> SafetyPrerequisites,
    IReadOnlyList<Citation> EvidenceCitations);

public sealed record SafetyPrerequisiteDto(
    string Description,
    bool IsMandatory,
    string? SourceDocumentId);
```

- **Termination:** plan emitted where every factual claim carries a citation or is
  marked unsupported (AG-007).
- **Failure behaviour:** schema-validation failure → one bounded re-prompt → `Degraded`.

### 5.3 Work Order Generator
- **Role:** compose the proposed work-order draft from validated findings (FR-024).
  Read-only: persistence happens via the Application-layer `CreateWorkOrderCommand`.
- **Input / Output:**

```csharp
public sealed record WorkOrderGeneratorInput(
    DiagnosticPlanResult DiagnosticPlan,
    string EquipmentId,
    string RequestedByUserId,
    Guid RunId);

public sealed record WorkOrderDraftResult(
    WorkOrderDraftDto Draft,
    decimal EstimatedCostUsd,
    bool BudgetApproved);

public sealed record WorkOrderDraftDto(
    string Title,
    string Description,
    IReadOnlyList<SafetyPrerequisiteDto> SafetyPrerequisites);
```

- **Termination:** draft DTO returned, or budget refusal via the ADR-004 cascade.
- **Failure behaviour:** budget exhausted → `BudgetExhausted` structured refusal; never a silent partial draft.

### Shared evidence type

```csharp
public sealed record Citation(
    string DocumentId,
    string ChunkId,
    string? Section,
    int? Page,
    float Score);
```

## 6. Tool Catalogue & Executor Rules

| Tool | Kind | Consumed by | AuthZ |
|---|---|---|---|
| `SearchManuals` | RAG retrieval | symptom-matcher | authenticated |
| `QueryFaultHistory` | read | symptom-matcher | Technician+ |
| `GetEquipmentSpecs` | read | diagnostic-safety-planner | Technician+ |
| `GenerateSafetyChecklist` | RAG retrieval | diagnostic-safety-planner | authenticated |
| `ValidateBudget` | read (governor) | work-order-generator | system-internal |
| `DraftWorkOrder` | read (compose only) | work-order-generator | Technician+ |
| *(write)* Dispatch Authorization | side-effecting | **no agent** — Supervisor via approval gate | Supervisor only (FR-019) |

Executor rules (TL-006…TL-008): every invocation is schema-validated against its
contract, checked against caller identity + agent allow-list + business rules, and
persisted with run/step attribution.

## 7. Cost Governor Integration (ADR-004)
- Pre-flight estimate before each step; reservation held during execution.
- Actual usage reconciled post-step and attributed to `RunId` + `AgentId` (CG-007).
- Over-budget triggers the cascade: cheaper tier → cache → structured refusal (CG-009).

## 8. Observability
Per run: `AgentRunStarted`, per-step `AgentStepStarted/Completed`, `ToolInvoked`,
`CitationAttached`, `AgentRunCompleted`, plus token/cost totals — all queryable by run ID (NFR-002, OBS-002).

## 9. Testability
- Agents unit-testable with stubbed `ILLMProvider` (NFR-006).
- Contract tests assert tool and agent JSON schemas (ITI §4 Testing).
- Negative matrix coverage: agent requests disallowed tool → blocked; iteration limit
  exceeded → breaker trips; degraded path returns cited plain-RAG answer (EVAL-009, EVAL-011).

## 10. Compliance Map
| Requirement | Satisfied by |
|---|---|
| AG-001 | §3 registry (3 agents + orchestrator) |
| AG-002 | §5 contracts + termination conditions |
| AG-003 | §3 allow-lists, §6 executor |
| AG-004 | §2, §5 records |
| AG-005 / AG-006 | §2 invariants |
| AG-007 | §5 citations / unsupported marking |
| AG-008 / AG-009 | §3 `MaxIterations` / `StepTimeout`, §4 controls |
| AG-010 | §4 degradation, §5 failure behaviours |
| FR-021…FR-024 | §4 lifecycle, §5.1…§5.3 |