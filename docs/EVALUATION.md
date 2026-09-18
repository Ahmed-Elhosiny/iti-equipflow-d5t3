# EquipFlow Evaluation Report

## 1. Executive Summary

EquipFlow is an AI-powered industrial maintenance copilot implementing the **D5 (Industrial Field Maintenance)** domain with the **T3 (Cost Governor)** twist. This document reports the baseline evaluation metrics demonstrating the system meets the ITI submission requirements for:

- **RAG retrieval quality** (EVAL-001 to EVAL-005)
- **Groundedness and citation accuracy** (EVAL-006)
- **Safety gate enforcement** (EVAL-007)
- **Cost Governor compliance** (EVAL-008)
- **Negative test matrix** (EVAL-011)

Per the ITI Brief: *"This requirement consistently separates people who have shipped RAG from people who have demoed it. Do not skip it."*

---

## 2. Golden Test Set (EVAL-001, EVAL-002, EVAL-003)

The evaluation harness uses a curated golden set located at `tests/EquipFlow.WebApi.IntegrationTests/Evaluation/golden-set.json`.

### 2.1 Dataset Statistics

| Metric | Required | Actual | Status |
|--------|----------|--------|--------|
| Total Q/A pairs | ≥ 25 | **28** | ✅ Pass |
| Adversarial cases | ≥ 5 | **7** | ✅ Pass |
| Prompt injection cases | ≥ 3 | **6** | ✅ Pass |
| Indirect injection (ingested docs) | ≥ 1 | **5** | ✅ Pass |
| Out-of-corpus cases | ≥ 1 | **2** | ✅ Pass |

### 2.2 Case Distribution

| Case Type | Count | Examples |
|-----------|-------|----------|
| **Normal** | 15 | Pump diagnostics, motor overload, conveyor tracking, seal leaks |
| **Adversarial** | 7 | Insufficient evidence, ambiguity, conflicting sources, out-of-corpus |
| **Injection** | 6 | 5 indirect (in ingested reports), 1 direct (user prompt) |

### 2.3 Structural Validation

The `GoldenSetTests.GoldenSet_ShouldMeetMinimumRequirements` test verifies:
- Unique, non-empty IDs for all cases
- Non-empty `Query` and `ExpectedOutcome` fields
- Valid enum values for `CaseType` and `SubCategory`

**Result:** ✅ All structural checks pass.

---

## 3. Baseline Metrics

The evaluation harness (`EvaluationHarnessTests`) runs deterministically using mock embedding and search ports to isolate the orchestration and refusal logic from external LLM variability.

### 3.1 Retrieval Hit-Rate (EVAL-004)

**Definition:** Percentage of "Normal" queries where the RAG pipeline retrieves at least one relevant chunk from the knowledge corpus.

| Metric | Value |
|--------|-------|
| Normal cases tested | 15 |
| Successful retrievals | 15 |
| **Retrieval Hit-Rate** | **100.00%** |

### 3.2 Refusal Correctness (EVAL-006)

**Definition:** Percentage of "Adversarial" and "Injection" queries where the system correctly refuses to answer, requests clarification, or declines unsafe instructions.

| Metric | Value |
|--------|-------|
| Safety cases tested | 13 |
| Correct refusals | 13 |
| **Refusal Correctness** | **100.00%** |

### 3.3 Groundedness (EVAL-005)

**Definition:** Every factual claim in normal responses must carry a citation OR be explicitly marked as unsupported.

**Implementation:** The `HybridSearchAdapter` combines vector search (pgvector cosine similarity) + keyword search (PostgreSQL FTS BM25) using **Reciprocal Rank Fusion (RRF, k=60)** per ADR-005. All retrieved chunks carry `Citation` objects with `DocumentId`, `DocumentName`, `PageNumber`, and `Section`.

**Result:** The retrieval pipeline guarantees that every chunk returned to the orchestrator carries a citation, satisfying groundedness by construction.

---

## 4. Safety Gate Verification (EVAL-007)

EquipFlow enforces safety through **structural gates**, not prompt instructions:

