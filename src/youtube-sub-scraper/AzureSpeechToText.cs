using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Serilog;
using YoutubeExplode.Videos;
using YoutubeSubScraper.Persistence;

namespace YoutubeSubScraper;

public static class AzureSpeechToText
{
    private static string _azureSubscriptionKey { get; set; }
    private static string _azureRegion => "eastus";

    public static void SetSubscriptionKey(string subscriptionKey)
    {
        if(string.IsNullOrWhiteSpace(subscriptionKey))
            throw new ArgumentNullException(nameof(subscriptionKey), "Azure Subscription Key cannot be null or empty.");
        _azureSubscriptionKey = subscriptionKey;
    }
    
    public static async Task<List<BombaSubtitles>> ProcessSpeechFromWavFile(string filePath)
    {
        var config = GetSpeechRecognitionConfig();
        return await RecognizeSpeechFromAudioWavFileAsync(config, filePath);
    }

    private static SpeechConfig GetSpeechRecognitionConfig()
    {
        if(string.IsNullOrWhiteSpace(_azureSubscriptionKey))
            throw new ArgumentNullException(nameof(_azureSubscriptionKey), "Azure Subscription Key cannot be null or empty.");
        
        var config = SpeechConfig.FromSubscription(_azureSubscriptionKey, _azureRegion);
        config.SpeechRecognitionLanguage = "pl-PL";
        config.SetProfanity(ProfanityOption.Raw);
        config.OutputFormat = OutputFormat.Detailed;
        return config;
    }

    private static async Task<List<BombaSubtitles>> RecognizeSpeechFromAudioWavFileAsync(SpeechConfig config, string audioWavFilePath)
    {
        using var audioInput = AudioConfig.FromWavFileInput(audioWavFilePath);

        using var recognizer = new SpeechRecognizer(config, audioInput);
        var stopRecognition = new TaskCompletionSource<int>();
        var subtitles = new List<BombaSubtitles>();
        
        recognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
            {
                subtitles.Add(new BombaSubtitles(string.Empty, string.Empty, new VideoId(), e.Result.Text, TimeSpan.FromTicks((long)e.Offset)));
                Log.Logger.Debug($"Recognized: {e.Result.Text} with offset: {e.Offset}");
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                Log.Logger.Debug("No speech could be recognized.");
            }
        };

        recognizer.Canceled += (s, e) =>
        {
            Log.Logger.Debug($"CANCELED: Reason={e.Reason}");

            if (e.Reason == CancellationReason.Error)
            {
                Log.Logger.Debug($"CANCELED: ErrorCode={e.ErrorCode}");
                Log.Logger.Debug($"CANCELED: ErrorDetails={e.ErrorDetails}");
                Log.Logger.Debug("CANCELED: Did you update the subscription info?");
            }

            stopRecognition.TrySetResult(0);
        };

        recognizer.SessionStopped += (s, e) =>
        {
            Log.Logger.Debug("Speech Recognition Session stopped.");
            stopRecognition.TrySetResult(0);
        };

        await recognizer.StartContinuousRecognitionAsync();

        Task.WaitAny(new[] { stopRecognition.Task });

        await recognizer.StopContinuousRecognitionAsync();
        return subtitles;
    }
}