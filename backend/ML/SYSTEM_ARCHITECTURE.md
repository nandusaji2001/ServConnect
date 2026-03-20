# System Architecture - Multimodal Lost & Found

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Frontend (Views)                         │
│                  LostAndFound/*.cshtml                           │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ HTTP
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    C# Backend (ASP.NET Core)                     │
│                                                                  │
│  ┌────────────────────────────────────────────────────────┐    │
│  │         LostAndFoundController.cs                       │    │
│  │  - ReportLost()                                         │    │
│  │  - ReportFound()                                        │    │
│  │  - SuggestedMatches()                                   │    │
│  └────────────────────────────────────────────────────────┘    │
│                              │                                   │
│  ┌────────────────────────────────────────────────────────┐    │
│  │         LostAndFoundService.cs                          │    │
│  │  - FindMatchingLostItems()                              │    │
│  │  - FindMatchingFoundItems()                             │    │
│  └────────────────────────────────────────────────────────┘    │
│                              │                                   │
│  ┌────────────────────────────────────────────────────────┐    │
│  │         ItemMatchingService.cs                          │    │
│  │  - FindMatchingLostItemsAsync()                         │    │
│  │  - FindMatchingFoundItemsAsync()                        │    │
│  │  - ComputeSimilarityAsync()                             │    │
│  └────────────────────────────────────────────────────────┘    │
│                              │                                   │
└──────────────────────────────┼───────────────────────────────────┘
                              │ HTTP POST
                              │ http://localhost:5003/match
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              Python ML API (Flask) - Port 5003                   │
│         multimodal_item_matching_api.py                          │
│                                                                  │
│  Endpoints:                                                      │
│  - POST /match          → Find matching items                   │
│  - POST /similarity     → Compute similarity                    │
│  - POST /trust_scores   → Compute GNN trust scores              │
│  - GET  /health         → Health check                          │
│  - POST /embed/image    → Get image embedding                   │
│  - POST /embed/text     → Get text embedding                    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│           MultimodalMatchingService (Orchestrator)               │
│         multimodal_matching_service.py                           │
│                                                                  │
│  - compute_multimodal_similarity()                               │
│  - find_matches()                                                │
│  - compute_final_score()                                         │
│  - initialize_gnn()                                              │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ CLIPService  │    │  GNNService  │    │ SBERTModel   │
│              │    │              │    │              │
│ clip_service │    │ gnn_service  │    │ sentence_    │
│     .py      │    │     .py      │    │ transformers │
└──────────────┘    └──────────────┘    └──────────────┘
```

## Component Details

### 1. CLIP Service (clip_service.py)

```
┌─────────────────────────────────────────────────────────┐
│                    CLIPService                           │
├─────────────────────────────────────────────────────────┤
│ Model: openai/clip-vit-base-patch32                     │
│ Embedding Dimension: 512                                │
├─────────────────────────────────────────────────────────┤
│ Methods:                                                 │
│  • encode_image(image) → [512-dim vector]               │
│  • encode_text(text) → [512-dim vector]                 │
│  • compute_image_image_similarity(img1, img2) → float   │
│  • compute_text_text_similarity(txt1, txt2) → float     │
│  • compute_image_text_similarity(img, txt) → float      │
├─────────────────────────────────────────────────────────┤
│ Input Formats:                                           │
│  • Image: URL, file path, bytes, base64                 │
│  • Text: String                                          │
└─────────────────────────────────────────────────────────┘
```

### 2. GNN Service (gnn_service.py)

```
┌─────────────────────────────────────────────────────────┐
│                    GNNService                            │
├─────────────────────────────────────────────────────────┤
│ Architecture: GraphSAGE or GAT                          │
│ Framework: PyTorch Geometric                            │
├─────────────────────────────────────────────────────────┤
│ Graph Structure:                                         │
│                                                          │
│  Nodes:                                                  │
│    • Users (features: posts, reports, age)              │
│    • Items (features: claims, verified, age)            │
│                                                          │
│  Edges:                                                  │
│    • User → Item (posted)                               │
│    • User → Item (claimed/viewed)                       │
│    • Bidirectional                                       │
├─────────────────────────────────────────────────────────┤
│ Methods:                                                 │
│  • build_graph(users, items, interactions) → Data       │
│  • initialize_model(input_dim)                          │
│  • compute_embeddings() → Tensor                        │
│  • compute_trust_scores() → Dict[str, float]            │
│  • get_user_trust_score(user_id) → float                │
│  • get_item_trust_score(item_id) → float                │
└─────────────────────────────────────────────────────────┘
```

### 3. Multimodal Matching Service

```
┌─────────────────────────────────────────────────────────┐
│           MultimodalMatchingService                      │
├─────────────────────────────────────────────────────────┤
│ Scoring Formula:                                         │
│                                                          │
│  similarity_score = 0.4×image_sim                       │
│                   + 0.4×text_sim                        │
│                   + 0.2×cross_modal_sim                 │
│                                                          │
│  trust_score = (user_trust + item_trust) / 2            │
│                                                          │
│  final_score = α×similarity_score + β×trust_score       │
│                                                          │
│  Default: α=0.7, β=0.3                                  │
├─────────────────────────────────────────────────────────┤
│ Methods:                                                 │
│  • compute_multimodal_similarity(item1, item2)          │
│  • compute_final_score(sim, trust, α, β)                │
│  • find_matches(query, candidates, ...)                 │
│  • initialize_gnn(users, items, interactions)           │
└─────────────────────────────────────────────────────────┘
```

## Data Flow

### Scenario 1: Text-Only Matching (Backward Compatible)

```
User Reports Found Item
         │
         ▼
┌─────────────────┐
│ C# Controller   │
│ ReportFound()   │
└─────────────────┘
         │
         ▼
┌─────────────────┐
│ C# Service      │
│ FindMatching    │
│ LostItems()     │
└─────────────────┘
         │
         ▼
┌─────────────────┐
│ ItemMatching    │
│ Service.cs      │
│ HTTP POST       │
└─────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│ Python API                           │
│ POST /match                          │
│                                      │
│ {                                    │
│   "query_item": {                    │
│     "title": "Black Wallet",         │
│     "description": "..."             │
│   },                                 │
│   "candidate_items": [...]           │
│ }                                    │
└─────────────────────────────────────┘
         │
         ▼
┌─────────────────┐
│ CLIP Service    │
│ encode_text()   │
│ compute_sim()   │
└─────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│ Response                             │
│                                      │
│ {                                    │
│   "matches": [                       │
│     {                                │
│       "item": {...},                 │
│       "similarity_score": 0.85,      │
│       "final_score": 0.85            │
│     }                                │
│   ]                                  │
│ }                                    │
└─────────────────────────────────────┘
         │
         ▼
┌─────────────────┐
│ C# Service      │
│ Returns matches │
│ to Controller   │
└─────────────────┘
         │
         ▼
┌─────────────────┐
│ View            │
│ Display matches │
└─────────────────┘
```

### Scenario 2: Multimodal Matching (Images + Text)

```
User Reports Found Item with Photo
         │
         ▼
┌─────────────────────────────────────┐
│ Python API                           │
│ POST /match                          │
│                                      │
│ {                                    │
│   "query_item": {                    │
│     "title": "Black Wallet",         │
│     "images": ["url1"]               │
│   },                                 │
│   "candidate_items": [               │
│     {                                │
│       "title": "Lost Wallet",        │
│       "images": ["url2"]             │
│     }                                │
│   ]                                  │
│ }                                    │
└─────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│ Multimodal Matching Service          │
│                                      │
│ For each candidate:                  │
│   1. Compute image similarity        │
│   2. Compute text similarity         │
│   3. Compute cross-modal similarity  │
│   4. Combine scores                  │
└─────────────────────────────────────┘
         │
    ┌────┴────┬────────────┐
    ▼         ▼            ▼
┌────────┐ ┌────────┐ ┌────────────┐
│ Image  │ │ Text   │ │ Cross-     │
│ ↔      │ │ ↔      │ │ Modal      │
│ Image  │ │ Text   │ │ Image↔Text │
└────────┘ └────────┘ └────────────┘
    │         │            │
    └────┬────┴────────────┘
         ▼
┌─────────────────────────────────────┐
│ Combined Similarity                  │
│                                      │
│ 0.4×image + 0.4×text + 0.2×cross    │
└─────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│ Response with detailed scores        │
│                                      │
│ {                                    │
│   "matches": [                       │
│     {                                │
│       "similarity_score": 0.85,      │
│       "details": {                   │
│         "image_similarity": 0.90,    │
│         "text_similarity": 0.80,     │
│         "cross_modal": 0.85          │
│       }                              │
│     }                                │
│   ]                                  │
│ }                                    │
└─────────────────────────────────────┘
```

### Scenario 3: Full System (Multimodal + GNN)

```
Request with User/Item Metadata
         │
         ▼
┌─────────────────────────────────────┐
│ Python API                           │
│ POST /match                          │
│                                      │
│ {                                    │
│   "query_item": {...},               │
│   "candidate_items": [...],          │
│   "users": [                         │
│     {                                │
│       "id": "user1",                 │
│       "posts_count": 10,             │
│       "reports_count": 0,            │
│       "account_age_days": 365        │
│     }                                │
│   ],                                 │
│   "items_metadata": [...],           │
│   "interactions": [...]              │
│ }                                    │
└─────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│ Step 1: Initialize GNN               │
│                                      │
│ Build graph from:                    │
│  - User nodes                        │
│  - Item nodes                        │
│  - Interaction edges                 │
└─────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│ Step 2: Train GNN                    │
│                                      │
│ GraphSAGE/GAT forward pass           │
│ Compute node embeddings              │
│ Extract trust scores                 │
└─────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│ Step 3: Compute Similarities         │
│                                      │
│ For each candidate:                  │
│  - Multimodal similarity (CLIP)      │
│  - User trust score (GNN)            │
│  - Item trust score (GNN)            │
└─────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│ Step 4: Compute Final Scores         │
│                                      │
│ For each candidate:                  │
│   trust = (user_trust + item_trust)/2│
│   final = α×similarity + β×trust     │
└─────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│ Step 5: Rank and Return              │
│                                      │
│ Sort by final_score descending       │
│ Return top_k matches                 │
└─────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│ Response with full details           │
│                                      │
│ {                                    │
│   "matches": [                       │
│     {                                │
│       "similarity_score": 0.85,      │
│       "trust_score": 0.75,           │
│       "final_score": 0.82,           │
│       "details": {                   │
│         "image_similarity": 0.90,    │
│         "text_similarity": 0.80,     │
│         "user_trust": 0.80,          │
│         "item_trust": 0.70           │
│       }                              │
│     }                                │
│   ],                                 │
│   "gnn_enabled": true                │
│ }                                    │
└─────────────────────────────────────┘
```

## GNN Graph Structure

```
┌─────────────────────────────────────────────────────────┐
│                    User-Item Graph                       │
└─────────────────────────────────────────────────────────┘

    User Nodes                    Item Nodes
    
    ┌─────────┐                  ┌─────────┐
    │ User 1  │                  │ Item 1  │
    │         │                  │         │
    │ posts:10│──────posted─────▶│claims:2 │
    │ rpts: 0 │                  │verified │
    │ age:365 │                  │age: 5   │
    └─────────┘                  └─────────┘
         │                            ▲
         │                            │
         │                         claimed
         │                            │
         │                            │
    ┌─────────┐                  ┌─────────┐
    │ User 2  │                  │ Item 2  │
    │         │                  │         │
    │ posts: 3│──────posted─────▶│claims:8 │
    │ rpts: 5 │                  │not verif│
    │ age: 30 │                  │age: 15  │
    └─────────┘                  └─────────┘
         │                            ▲
         │                            │
         └──────────viewed────────────┘

Node Features:
  Users: [posts_count, reports_count, account_age_days, is_user=1]
  Items: [claims_count, verified, age_days, is_item=0]

Edge Types:
  • User → Item (posted)
  • User → Item (claimed)
  • User → Item (viewed)
  • Bidirectional

GNN Output:
  • User embeddings → Trust scores
  • Item embeddings → Reliability scores
```

## Scoring Visualization

```
┌─────────────────────────────────────────────────────────┐
│              Final Score Computation                     │
└─────────────────────────────────────────────────────────┘

Similarity Score (0.85)
├─ Image Similarity (0.90) ──────────────────────┐
├─ Text Similarity (0.80) ────────────────────┐  │
└─ Cross-Modal Similarity (0.85) ──────────┐  │  │
                                           │  │  │
                                           ▼  ▼  ▼
                                    ┌──────────────┐
                                    │  Weighted    │
                                    │  Average     │
                                    │ 0.4+0.4+0.2  │
                                    └──────────────┘
                                           │
                                           ▼
                                    Similarity: 0.85

Trust Score (0.75)
├─ User Trust (0.80) ────────────────────────┐
└─ Item Trust (0.70) ─────────────────────┐  │
                                          │  │
                                          ▼  ▼
                                    ┌──────────────┐
                                    │   Average    │
                                    │  (0.80+0.70) │
                                    │      /2      │
                                    └──────────────┘
                                           │
                                           ▼
                                      Trust: 0.75

Final Score
┌──────────────┐        ┌──────────────┐
│ Similarity   │        │    Trust     │
│    0.85      │        │    0.75      │
└──────────────┘        └──────────────┘
       │                       │
       │ α=0.7                 │ β=0.3
       │                       │
       ▼                       ▼
    0.595                   0.225
       │                       │
       └───────────┬───────────┘
                   ▼
            ┌──────────────┐
            │ Final Score  │
            │    0.82      │
            └──────────────┘
```

## Model Architecture

### CLIP Model

```
Input: Image (224×224) or Text (77 tokens)
         │
         ▼
┌─────────────────────────────────────┐
│     Vision Transformer (ViT)        │
│     or Text Transformer             │
│                                     │
│  12 layers, 512 hidden dim          │
│  8 attention heads                  │
└─────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│     Projection Layer                │
└─────────────────────────────────────┘
         │
         ▼
    512-dim embedding
    (normalized)
```

### GNN Model (GraphSAGE)

```
Input: Node features + Edge index
         │
         ▼
┌─────────────────────────────────────┐
│     SAGEConv Layer 1                │
│     input_dim → 64                  │
│     Aggregation: mean               │
└─────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│     ReLU + Dropout (0.3)            │
└─────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│     SAGEConv Layer 2                │
│     64 → 32                         │
│     Aggregation: mean               │
└─────────────────────────────────────┘
         │
         ▼
    32-dim node embeddings
         │
         ▼
    Sigmoid → Trust scores (0-1)
```

## Deployment Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Production Setup                      │
└─────────────────────────────────────────────────────────┘

┌─────────────┐
│   Browser   │
└─────────────┘
       │
       │ HTTPS
       ▼
┌─────────────┐
│  IIS/Nginx  │
└─────────────┘
       │
       │ HTTP
       ▼
┌─────────────────────────────────────┐
│   ASP.NET Core Backend              │
│   (ItemMatchingService.cs)          │
└─────────────────────────────────────┘
       │
       │ HTTP (localhost)
       ▼
┌─────────────────────────────────────┐
│   Python ML API (Flask)             │
│   Port 5003                         │
│                                     │
│   ┌─────────────────────────────┐  │
│   │  CLIP Model (~600MB)        │  │
│   │  GNN Model (~50MB)          │  │
│   │  SBERT Model (~90MB)        │  │
│   └─────────────────────────────┘  │
└─────────────────────────────────────┘
       │
       │ (Optional)
       ▼
┌─────────────────────────────────────┐
│   MongoDB                           │
│   (Cache embeddings)                │
└─────────────────────────────────────┘
```

This architecture provides a scalable, modular, and maintainable system for multimodal lost and found item matching with trust scoring!
