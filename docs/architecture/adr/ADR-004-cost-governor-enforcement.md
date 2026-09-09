# ADR-004 — Cost Governor Enforcement (T3 Central Decision)

## Context
- T3 makes cost a first-class safety property: per-user budgets, budget-aware routing,
  pre-flight estimation, hard cut-off and a spend view are mandatory.
- Agentic runs are multi-step and concurrent requests could silently overspend (CG-005).
- Every model execution must pass the governor (CG-010); on budget failure the system must
  take a cheaper path, defer, or refuse — never silently execute (CG-009).

## Decision
Implement a **centralised Cost Governor service** in the Agentic Coordination Layer acting
as a strict gateway for all LLM invocations.

1. **Normalisation:** all provider pricing is expressed in USD-equivalent cost; the governor
   is provider-agnostic (works with the ENG-001 abstraction).
2. **Pre-flight estimation (CG-002):** `estimated_tokens × provider_rate × safety_margin(1.2)`
   before any agent step executes.
3. **Reservation with concurrency protection (CG-005):** the estimated cost is *reserved*
   before execution using pessimistic locking (PostgreSQL `SELECT … FOR UPDATE` on the user's
   budget row). This closes the race where two concurrent runs each see sufficient budget.
4. **No bypass (CG-010):** `ILLMProvider` executions require a valid `BudgetReservationToken`;
   no token → no call. **Fail-closed:** if the governor store is unreachable, billable execution is blocked.
5. **Budget-aware routing & cascade (CG-003, CG-009):** over-budget triggers, in order:
   cheaper model tier → semantic cache hit → structured refusal (`budget_exhausted` DTO with
   remaining/estimated/options), surfaced to the client and to degradation logic.
6. **Post-execution reconciliation (CG-006, CG-007):** actual `usage` tokens adjust the
   reservation (release unused, deduct actual) and are persisted attributed to run ID + agent step.
7. **Spend view (CG-008):** `GET /api/cost/spend` with object-level authorization (own spend;
   elevated roles for others).

### Alternatives considered
- **Eventual-consistency ledger (deduct after the fact):** rejected — violates CG-004/CG-005;
  a runaway loop could spend unboundedly before reconciliation.
- **Client-side / prompt-level budget hints:** rejected — the LLM is never a budget authority (BR-001).
- **In-memory token bucket only:** acceptable interim for single-instance MVP, but cannot
  survive restarts or multiple instances; documented as a gap with Redis bucket as the closing path.

## Consequences
- **Positive:** hard, testable guarantees for every EVAL-008 and EVAL-011 cost scenario;
  full per-step financial attribution.
- **Negative:** one locked DB round-trip per billable step (latency); governor availability
  becomes on the critical path (fail-closed by design).
- **Risk:** lock contention under load — acceptable at MVP scale; mitigation path documented above.

## Compliance map
| Requirement | Satisfied by |
|---|---|
| CG-001 / CG-002 | Budget store + pre-flight estimation |
| CG-003 / CG-009 | Routing cascade + structured refusal |
| CG-004 / CG-010 | Reservation + `BudgetReservationToken`, fail-closed |
| CG-005 | Pessimistic-lock reservation |
| CG-006 / CG-007 / CG-008 | Reconciliation + attribution + spend view |