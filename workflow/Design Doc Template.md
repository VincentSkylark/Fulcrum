Since **Fulcrum.News** is the "engine" of your platform, it serves as the best template. This design doc is intended to live in your repository (e.g., `docs/modules/news-module.md`) to give you and your AI agents a clear blueprint.

---

# Design Doc: Fulcrum.News Module

## 1. Mission & Responsibility
The **News Module** is the primary entry point for data. It is responsible for the lifecycle of content before it becomes a recommendation.

* **Owns:** News sources (RSS/API), article metadata, raw content storage, and deduplication logic.
* **Delegates:** User-specific "read" states (to **Analytics**) and personalized ranking (to **Recommendations**).

## 2. Internal Architecture: Vertical Slice (VSA)
We use **VSA** here because news ingestion is a linear pipeline. Each "Feature" (e.g., `AddSource`, `ProcessRssFeed`, `SearchArticles`) contains its own logic, DTOs, and persistence code.

### The Ingestion Pipeline (Flow)
1.  **Trigger:** Hangfire `RecurringJob` calls `FetchSourceHandler`.
2.  **Extraction:** `HtmlSanitizer` cleans the body; `MetadataExtractor` finds thumbnails.
3.  **Deduplication:** Generate a `ContentHash`. Check `news.Articles` for existing hash within a 48-hour window.
4.  **Persistence:** Save to PostgreSQL `news` schema.
5.  **Broadcast:** Publish `ArticleIngestedIntegrationEvent` to the message bus.

## 3. Data Schema (`news` schema)

| Table | Purpose | Key Columns / Indexes |
| :--- | :--- | :--- |
| **Sources** | External providers | `Url`, `ProviderType`, `LastFetchedAt`, `IsActive` (Index: `IsActive`) |
| **Articles** | Stored content | `Title`, `Body`, `SourceUrl`, `ContentHash`, `PublishedAt` (Index: `PublishedAt`, `ContentHash`) |
| **ArticleVectors** | AI embeddings | `ArticleId`, `Embedding` (Type: `vector(1536)`, Index: `HNSW`) |



## 4. Integration Contract

### Published Events (Outgoing)
* **`ArticleIngestedEvent`**: 
    * *Payload:* `{ ArticleId, SourceId, Category, TitleSummary }`
    * *Subscribers:* `Recommendations` (to update vectors), `Notifications` (to check for breaking news alerts).
* **`SourceFailedEvent`**: 
    * *Payload:* `{ SourceId, Error, StatusCode }`
    * *Subscribers:* `Admin` (to alert the dashboard).

### Consumed Events (Incoming)
* **`ArticleModeratedEvent`**:
    * *Action:* Mark article as `IsHidden` or `Deleted` based on Admin action.

## 5. Implementation Details & Constraints

* **Concurrency:** Max 10 concurrent scrapers to avoid IP blocking. Use a `SemaphoreSlim` in the `SourceProcessor`.
* **Resiliency:** Use **Polly** for retries on HTTP 5xx errors.
* **Deduplication Logic:** ```csharp
    // ContentHash = SHA256(Title.ToLower().Trim() + PublishedAt.Date)
    ```
* **AI Context:** When using Claude Code to modify this module, ensure all new endpoints implement `IEndpointGroup` and use `FluentValidation`.

---

## 🛠️ How to use this template
For your other modules, follow this same pattern but swap the specifics:

1.  **Fulcrum.Auth:** Focus on the **Kratos Webhook** handling and **Identity Mapping**.
2.  **Fulcrum.Recommendations:** Focus on the **Vector Search logic** and how it weights "Vantage" (diversity) vs. "Similarity."
3.  **Fulcrum.Notifications:** Focus on the **Provider Abstraction** (FCM, SendGrid) and **Rate Limiting**.

Does this level of detail feel right for your current workflow, or should we add a section for **Frontend Requirements** as well?