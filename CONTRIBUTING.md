# Contributing to EquipFlow

EquipFlow is an enterprise-grade Industrial Maintenance Copilot built on **Clean Architecture** and **CQRS**. We enforce strict layer separation, structural safety gates, and rigorous cost governance. 

## 1. Architectural Boundaries (Non-Negotiable)
- **Domain Layer:** Pure C# business rules only. **ZERO** dependencies on external frameworks, EF Core, or ASP.NET.
- **Application Layer:** CQRS (MediatR) and Ports/Interfaces only. No infrastructure implementations.
- **Infrastructure Layer:** Adapters for EF Core, pgvector, and LLM Providers (OpenAI/Ollama/Mock).
- **WebApi Layer:** Minimal APIs and Composition Root only. Never call repositories directly from the API layer.

## 2. Branching Strategy
All work must be tied to a GitHub Issue. Direct pushes to `main` are blocked.
- Format: `<type>/<issue-number>-<short-description>`
- Examples: `feat/248-cli-token-mismatch`, `fix/238-fail-open-security`, `docs/252-ai-usage-log`

## 3. Commit Messages
We strictly follow **Conventional Commits**:
```text
<type>(<scope>): <description>

[optional body]
```
- **Types:** `feat`, `fix`, `docs`, `test`, `refactor`, `chore`.
- **Scopes:** `domain`, `application`, `infrastructure`, `webapi`, `cli`, `agentic`.

## 4. Pull Request Workflow
Every PR must:
1. Be linked to an Issue using `Closes #<issue-number>` in the description.
2. Include a "What / Why / How tested" breakdown.
3. **Mandatory Self-Review:** The author must leave exactly 3 inline comments on their own PR before requesting review:
   - **Architectural:** How does this affect Clean Architecture boundaries or domain purity?
   - **Technical:** How does this handle edge cases, concurrency, or performance?
   - **Security:** How does this mitigate OWASP Web/LLM Top 10 risks (e.g., IDOR, Prompt Injection, Budget Exhaustion)?
