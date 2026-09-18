# EquipFlow Security Report

## 1. Executive Summary

This document outlines the security controls, threat mitigations, and architectural decisions implemented in EquipFlow to protect the system, its users, and the industrial maintenance data it processes. 

EquipFlow is designed with a "Defense in Depth" strategy, addressing both traditional web application threats (**OWASP Web Top 10**) and AI-specific threats (**OWASP LLM Top 10**). The backend remains the absolute authority for authentication, authorization, safety gating, and cost controls; the LLM is never trusted with execution or authorization decisions.

---

## 2. OWASP Web Top 10 Mitigations

### 2.1 Broken Access Control
- **Authentication:** JWT Bearer tokens are required for all protected endpoints.
- **Role-Based Access Control (RBAC):** Enforced server-side via ASP.NET Core Authorization policies. Roles include `Technician`, `Engineer`, `Manager`, and `Supervisor`. UI hiding is never relied upon.
- **Object-Level Authorization:** Users can only access their own Work Orders, Budgets, and Run traces. Managers/Supervisors have fleet-wide visibility.
- **Approval Gates:** Only users with the `Supervisor` role can approve, reject, or edit-and-approve Work Orders for dispatch.

### 2.2 Cryptographic Failures
- **JWT Signing:** Tokens are signed using HMAC-SHA256 with a strong, configurable secret key (`Jwt__Key`).
- **Data at Rest:** PostgreSQL handles encryption at rest in production deployments.
- **No Hardcoded Secrets:** All sensitive configuration (API keys, DB passwords) is externalized via environment variables (`.env`).

### 2.3 Injection
- **SQL Injection:** Prevented by using Entity Framework Core (parameterized queries) for all database interactions.
- **File Upload Injection:** Document ingestion validates file extensions and uses isolated, safe parsers (`PdfDocumentExtractor`, `DocxDocumentExtractor`) to prevent malicious payload execution.

### 2.4 Security Misconfiguration
- **Environment Variables:** `.env.example` is provided with safe placeholders. No real secrets are committed.
- **CORS:** Configured explicitly in the Web API to allow only trusted origins.
- **Health Endpoints:** `/health` and `/ready` are exposed for infrastructure monitoring but do not leak sensitive internal state.

---

## 3. OWASP LLM Top 10 Mitigations

### 3.1 Prompt Injection (Direct & Indirect)
- **Threat:** Attackers attempting to override system instructions via user prompts (Direct) or malicious content inside ingested documents (Indirect).
- **Mitigation:** 
  - Strict separation of System Prompts and User/Retrieved Context.
  - The Golden Set Evaluation Harness includes 6 explicit injection cases (e.g., GM-021 to GM-026) proving the system ignores embedded commands like "ignore previous instructions" or "reveal system prompt".
  - Retrieved documents are treated as untrusted data.

### 3.2 Insecure Output Handling
- **Threat:** Passing LLM output directly to shell, SQL, or file paths.
- **Mitigation:** LLM output is strictly parsed into structured JSON contracts (C# Records). Tool arguments are schema-validated by the `ValidatingToolDispatcher` before execution. The LLM never generates executable code or raw SQL.

### 3.3 Sensitive Information Disclosure
- **Threat:** LLM leaking PII or internal system prompts.
- **Mitigation:** 
  - The synthetic corpus contains no real PII.
  - System prompts explicitly instruct the model not to disclose internal instructions.
  - Cost Governor and Audit logs do not record raw prompts, only token counts and hashed inputs.

### 3.4 Excessive Agency
- **Threat:** Agents performing unintended, destructive actions.
- **Mitigation:**
  - **Tool Allow-Lists:** Each agent (SymptomMatcher, DiagnosticPlanner, WorkOrderGenerator) has a strictly defined set of permitted tools via `StaticAgentToolRegistry`.
  - **Gated Write Tools:** The most consequential action (Dispatching a Work Order) is a deterministic backend operation that **requires** structural safety prerequisite resolution AND explicit Supervisor approval. The LLM cannot bypass this gate.

### 3.5 Unbounded Consumption
- **Threat:** Runaway LLM calls draining budgets (Token/Compute exhaustion).
- **Mitigation:** Addressed by the **T3 Cost Governor**:
  - Pre-flight token estimation with a 1.2x safety margin.
  - Per-user monthly budgets with pessimistic locking (`SELECT ... FOR UPDATE`) to prevent race conditions.
  - Hard cut-offs and structured HTTP 402 refusals when budgets are exhausted.
  - Max-iteration breakers and per-step timeouts (45s) in the Orchestrator.

---

## 4. Safety & Structural Enforcement

Unlike systems that rely on "prompting the model to be safe", EquipFlow enforces safety structurally in the Domain Layer:

1. **Safety Prerequisite Gate:** A Work Order cannot transition to `PendingApproval` if mandatory safety prerequisites are unresolved.
2. **Supervisor Approval Gate:** A Work Order cannot transition to `Approved` without an explicit `ApprovalAction` from a Supervisor.
3. **Dispatch Gate:** A Work Order cannot be dispatched unless it is `Approved` and all safety checks are cleared.

These rules are enforced by the Domain Aggregate (`WorkOrder.cs`) and verified by Domain Unit Tests (`DispatchTests`, `SubmissionApprovalGateTests`).

---

## 5. Audit & Observability

- **Correlation IDs:** Every incoming HTTP request is assigned a Correlation ID (`CorrelationIdMiddleware`) that flows through the Orchestrator, Agents, Tools, and LLM calls.
- **Agent Event Store:** Every agent step, tool invocation, and LLM call is persisted in the `AgentEvents` table with timestamps, token usage, and outcomes.
- **Approval Audit Trail:** Every Supervisor action (Approve, Reject, Edit) is recorded in the `ApprovalActions` table with the actor's ID, timestamp, and comments.

---

## 6. Secrets Management & Supply Chain

- **Zero Secrets in Git:** No API keys, passwords, or certificates are committed to the repository.
- **Secret Scanning:** The ITI submission requires a full history secret scan to ensure no leaked keys exist in previous commits.
- **Dependency Management:** .NET 10 LTS is used for long-term support. NuGet packages are pinned to specific versions in the `.csproj` files to prevent supply chain attacks.

---

## 7. Conclusion

EquipFlow demonstrates that AI-powered industrial tools can be built securely by treating the LLM as an untrusted synthesizer rather than an authoritative agent. By combining deterministic backend gates (Safety & Approval), strict tool boundaries (Excessive Agency), and economic controls (Cost Governor), EquipFlow mitigates the unique risks of Agentic RAG systems.