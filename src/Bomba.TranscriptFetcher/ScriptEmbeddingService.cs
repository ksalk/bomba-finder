using Bomba.DB;
using Bomba.Embeddings;
using Microsoft.EntityFrameworkCore;

namespace Bomba.TranscriptFetcher;

public class ScriptEmbeddingService(BombaDbContext dbContext, OpenRouterEmbeddingService embeddingService)
{
    public async Task GenerateAndStoreEmbeddingsAsync()
    {
        Console.WriteLine("[EMBEDDING SERVICE] Starting embedding generation process...");

        var scriptChunks = await dbContext.ScriptChunks.Where(sc => sc.Embedding == null).ToListAsync();

        var iterator = 0;
        foreach (var scriptChunk in scriptChunks.Chunk(10))
        {
            var chunkList = scriptChunk.ToList();
            var texts = chunkList.Select(c => c.Text).ToList();
            var embeddings = await embeddingService.GenerateEmbeddingsAsync(texts);

            for (int i = 0; i < chunkList.Count; i++)
            {
                chunkList[i].Embedding = embeddings[i];
            }

            dbContext.ScriptChunks.UpdateRange(chunkList);
            await dbContext.SaveChangesAsync();
            iterator++;

            Console.WriteLine($"[EMBEDDING SERVICE] Processed {iterator * 10}/{scriptChunks.Count} script chunks...");
        }

        Console.WriteLine("[EMBEDDING SERVICE] Embedding generation process completed.");
    }
}
