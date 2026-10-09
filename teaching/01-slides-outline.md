# 📊 Slides Outline: Building an AI Maintenance Copilot

**Target Audience:** Post-graduate software engineers
**Duration:** 90 Minutes

## 1. Introduction & Context (15 mins)
- **The Problem:** Industrial maintenance relies on dense, fragmented PDF manuals. Downtime costs thousands per minute.
- **The Solution:** EquipFlow – An AI copilot that ingests docs, answers grounded questions, and drafts work orders.
- **The D5T3 Variant:** Domain (Industrial Maintenance) + Twist (Cost Governor to prevent API bankruptcy).

## 2. Clean Architecture in the AI Era (20 mins)
- **Strict Layer Separation:** Why the Domain layer knows nothing about OpenAI or pgvector.
- **Ports & Adapters:** Swapping LLM providers (OpenAI vs. Ollama) without touching business logic.
- **CQRS & MediatR:** Managing the complexity of multi-step agentic workflows.

## 3. RAG & Hybrid Search (20 mins)
- **Ingestion Pipeline:** Structure-aware chunking and content-hash idempotency.
- **Retrieval:** Why dense vectors (pgvector) aren't enough. Fusing Cosine Distance with Postgres FTS (Keyword) using Reciprocal Rank Fusion (RRF).
- **Groundedness:** Enforcing citations to prevent hallucinations.

## 4. Multi-Agent Orchestration & Cost Governor (25 mins)
- **The Agent Team:** Symptom Matcher ➔ Diagnostic Planner ➔ Work Order Generator.
- **Tool Calling:** How agents interact with the database safely (Allow-lists, JSON schema validation).
- **The Cost Governor (T3):** Pre-flight token estimation, pessimistic locking, and the cheaper-model cascade.

## 5. Live Demo & Q&A (10 mins)
- Walkthrough of the 5-minute demo path.
- Open floor for architectural questions.