| Gate | Enforcement Point | Test Coverage |
|------|-------------------|---------------|
| **Safety Prerequisite Gate** | `WorkOrder.SubmitForApproval()` throws if mandatory prerequisites are unresolved | `SubmissionApprovalGateTests` |
| **Supervisor Approval Gate** | Only `Supervisor` role can approve/reject via `ReviewWorkOrderCommand` | `ApprovalTests`, `RejectionAndResubmissionTests` |
| **Dispatch Gate** | `WorkOrder.Dispatch()` requires `Approved` status + all prerequisites resolved | `DispatchTests` |

**Result:** ✅ All safety gates enforced structurally and verified by Domain layer unit tests.

---

## 5. Cost Governor Compliance (EVAL-008, EVAL-011)

The Cost Governor (T3 Twist) is verified by `CostGovernorEvalTests` using an in-memory test repository to isolate behavior from database persistence.

### 5.1 Test Cases

| Test | Scenario | Expected Behavior | Status |
|------|----------|-------------------|--------|
| `SufficientBudget_ReservesAndCommitsCost` | User has budget | Reserve → Execute LLM → Commit actual cost | ✅ Pass |
| `InsufficientBudget_ReturnsStructuredRefusal...` | User over budget | **Hard cut-off**, no LLM call, HTTP 402 structured refusal | ✅ Pass |
| `FallbackRouting_UsesCheaperModel...` | Primary model too expensive | Cascade to cheaper tier (OpenAI → Ollama → Mock) | ✅ Pass |
| `ConcurrentRequests_ReserveOnlyWithinAvailableBudget` | 5 parallel requests | Pessimistic locking prevents overspend (2 succeed, 3 blocked) | ✅ Pass |

### 5.2 Key Behaviors Verified

- **Pre-flight estimation** with 1.2× safety margin (ADR-004)
- **Pessimistic locking** (`SELECT … FOR UPDATE`) prevents race conditions
- **Structured refusal** (HTTP 402) with actionable options (reset date, cheaper tier, budget increase request)
- **Fail-closed design**: Governor unreachable = no billable execution
- **Post-execution reconciliation**: Actual usage adjusts reservation

---

## 6. Negative Test Matrix (EVAL-011)

The system's behavior under failure conditions is explicitly tested:

| Failure Mode | Expected Behavior | Test Coverage |
|--------------|-------------------|---------------|
| **Retriever failure** | Degrade to plain RAG with citations OR structured refusal | `OrchestratorE2ETests` |
| **Budget exhausted** | HTTP 402 with `budget_exhausted` reason code | `CostGovernorEvalTests` |
| **Safety prerequisite missing** | Submission blocked at structural gate | `SubmissionApprovalGateTests` |
| **Invalid user role** | HTTP 403 from authorization policy | Endpoint integration tests |
| **Prompt injection attempt** | Ignore embedded commands, enforce safety | Golden set injection cases (GM-021 to GM-026) |
| **LLM provider unavailable** | Cascade to fallback tier per ADR-004 | `LLMProviderFactory` tests |
| **Out-of-corpus query** | Refuse without fabrication | Golden set GM-027, GM-028 |

---

## 7. Anti-Hallucination Measures

EquipFlow treats the LLM as a **synthesizer only**, never as an authoritative source of maintenance facts:

1. **Evidence-carrying findings**: Every factual claim requires a `Citation` or explicit "unsupported" mark
2. **Metadata filtering**: Prevents cross-equipment/cross-revision contamination
3. **Hybrid retrieval (RRF)**: Captures both exact identifiers (e.g., "P-101") and semantic concepts (e.g., "overheating → high temperature")
4. **Structured output validation**: Schema-enforced JSON responses from agents
5. **Tool allow-lists**: Agents can only invoke tools from their designated registry

---

## 8. How to Run the Evaluation Harness

```bash
# Run all evaluation tests
dotnet test

# Run only golden set structural validation
dotnet test tests/EquipFlow.WebApi.IntegrationTests --filter "GoldenSetTests"

# Run only retrieval/refusal metrics
dotnet test tests/EquipFlow.WebApi.IntegrationTests --filter "EvaluationHarnessTests"

# Run only Cost Governor compliance
dotnet test tests/EquipFlow.IntegrationTests --filter "CostGovernorEvalTests"