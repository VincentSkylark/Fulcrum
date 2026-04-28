**JSONB column on the Event.** Here's the pattern:

```sql
-- Fixed columns: identity, relationships, common fields
CREATE TABLE news.Events (
    Id            UUID PRIMARY KEY,
    Category      VARCHAR(100) NOT NULL,      -- "sports", "tech", "politics"
    Summary       TEXT NOT NULL,
    OccurredAt    TIMESTAMPTZ,
    CreatedAt     TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Flexible columns: varies by category, evolves freely
    Who           JSONB DEFAULT '{}',         -- [{"name":"Tesla","type":"org"}]
    What          JSONB DEFAULT '{}',
    Location      JSONB DEFAULT '{}',         -- {"city":"SF","country":"US"}
    Metadata      JSONB DEFAULT '{}'          -- category-specific catch-all
);
```

### How this works

**Fixed columns** for what every event has: category, summary, timestamp, who/what/where. These get regular indexes and are your primary query filters.

**`Metadata` JSONB** for what varies by category:
- Sports: `{"league": "NBA", "teams": ["Lakers", "Celtics"], "score": "112-105"}`
- Tech: `{"company": "Apple", "product": "iPhone 17", "event_type": "launch"}`
- Politics: `{"country": "US", "election": "2026 midterms", "politician": "..."}`

You add new fields by updating the LLM extraction prompt — zero database migrations.

### Query performance

```sql
-- GIN index makes JSONB queries fast
CREATE INDEX ix_events_metadata ON news.Events USING GIN (Metadata jsonb_path_ops);

-- Query by any metadata field
SELECT * FROM news.Events
WHERE Metadata @> '{"company": "Apple"}';

-- Query by category + metadata
SELECT * FROM news.Events
WHERE Category = 'tech' AND Metadata->>'product' = 'iPhone 17';
```

### When to promote a field out of JSONB

If a metadata field becomes a hot filter (queried in most requests), promote it to a real column:

```sql
ALTER TABLE news.Events ADD COLUMN League VARCHAR(50);
CREATE INDEX ix_events_league ON news.Events (League);
```

The rule: **JSONB for flexibility, real columns for performance.** Promote when query patterns stabilize.

### Schema validation stays in code, not DDL

Define expected metadata shapes per category in your application:

```csharp
public sealed record CategorySchema(
    string Category,
    string[] RequiredKeys,
    string[] OptionalKeys);

private static readonly CategorySchema[] Schemas =
[
    new("sports", ["league", "teams"], ["score", "player"]),
    new("tech", ["company"], ["product", "version", "event_type"]),
];
```

The LLM extracts, your code validates against the schema, PostgreSQL stores. No migration needed when you add `"stadium"` to the sports schema — just update the code.

### Why not EAV or separate tables per category

- **EAV** (key-value rows) — queries require self-joins, terrible performance, unreadable SQL
- **Table-per-category** — a new table every time you add a category, ORM nightmare, can't query across categories
- **JSONB** — one table, indexed, queryable, evolves without migrations, PostgreSQL has been optimizing it for 10+ years

**Bottom line:** Fixed columns for identity and relationships, JSONB for category-specific metadata, promote to real columns when query patterns demand it. One schema, one database, no migrations for metadata changes.