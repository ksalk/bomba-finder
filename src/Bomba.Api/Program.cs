using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Bomba.DB;
using Bomba.Embeddings;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

builder.Services.AddSingleton(_ => new OpenRouterEmbeddingService(Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")!));
builder.Services.AddScoped<BombaDbContext>();
builder.Services.AddScoped<ScriptFinder>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("search", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromSeconds(10);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 2;
    });
});

var app = builder.Build();

app.UseCors("AllowAll");
app.UseRateLimiter();

var enableWebUi = Environment.GetEnvironmentVariable("ENABLE_WEB_UI")?.ToLower() != "false";

if (enableWebUi)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapGet("/api/search", async (string query, ScriptFinder scriptFinder) =>
{
    if (string.IsNullOrWhiteSpace(query))
    {
        return Results.BadRequest(new { error = "Query parameter is required" });
    }

    const int maxQueryLength = 64;
    if (query.Length > maxQueryLength)
    {
        return Results.BadRequest(new { error = $"Query parameter cannot exceed {maxQueryLength} characters" });
    }

    try
    {
        var result = await scriptFinder.GetBestResultForQuery(query.ToLower());
        var videoId = ExtractVideoId(result.VideoUrl);
        var timestampSeconds = (int)result.ChunkStartTime.TotalSeconds;

        var response = new
        {
            videoTitle = result.VideoTitle,
            videoUrl = $"{result.VideoUrl}&t={timestampSeconds}",
            embedUrl = $"https://www.youtube.com/embed/{videoId}?start={timestampSeconds}",
            timestamp = timestampSeconds,
            //similarityScore = result.SimilarityScore,
            thumbnailUrl = $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg",
            videoId = videoId,
            chunkText = result.ChunkText
        };

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Search failed: {ex.Message}");
    }
}).RequireRateLimiting("search");

if (enableWebUi)
{
    app.MapFallback(async (HttpContext context) =>
    {
        var indexPath = Path.Combine(app.Environment.WebRootPath, "index.html");
        if (File.Exists(indexPath))
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(indexPath);
        }
        else
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("Not found");
        }
    });
}

app.Run();

static string ExtractVideoId(string url)
{
    var match = Regex.Match(url, @"(?:v=|/)([0-9A-Za-z_-]{11}).*");
    return match.Success ? match.Groups[1].Value : string.Empty;
}
