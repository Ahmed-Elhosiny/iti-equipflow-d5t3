# 🧪 Lab Sheet: Hands-on with EquipFlow

**Prerequisites:** Docker Desktop installed, .env configured.

## Exercise 1: Stack Initialization & Seeding
1. Start the database and API: `docker compose up -d`
2. Seed the baseline data (equipment, budgets, corpus): `docker compose --profile tools run --rm seed`
3. Verify health: `curl http://localhost:5000/health`

## Exercise 2: Exploring the RAG Pipeline
1. Authenticate as a Technician via `/api/auth/login`.
2. Use the `/api/chat` endpoint to ask: *"What are the common causes of P-101 overheating?"*
3. **Observe:** Note the `citations` array in the response. How does the system prove it didn't hallucinate?

## Exercise 3: Triggering the Multi-Agent Workflow
1. Use the `/api/ai/analyze` endpoint with the symptom: *"Pump P-101 is drawing 18% more current than normal and discharge pressure is low."*
2. **Observe:** The system will invoke the Symptom Matcher, Diagnostic Planner, and Work Order Generator sequentially.
3. Take the returned `WorkOrderId` and submit it for approval via `/api/workorders/{id}/submit`.

## Exercise 4: Testing the Cost Governor (Adversarial)
1. Authenticate as a user with a depleted budget (or artificially lower the budget in the DB).
2. Attempt to run `/api/ai/analyze` again.
3. **Observe:** The system should return a `402 Payment Required` with the structured SDD §5.3 budget-refusal DTO, offering options like "Try cheaper model tier".
