# ADR-003: Vector Store Selection

## Status
Accepted

## Context
EquipFlow requires a vector database to store document embeddings for RAG. The ITI brief mandates a relational store + a vector store with migrations, reproducible runtime via Docker (`docker compose up`), and strict adherence to MVP scope without incurring paid tier costs. 

The domain requires heavy relational data modeling (Equipment, Maintenance Events, Work Orders, Safety Prerequisites, Cost Governor budgets) alongside vector search.

## Decision
We will use **PostgreSQL with the `pgvector` extension** as a unified Relational and Vector Store.

### Implementation Details
*   **Single Source of Truth:** One PostgreSQL instance will handle both the domain entities (Users, Equipment, Work Orders) and the `DocumentChunks` table with a `vector` column for embeddings.
*   **Hybrid Search Execution:** PostgreSQL natively supports Full-Text Search (`tsvector`) for keyword retrieval and `pgvector` for dense retrieval, allowing both to be executed and fused within the same database query or application layer efficiently.
*   **Metadata Filtering:** Standard SQL `WHERE` clauses will be used for metadata filtering (FR-013), leveraging standard B-Tree indexes alongside HNSW/IVFFlat indexes for the vector columns.
*   **Migrations:** Standard EF Core migrations will manage both relational schema and vector extension setup.

## Alternatives Considered
1.  **Dedicated Vector DB (Qdrant, Milvus, Weaviate):** Rejected. While excellent for pure vector search, adding a separate Vector DB introduces infrastructure complexity (an extra Docker container, network hops, and dual-database transaction management). Since we *must* have a relational DB for the D5 workflow (Work Orders, Safety Gates) and T3 Cost Governor, a unified store is much leaner for the MVP.
2.  **Pure In-Memory Vector Store (e.g., Semantic Kernel Memory):** Rejected. Does not survive restarts, violates the requirement for a persistent store with migrations, and cannot handle the relational data requirements of the workflow.
3.  **Cloud-Managed Vector DB (Pinecone):** Rejected. Violates the "Free tier / Dockerizable / Offline-capable" constraints of the MVP.

## Consequences
*   **Positive:** Drastically simplifies the infrastructure and `docker-compose.yml` (one DB to rule them all). Simplifies backup, migrations, and transaction management. SQL is universally understood, making the codebase easier to teach and explain.
*   **Negative:** `pgvector` performance at massive scale (millions of chunks) might lag behind specialized DBs like Qdrant, but for the MVP corpus (~190 pages / ~2000 chunks), it is more than sufficient and performant. Requires the Postgres Docker image to have the `pgvector` extension pre-installed.