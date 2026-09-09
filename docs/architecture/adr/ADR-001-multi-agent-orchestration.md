# ADR-001 — Multi-Agent Orchestration Pattern: Sequential Supervisor Pipeline

## Context
- The D5 workflow is inherently sequential: symptom → equipment/revision identification →
  diagnostic sequence → safety prerequisites → work order (FR-021).
- ITI mandates ≥3 specialised agents + an orchestrator, typed inter-agent contracts,
  restricted tool allow-lists, and mandatory controls: max-iteration breaker, per-step
  timeout, retry with backoff, and graceful degradation to plain RAG; every run must be
  inspectable step-by-step by run ID (FR-4, FR-5).
- The approved System Architecture Document places an Agentic Coordination Layer above
  the CQRS application stack, with a central Orchestrator and a pluggable Agent Registry (§4.2).
- Safety is structural: no agent may bypass the Supervisor approval gate or the
  safety-prerequisite gate (FR-019, FR-026; D5 guarded risk: skipping a safety prerequisite).

## Decision
Adopt a **Sequential Supervisor Pipeline**: one Orchestrator (supervisor) drives a fixed,
state-machine-backed pipeline of three specialised agents. Agents never communicate
directly; the Orchestrator passes typed contracts between steps.

### Why this pattern (alternatives considered)
- **Planner–Executor (rejected):** plan quality here depends on staged evidence
  (symptoms → specs → safety). An upfront planner would commit before retrieval grounding.
- **Free-form dynamic supervisor (rejected):** dynamic routing raises cost and audit
  complexity; the D5 sequence is known a priori, and T3 cost governance favours a predictable path.
- **Decentralised pipeline, agents chaining directly (rejected):** loses the single
  enforcement point for budget, timeouts, iteration limits and audit.

### Components
1. **Orchestrator (supervisor):** owns the run context and Correlation ID, enforces global
   constraints (Cost Governor pre-flight, global timeout), routes payloads sequentially,
   and owns degradation.
2. **Pipeline agents:** SymptomMatcher → Diagnostic & Safety Planner → Work Order
   Generator. Full specs, contracts and tool allow-lists in `AGENT-DESIGN.md`.
3. **Typed contracts:** C# `record` types; LLM output is parsed and schema-validated.
   Validation failure → bounded retry → degradation. Free-form text is never passed between steps.

### Mandatory controls (FR-5, AG-008…AG-010)
- **Max-iteration breaker:** ≤3 tool-loop iterations per agent (per `AgentDefinition`).
- **Per-step timeout:** 30 s per agent step, plus a global run timeout.
- **Retry with backoff:** one retry with exponential backoff, transient failures only.
- **Graceful degradation:** any pipeline failure degrades to a plain RAG answer with
  citations, or a structured refusal — never a partial or unsafe write.
- **Inspectability:** `AgentRunStarted` / `AgentRunCompleted` / tool events persisted per run ID (OBS-001, OBS-002).

## Consequences
- **Positive:** single enforcement and audit point; agents isolated and unit-testable with
  stubbed LLMs (NFR-006); deterministic fallback; clean trace per run ID.
- **Negative:** sequential execution adds latency vs parallel agents (mitigated by
  parallelising tool calls *within* a step).
- **Risk:** Orchestrator is a single point of failure for the agentic path — mitigated by
  degradation to plain RAG, which remains fully functional.

## Compliance map
| Requirement | Satisfied by |
|---|---|
| FR-4 (≥3 agents + orchestrator, typed contracts, restricted tools) | Components 1–3, AGENT-DESIGN.md §3–4 |
| FR-5 (named justified pattern + controls) | Decision + Mandatory controls |
| AG-008 / AG-009 / AG-010 | Iteration breaker / timeout+retry / degradation |