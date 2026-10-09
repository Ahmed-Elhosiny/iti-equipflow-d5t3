# ⚠️ Common Mistakes & Pitfalls

When building AI-integrated Clean Architecture systems, trainees frequently fall into these traps:

## 1. Leaking Infrastructure into the Domain
**The Mistake:** Adding `[NotMapped]` or EF Core attributes to Domain entities, or referencing `OllamaSharp` in the Application layer.
**The Fix:** The Domain and Application layers must only use primitive types and custom interfaces (Ports). Infrastructure concerns belong strictly in the `Infrastructure` project.

## 2. Ignoring the Cost Governor Pre-flight
**The Mistake:** Calling the LLM directly and checking the budget *after* the tokens are spent.
**The Fix:** Always use the `ICostGovernor` to estimate and reserve the budget *before* invoking the orchestrator. Fail-closed if the reservation fails.

## 3. Trusting Dense Vectors for Exact Matches
**The Mistake:** Relying solely on pgvector cosine similarity for queries like "Error Code E-404". Vector search is semantic and will fail at exact keyword matching.
**The Fix:** Always implement Hybrid Search. Use Postgres `tsvector` for exact keyword matching and fuse the results using Reciprocal Rank Fusion (RRF).

## 4. Bypassing the Work Order State Machine
**The Mistake:** Directly updating the `Status` column in the database from "Draft" to "Dispatched".
**The Fix:** The `WorkOrder` aggregate enforces structural gates. You must transition through `Submitted` ➔ `Approved` and ensure all `SafetyPrerequisite` items are completed before dispatch is allowed.

## 5. Unbounded Agent Tool Loops
**The Mistake:** Allowing an LLM agent to call tools indefinitely until it "figures it out".
**The Fix:** Always enforce a strict `MaxIterations` limit (e.g., 3) and a global timeout in the orchestrator to prevent infinite loops and budget exhaustion.
