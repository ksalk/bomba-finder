using System.Text.Json;
using Bomba.DB;
using Bomba.Embeddings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .Build();

var EXTRACT_SCRIPTS = configuration.GetValue<bool>("FeatureFlags:ExtractScripts", true);
var CHUNK_SCRIPTS = configuration.GetValue<bool>("FeatureFlags:ChunkScripts", false);
var GENERATE_EMBEDDINGS = configuration.GetValue<bool>("FeatureFlags:GenerateEmbeddings", false);
var EXTRACT_ONLY_MISSING = configuration.GetValue<bool>("FeatureFlags:ExtractOnlyMissing", true);
var SHOW_SKIP_INFO = configuration.GetValue<bool>("FeatureFlags:ShowSkipInfo", false);

var openRouterApiKey = configuration.GetValue<string>("OpenRouterApiKey");

var bombaDb = new BombaDbContext();
await bombaDb.Database.EnsureCreatedAsync();

var scriptExtractor = new ScriptExtractor(bombaDb);
var scriptChunker = new ScriptChunker(bombaDb);
var embeddingService = new OpenRouterEmbeddingService(openRouterApiKey);
var scriptFinder = new ScriptFinder(bombaDb, embeddingService);

if (EXTRACT_SCRIPTS)
{
    Console.WriteLine("[MAIN] Starting script extraction process...");
    var videoPlaylists = new[]
    {
        "https://www.youtube.com/playlist?list=PLHtUOYOPwzJGGZkjR-FspIL17YtSBGaCR", // Kapitan Bomba
        "https://www.youtube.com/playlist?list=PLHtUOYOPwzJEC3FKnhMtBGgXaITMR21U3", // Laserowy Gniew Dzidy
        "https://www.youtube.com/playlist?list=PLdhsyudOIKSaIGhF4ul0_eOxsoLXOwWRB" // Galaktyczne Lektury
    };

    foreach (var videoPlaylist in videoPlaylists)
    {
        await scriptExtractor.ExtractScriptsForPlaylist(videoPlaylist, EXTRACT_ONLY_MISSING, SHOW_SKIP_INFO);
    }
}

if (CHUNK_SCRIPTS)
{
    await scriptChunker.GetScriptChunks();
}

if (GENERATE_EMBEDDINGS)
{
    await GenerateAndStoreEmbeddingsAsync(bombaDb);
}

async Task GenerateAndStoreEmbeddingsAsync(BombaDbContext dbContext)
{
    Console.WriteLine("[MAIN] Starting embedding generation process...");

    var scriptChunks = await dbContext.ScriptChunks.Where(sc => sc.Embedding == null).ToListAsync();

    var iterator = 0;
    foreach (var scriptChunk in scriptChunks.Chunk(10))
    {
        await embeddingService.GenerateEmbeddingsForScriptChunksAsync(scriptChunk.ToList());
        iterator++;

        Console.WriteLine($"[MAIN] Processed {iterator * 10}/{scriptChunks.Count} script chunks...");

    }

    Console.WriteLine("[MAIN] Embedding generation process completed.");
}

async Task ExportBombaDbToJson(string filePath)
{
    var allScripts = bombaDb.VideoScripts.ToList();
    var json = JsonSerializer.Serialize(allScripts, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(filePath, json);
}
