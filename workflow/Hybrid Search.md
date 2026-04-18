# Hybrid Search

Combining keyword search results with semantic recommendations  -- **Hybrid Search**.

In 2026, you don't need to be a search engine expert to do this well. You can use a proven mathematical shortcut called **Reciprocal Rank Fusion (RRF)**. It’s the industry standard for merging results from different systems (like Meilisearch and pgvector) without worrying about their different scoring math.

### 1. Is Meilisearch Enough?
**Yes, but with a specific strategy.**
As of early 2026, Meilisearch has native **Hybrid Search** capabilities. You can send it a keyword *and* a vector in one request. It will handle the merging for you using its own internal "Semantic Ratio."

**However**, since your recommendation vectors are "User Preference Spectrums" stored in PostgreSQL, you have two choices:

* **The "All-in-Meili" Path:** You push the articles to Meilisearch and, at search time, you send the User's Preference Vector as part of the search query. Meilisearch handles the fusion.
* **The "Architect's Hybrid" Path (Recommended):** You pull the top 50 matches from Meilisearch (Keywords) and the top 50 from pgvector (Recommendations) and merge them in your `.NET` code using **RRF**.

---

### 2. The "Secret Sauce": Reciprocal Rank Fusion (RRF)
RRF is a simple formula that allows you to combine lists based on **rank** rather than **score**. This is perfect because a "Meilisearch Score" (0–1000) and a "pgvector Distance" (0–1) are like comparing apples to spaceships.

**The Formula:**
$$RRFscore(d) = \sum_{r \in R} \frac{1}{k + rank(d, r)}$$
*(Usually $k = 60$. If a document is #1 in a list, its rank is 1.)*



---

### 3. C# Implementation Pattern
In your `Fulcrum.API`, your search service would look like this:

```csharp
public async Task<List<Article>> SearchVantageAsync(string query, Guid userId)
{
    // 1. Get Top 50 by Keywords (Meilisearch)
    var keywordResults = await _meiliClient.SearchAsync(query, limit: 50);

    // 2. Get Top 50 by Personalization (pgvector via Recommendations Module)
    var userVector = await _prefStore.GetVectorAsync(userId);
    var recommendedResults = await _db.Articles
        .OrderBy(a => a.Embedding.L2Distance(userVector))
        .Take(50).ToListAsync();

    // 3. Fuse the results
    var fusedScores = new Dictionary<Guid, double>();
    
    // Rank keyword results
    for (int i = 0; i < keywordResults.Count; i++) {
        var id = keywordResults[i].Id;
        fusedScores[id] = fusedScores.GetValueOrDefault(id) + (1.0 / (60 + (i + 1)));
    }

    // Rank recommendation results
    for (int i = 0; i < recommendedResults.Count; i++) {
        var id = recommendedResults[i].Id;
        fusedScores[id] = fusedScores.GetValueOrDefault(id) + (1.0 / (60 + (i + 1)));
    }

    // 4. Return sorted by fused score
    return await _db.Articles
        .Where(a => fusedScores.Keys.Contains(a.Id))
        .OrderByDescending(a => fusedScores[a.Id])
        .ToListAsync();
}
```

---

### 4. Why this is the "Senior" Move for Vantage
* **Safety:** If Meilisearch goes down, the user still gets recommendations. If the recommendation engine is buggy, they still get keyword results.
* **No "Tuning" Required:** Unlike weighted averages (e.g., "60% keywords / 40% AI"), RRF works out of the box with no configuration. It naturally favors items that appear in **both** lists.
* **The "Show me a different angle" Toggle:** If the user clicks "Different Angle," you just use the **bottom** 50 results from pgvector (highest distance) and fuse those with the keywords instead.

### Final Architect Assessment
By using **Meilisearch** for the "lexical" (word) search and **pgvector** for the "semantic" (meaning) search, and merging them with **RRF**, you are building a search experience that rivals professional news platforms. You've effectively bypassed the need for a Ph.D. in Search Engineering while maintaining a "Senior Architect" level of system reliability.

Does this "merge-in-C#" approach feel more manageable than trying to configure a single massive search engine?