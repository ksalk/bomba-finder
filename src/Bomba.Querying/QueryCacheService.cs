using Bomba.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Bomba.Querying;

public class QueryCacheService(
    IMemoryCache memoryCache,
    BombaDbContext dbContext,
    IServiceScopeFactory scopeFactory)
{
    private static readonly MemoryCacheEntryOptions L1CacheOptions = new()
    {
        Size = 1,
        Priority = CacheItemPriority.High
    };

    public async Task<Guid?> GetAsync(string query)
    {
        var normalizedQuery = TextNormalizer.Normalize(query);

        // Check L1 (Memory Cache)
        if (memoryCache.TryGetValue(normalizedQuery, out Guid cachedChunkId))
        {
            // Fire-and-forget update of access stats in L2 using a dedicated scope to avoid concurrent DbContext access
            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BombaDbContext>();
                await UpdateAccessStatsAsync(db, normalizedQuery);
            });
            return cachedChunkId;
        }

        // Check L2 (Postgres)
        var entry = await dbContext.QueryCacheEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.NormalizedQuery == normalizedQuery);

        if (entry != null)
        {
            // Populate L1
            memoryCache.Set(normalizedQuery, entry.ScriptChunkId, L1CacheOptions);

            // Fire-and-forget update of access stats using a dedicated scope to avoid concurrent DbContext access
            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BombaDbContext>();
                await UpdateAccessStatsAsync(db, normalizedQuery);
            });

            return entry.ScriptChunkId;
        }

        return null;
    }

    public async Task SetAsync(string query, Guid scriptChunkId)
    {
        var normalizedQuery = TextNormalizer.Normalize(query);
        var now = DateTime.UtcNow;

        // Save to L1
        memoryCache.Set(normalizedQuery, scriptChunkId, L1CacheOptions);

        // Save to L2 (upsert)
        var existingEntry = await dbContext.QueryCacheEntries
            .FirstOrDefaultAsync(e => e.NormalizedQuery == normalizedQuery);

        if (existingEntry != null)
        {
            existingEntry.ScriptChunkId = scriptChunkId;
            existingEntry.LastAccessedAt = now;
            existingEntry.AccessCount++;
        }
        else
        {
            dbContext.QueryCacheEntries.Add(new QueryCacheEntry
            {
                NormalizedQuery = normalizedQuery,
                ScriptChunkId = scriptChunkId,
                CreatedAt = now,
                LastAccessedAt = now,
                AccessCount = 1
            });
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task<SearchResult> GetOrCreateAsync(
        string query,
        Func<Task<SearchResult>> factory)
    {
        var cachedChunkId = await GetAsync(query);

        if (cachedChunkId.HasValue)
        {
            var cachedResult = await GetSearchResultByChunkId(cachedChunkId.Value);
            if (cachedResult != null)
            {
                Console.WriteLine($"[CACHE] Cache hit for query: \"{query}\" -> ChunkId: {cachedChunkId.Value}");
                return cachedResult;
            }
        }

        Console.WriteLine($"[CACHE] Cache miss for query: \"{query}\" - running search");

        var result = await factory();

        await SetAsync(query, result.ChunkId);

        return result;
    }

    private async Task<SearchResult?> GetSearchResultByChunkId(Guid chunkId)
    {
        var chunk = await dbContext.ScriptChunks
            .AsNoTracking()
            .Include(sc => sc.VideoScript)
            .FirstOrDefaultAsync(sc => sc.Id == chunkId);

        if (chunk == null)
        {
            return null;
        }

        return new SearchResult
        {
            ChunkId = chunk.Id,
            ChunkText = chunk.Text,
            ChunkStartTime = chunk.Start,
            VideoId = chunk.VideoScript.VideoId,
            VideoTitle = chunk.VideoScript.Title,
            VideoUrl = chunk.VideoScript.VideoUrl,
            SimilarityScore = 1.0 // Cached results have perfect "similarity"
        };
    }

    private async Task UpdateAccessStatsAsync(BombaDbContext db, string normalizedQuery)
    {
        try
        {
            await db.QueryCacheEntries
                .Where(e => e.NormalizedQuery == normalizedQuery)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.LastAccessedAt, DateTime.UtcNow)
                    .SetProperty(e => e.AccessCount, e => e.AccessCount + 1));
        }
        catch (Exception ex)
        {
            // Silently fail - cache stats are not critical
            Console.WriteLine($"[CACHE] Failed to update access stats: {ex.Message}");
        }
    }
}
