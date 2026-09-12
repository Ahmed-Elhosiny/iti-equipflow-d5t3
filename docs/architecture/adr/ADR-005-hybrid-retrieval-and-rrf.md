# ADR-005 — Hybrid Retrieval and Reciprocal Rank Fusion

## Status
Accepted

## Context
EquipFlow must satisfy **FR-012** by retrieving both exact technical identifiers and semantically related concepts from industrial documents. Exact terms such as equipment identifiers (`P-101`), line names, error codes, and revision values are important, while user-described symptoms may use language that differs from the source document.

The retrieval path must also respect strict document boundaries before evidence is passed to the LLM. The ITI brief identifies a justified retrieval enhancement for **FR-2**; the MVP must improve retrieval quality without introducing the latency and token cost of a heavy re-ranking model, in alignment with the **T3 Cost Governor** constraints.

## Decision
We will use **Hybrid Retrieval** combining dense vector search and PostgreSQL keyword search, with results fused using **Reciprocal Rank Fusion (RRF)**.

### 1. Retrieval sources
- **Dense retrieval:** use `pgvector` `CosineDistance` over document chunk embeddings to capture semantic concepts, such as a symptom described as "overheating" matching a source phrase such as "high temperature".
- **Keyword retrieval:** use PostgreSQL Full-Text Search over a `tsvector` column to preserve exact-term matching for equipment identifiers, error codes, line names, and other technical vocabulary.
- Both retrieval paths apply the same strict metadata filters at the database query level: equipment, production line, document type, and version. Filtering occurs before results are used for LLM generation, preventing cross-equipment or cross-revision evidence from entering the context.

### 2. Result fusion
The two ranked result sets will be combined with Reciprocal Rank Fusion using the standard constant `k = 60`:

`RRFScore(document) = sum(1 / (60 + rank_i(document)))`

RRF is chosen because it is robust, requires no score normalization, and works well when combining heterogeneous scoring systems such as cosine distance and PostgreSQL full-text relevance. The rank-based calculation also produces stable, deterministic fusion without brittle weighting between the two sources.

### 3. Justified retrieval enhancement
For **FR-2**, the justified retrieval enhancement is **strict metadata filtering**, implemented in the database query rather than through a heavy cross-encoder re-ranker. The filters are applied to equipment, line, document type, and version before dense and keyword candidates are fused.

### 4. Re-ranking in the MVP
The MVP will provide a `NoOpReranker` implementation to satisfy the `IRerankerPort` contract while preserving the retrieval pipeline's extension point. Semantic re-ranking is deferred to a future iteration so the MVP avoids additional latency and AI token/cost overhead, consistent with T3 Cost Governor constraints.

## Alternatives Considered
1. **Dense retrieval only:** Rejected. Semantic similarity alone can miss exact identifiers, error codes, and revision-specific terms.
2. **Keyword retrieval only:** Rejected. Full-text matching alone is less effective when users describe symptoms using different terminology from the source documents.
3. **Weighted score fusion:** Rejected. Cosine-distance and full-text relevance scores have different scales and distributions, making weights difficult to normalize and tune reliably.
4. **Cross-encoder re-ranking in the MVP:** Deferred. It may improve precision, but adds model latency and token/cost overhead. The `NoOpReranker` keeps the `IRerankerPort` contract available while evaluation evidence can guide a future decision.

## Consequences
- **Positive:** Improved recall for specific equipment identifiers such as `P-101` and for semantic concepts.
- **Positive:** RRF provides stable, deterministic fusion without score normalization.
- **Positive:** Database-level metadata filtering enforces strict data boundaries before LLM generation.
- **Positive:** Deferring semantic re-ranking reduces MVP latency and AI token/cost overhead.
- **Negative:** Hybrid retrieval requires maintaining both embedding/vector and PostgreSQL full-text indexes, plus the associated query paths.
- **Risk:** A future corpus or evaluation result may justify semantic re-ranking; the `IRerankerPort` and `NoOpReranker` preserve a controlled extension point for that iteration.
