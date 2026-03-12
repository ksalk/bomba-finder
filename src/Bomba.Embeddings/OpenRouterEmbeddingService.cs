using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using Pgvector;

namespace Bomba.Embeddings;

public class OpenRouterEmbeddingService
{
    private const string OpenRouterApiUrl = "https://openrouter.ai/api/v1/embeddings";
    private const string EmbeddingModel = "openai/text-embedding-3-large";
    private const int BatchSize = 10;
    private readonly HttpClient HttpClient = new();

    public OpenRouterEmbeddingService(string openRouterApiKey = null)
    {
        var apiKey = openRouterApiKey ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(
                "OPENROUTER_API_KEY environment variable is not set. " +
                "Please add your OpenRouter API key to the .env file.");
        }

        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        HttpClient.DefaultRequestHeaders.Add("X-Title", "Bomba Transcript Fetcher");
    }

    public async Task<List<Vector>> GenerateEmbeddingsAsync(List<string> texts)
    {
        var allEmbeddings = new List<Vector>();
        var totalBatches = (int)Math.Ceiling(texts.Count / (double)BatchSize);

        for (var i = 0; i < texts.Count; i += BatchSize)
        {
            var batchNumber = i / BatchSize + 1;
            var batch = texts.Skip(i).Take(BatchSize).ToList();
            
            //Console.WriteLine($"[EMBEDDING] Processing batch {batchNumber}/{totalBatches} ({batch.Count} texts)...");

            var batchEmbeddings = await GenerateBatchEmbeddingsWithRetryAsync(batch);
            allEmbeddings.AddRange(batchEmbeddings);

            // Rate limiting - small delay between batches
            if (i + BatchSize < texts.Count)
            {
                await Task.Delay(100);
            }
        }

        return allEmbeddings;
    }

    private async Task<List<Vector>> GenerateBatchEmbeddingsWithRetryAsync(List<string> batch, int maxRetries = 3)
    {
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await GenerateBatchEmbeddingsAsync(batch);
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    throw new InvalidOperationException(
                        $"Failed to generate embeddings after {maxRetries} attempts: {ex.Message}", ex);
                }

                var delayMs = attempt * 1000;
                Console.WriteLine($"[EMBEDDING] Attempt {attempt} failed, retrying in {delayMs}ms...");
                await Task.Delay(delayMs);
            }
        }

        throw new InvalidOperationException("Unexpected error in retry loop");
    }

    private async Task<List<Vector>> GenerateBatchEmbeddingsAsync(List<string> batch)
    {
        var requestBody = new
        {
            model = EmbeddingModel,
            input = batch
        };

        var response = await HttpClient.PostAsJsonAsync(OpenRouterApiUrl, requestBody);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"OpenRouter API returned {(int)response.StatusCode}: {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(responseJson);

        if (embeddingResponse?.Data == null)
        {
            throw new InvalidOperationException("Failed to deserialize embedding response");
        }

        // Ensure embeddings are in the same order as input
        var sortedEmbeddings = embeddingResponse.Data
            .OrderBy(d => d.Index)
            .Select(d => new Vector(d.Embedding.ToArray()))
            .ToList();

        return sortedEmbeddings;
    }

    private class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData> Data { get; set; } = [];
    }

    private class EmbeddingData
    {
        [JsonPropertyName("embedding")]
        public List<float> Embedding { get; set; } = [];

        [JsonPropertyName("index")]
        public int Index { get; set; }
    }
}
