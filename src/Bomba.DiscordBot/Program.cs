using System.Text.RegularExpressions;
using Bomba.DB;
using Discord;
using Discord.WebSocket;

class Program
{
    private static DiscordSocketClient? _client;

    static async Task Main(string[] args)
    {
        var discordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        if (string.IsNullOrEmpty(discordToken))
        {
            throw new InvalidOperationException("DISCORD_TOKEN environment variable is not set");
        }

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages
        };

        _client = new DiscordSocketClient(config);
        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.SlashCommandExecuted += SlashCommandExecutedAsync;

        await _client.LoginAsync(TokenType.Bot, discordToken);
        await _client.StartAsync();

        await Task.Delay(-1);
    }

    private static Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }

    private static async Task ReadyAsync()
    {
        Console.WriteLine("Zalogowano pomyślnie");

        var bombaCommand = new SlashCommandBuilder()
            .WithName("bomba")
            .WithDescription("Podaj cytat")
            .AddOption("text", ApplicationCommandOptionType.String, "Tekst do wyszukania", isRequired: true);

        await _client!.CreateGlobalApplicationCommandAsync(bombaCommand.Build());
    }

    private static async Task SlashCommandExecutedAsync(SocketSlashCommand command)
    {
        if (command.CommandName == "bomba")
        {
            var text = (string)command.Data.Options.First().Value;
            Console.WriteLine($"Otrzymano komendę bomba z tekstem: {text}");

            await using var db = new BombaDbContext();
            var result = await ScriptFinder.GetBestResultForQuery(db, text.ToLower());

            Console.WriteLine($"Najlepszy wynik to {result.VideoTitle} z dopasowaniem {result.SimilarityScore:F3}");

            var embed = new EmbedBuilder()
                .WithTitle($"Szukam \"{text}\"")
                .WithDescription($"Wynik znaleziono z dopasowaniem {result.SimilarityScore * 100:F1}%")
                .WithColor(Color.Blue);

            var videoId = ExtractVideoId(result.VideoUrl);
            var thumbnailUrl = $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg";
            embed.WithThumbnailUrl(thumbnailUrl);

            var timestampSeconds = (int)result.ChunkStartTime.TotalSeconds;
            embed.AddField(result.VideoTitle, $"[{result.VideoTitle}]({result.VideoUrl}&t={timestampSeconds})", inline: true);

            await command.RespondAsync(embed: embed.Build());
        }
    }

    private static string ExtractVideoId(string url)
    {
        var match = Regex.Match(url, @"(?:v=|/)([0-9A-Za-z_-]{11}).*");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
