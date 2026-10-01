# EquipFlow AI Usage Log

## 1. Purpose and Honesty Statement
This document is an honest engineering log detailing how AI tools were utilized during the development of EquipFlow. AI was used as a governed participant in the development process, not as an autonomous author. The goal of this log is to demonstrate deliberate direction of AI tools, rigorous verification of their output, and the correction of their mistakes. A log claiming flawless AI usage would be dishonest; this document highlights where AI failed and how human judgment corrected it.

## 2. Human Accountability Statement
The human candidate (Ahmed Elhosiny) is solely responsible for the final submitted code, architecture decisions, security controls, and documentation. Every line of code, architectural boundary, and security gate was reviewed, understood, and approved by the human developer. AI tools do not hold accountability for this submission.

## 3. AI Delegation Categories

| Category | What was Delegated to AI | What was Written/Enforced by Human |
|---|---|---|
| **Architecture & Design** | Brainstorming Clean Architecture boundaries, CQRS patterns, and drafting ADR structures. | Defining the core Domain model (WorkOrder state machine, safety prerequisites, approval gates) and enforcing strict layer dependencies. |
| **Implementation** | Generating boilerplate code, MediatR handlers, EF Core configurations, and adapter scaffolding (pgvector, OpenAI). | Reviewing all generated code for Clean Architecture violations, ensuring Domain purity, and defining business rules. |
| **Testing** | Generating xUnit test structures, mock setups, and test data generation. | Defining the actual test scenarios, especially adversarial cases, safety gate enforcement, and the negative test matrix. |
| **Debugging** | Assisting in diagnosing EF Core concurrency issues, SSE streaming parsing errors, and DI scope lifetimes. | Identifying the root cause of security flaws (e.g., fail-open authentication) and directing the fix. |
| **Documentation** | Structuring the BRD, SDD, and Evaluation reports based on human-provided bullet points and requirements. | Verifying that documentation matches actual implementation and explicitly documenting deferred work/gaps. |
| **Repo Hygiene** | Generating GitHub Action workflows, PR templates, and conventional commit hooks. | Enforcing the PR workflow, self-review mandates, and branch protection rules. |

## 4. Verification Process

AI-generated code is inherently untrustworthy until proven otherwise. The following verification gates were applied to all AI output:

1. **Build Verification:** Every AI-generated code block was subjected to `dotnet build` to ensure no hallucinated dependencies, missing namespaces, or syntax errors.
2. **Unit/Integration Tests:** AI-generated tests were run via `dotnet test`. Tests that passed trivially or tested implementation details rather than behavior were rewritten by the human to ensure they actually tested domain rules (e.g., structural safety gates).
3. **Manual API/CLI Verification:** The "5-Minute Demo Path" was manually executed end-to-end. This caught critical integration gaps that AI unit tests missed (e.g., CLI token mismatch, CLI silently swallowing SSE refusal events).
4. **Issue/PR Workflow:** All work was tracked via GitHub Issues and PRs. AI was used to draft PR descriptions, but the human verified the "What/Why/How tested" sections against actual manual test results.
5. **Mandatory Self-Review:** Every PR required 3 inline self-review comments (Architectural, Technical, Security) to force human reflection on AI-generated code before merging.

## 5. Concrete Examples of AI Mistakes and Corrections

This section details specific instances where AI generated flawed, insecure, or incorrect code, and how human judgment caught and corrected it.

### Mistake 1: Hardcoded Blended Pricing in Cost Governor (T3 Twist)
- **What AI did:** AI initially generated a Cost Governor implementation that used a hardcoded blended rate of `.01` per 1K tokens for pre-flight estimation to "make the math simple".
- **Why it was wrong:** This violated the T3 requirement for config-driven, provider-specific pricing and made the system unable to route to cheaper models accurately.
- **The Correction:** The human caught this during PR review and forced a refactor to resolve pricing dynamically from `IOptions<OpenAIOptions>` (Issue #244).

### Mistake 2: Fail-Open Security in Agentic Orchestrator
- **What AI did:** AI generated an agentic orchestrator that silently fell back to a hardcoded `MockUserId` when the JWT token was missing or invalid, attempting to "make local testing easier without breaking the flow".
- **Why it was wrong:** This was a massive security flaw (Fail-Open). It allowed unauthenticated users to execute agentic workflows and bypass object-level authorization.
- **The Correction:** The human identified this as a critical SEC-001 violation and refactored the orchestrator to fail closed, throwing an `InvalidOperationException` on anonymous identity (Issue #238).

### Mistake 3: CLI Token Field Mismatch
- **What AI did:** AI generated the CLI login command expecting an `accessToken` field from the API JSON response, while the API actually returned a `token` field. 
- **Why it was wrong:** The AI didn't catch this because it was generating code in isolation without running the integrated system. It caused the CLI demo path to fail immediately.
- **The Correction:** Manual CLI testing revealed the login failure. The human directed the AI to fix the JSON parsing to match the actual API contract and add graceful error handling (Issue #248).

### Mistake 4: Silent CLI Failures on Budget Enforcement
- **What AI did:** When the backend correctly refused an LLM call due to ADR-004 budget enforcement, the AI-generated CLI SSE parser simply ignored the `refusal` event and printed a blank screen.
- **Why it was wrong:** It hid the fact that the backend was working correctly and strictly enforcing cost controls, making the system appear broken to an evaluator.
- **The Correction:** Manual testing exposed the silent failure. The human directed the AI to parse the `refusal` event and display the backend's structured error message (Issue #250).

## 6. Known Limitations and Deferred Work

- **Orchestrator Budget Reservation Gap:** The orchestrator currently struggles to pass a valid `ReservationId` to the LLM provider in some CLI execution paths, triggering ADR-004 violations. This is documented as a known gap in the SDD and represents a deliberate fail-closed behavior rather than a silent bypass.
- **OpenTelemetry Integration:** Deferred in favor of a custom Agent Event Store to meet the MVP deadline. Basic event logging is sufficient for the MVP evaluation harness.
- **Cross-Encoder Re-Ranking:** Deferred due to latency and cost constraints for the MVP corpus size.

## 7. Final Submission Responsibility Statement

I, Ahmed Elhosiny, confirm that I have read, understood, and verified every component of this submission. AI tools were used to accelerate boilerplate, explore design spaces, and draft documentation, but all architectural boundaries, security controls, and domain rules were strictly enforced by human judgment. I take full responsibility for the quality, security, and correctness of the EquipFlow D5T3 submission.
