# ADR-002: Chunking & Retrieval Strategy

## Status
Accepted

## Context
EquipFlow ingests specialized industrial documents (Equipment Manuals, SOPs, Safety Procedures, Troubleshooting Guides). To satisfy **FR-014 (Grounded Answer)**, **FR-015 (Citations)**, and **FR-016 (Refusal)**, the retrieval pipeline must provide highly relevant, context-rich evidence to the LLM. 

The ITI brief mandates a deliberate chunking strategy justified by document structure, hybrid retrieval with a documented fusion method, and metadata filtering (**FR-007, FR-012, FR-013**). Furthermore, industrial documents are highly structured (hierarchical headers, numbered steps); naive chunking destroys the context of safety procedures.

## Decision
We will implement a **Structure-Aware Chunking** strategy combined with **Hybrid Retrieval (Dense + Keyword)** using **Reciprocal Rank Fusion (RRF)**.

### 1. Chunking Strategy: Structure-Aware (Hierarchical)
*   **Primary Method:** Parse documents and split chunks based on structural boundaries (Headers H1-H3, numbered lists for SOP steps). 
*   **Context Injection:** Every chunk will inherit parent headers as contextual prefixes (e.g., `[Manual: P-101] > [Section: Safety] > Step 1...`).
*   **Fallback:** Fixed-size chunking (500 tokens, 50 overlap) only for unstructured plain-text sections.
*   **Metadata Preservation (FR-008, FR-009):** Every chunk will carry strict metadata: `DocumentId`, `DocumentType`, `EquipmentId`, `ProductionLine`, `Version`, `Page/Section`.

### 2. Retrieval Strategy: Hybrid Search with Metadata Pre-filtering
*   **Dense Retrieval:** Vector similarity (Cosine) using an embedding model to capture semantic intent (e.g., "overheating" matches "high temperature").
*   **Keyword Retrieval:** Full-text search (BM25) to capture exact technical terms, equipment IDs (e.g., "P-101"), and error codes.
*   **Metadata Filtering (FR-013):** Apply SQL-like pre-filtering (e.g., `WHERE EquipmentId = 'P-101' AND Version = 'v2.1'`) *before* vector/keyword search to ensure revision-awareness and prevent cross-equipment hallucination.

### 3. Fusion Method: Reciprocal Rank Fusion (RRF)
*   Instead of weighted scoring (which requires tuning arbitrary weights between vector distance and BM25 scores), we will use RRF: `score = sum(1 / (k + rank_i))`. RRF is robust, parameter-light, and an industry standard for hybrid search.

## Alternatives Considered
1.  **Fixed-Size Chunking Only:** Rejected. Destroys the sequential nature of SOPs and safety prerequisites, risking dangerous out-of-context retrieval.
2.  **Weighted Score Fusion:** Rejected. Vector cosine similarity (0 to 1) and BM25 scores (0 to infinity) have different distributions; weighting them requires brittle tuning. RRF relies on rank, not raw scores.
3.  **Cross-Encoder Re-ranking:** Deferred. While it improves precision, it adds latency and complexity. We will implement basic Hybrid+RRF first and add Re-ranking only if the Evaluation Harness (FR-3) proves retrieval hit-rate is insufficient (Documented in SDD Gap Table).

## Consequences
*   **Positive:** High-quality, context-aware retrieval. Strong defense against cross-equipment hallucination via metadata filtering. Citations map cleanly to exact chunks and sections.
*   **Negative:** Ingestion pipeline requires a document parser capable of extracting structural hierarchy (e.g., Markdown headers or PDF layout analysis) rather than just reading raw text.