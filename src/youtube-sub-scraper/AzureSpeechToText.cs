using System.Text;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
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
        var config = SpeechConfig.FromSubscription(_azureSubscriptionKey, _azureRegion);
        config.SpeechRecognitionLanguage = "pl-PL";
        config.SetProfanity(ProfanityOption.Raw);
        config.OutputFormat = OutputFormat.Detailed;
        return await RecognizeSpeechFromAudioWavFileAsync(config, filePath);
    }
    
    private static async Task<List<BombaSubtitles>> RecognizeSpeechFromAudioWavFileAsync(SpeechConfig config, string audioWavFilePath)
    {
        using var audioInput = AudioConfig.FromWavFileInput(audioWavFilePath);

        using var recognizer = new SpeechRecognizer(config, audioInput);
        var stopRecognition = new TaskCompletionSource<int>();
        var subtitles = new List<BombaSubtitles>();
        
        recognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                subtitles.Add(new BombaSubtitles("test", "test", e.Result.Text, TimeSpan.FromMicroseconds(e.Offset / 100)));
                Console.WriteLine($"Recognized: {e.Result.Text}");
                Console.WriteLine($"Result: {e.Result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult)}");
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                Console.WriteLine("No speech could be recognized.");
            }
        };

        recognizer.Canceled += (s, e) =>
        {
            Console.WriteLine($"CANCELED: Reason={e.Reason}");

            if (e.Reason == CancellationReason.Error)
            {
                Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
                Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                Console.WriteLine("CANCELED: Did you update the subscription info?");
            }

            stopRecognition.TrySetResult(0);
        };

        recognizer.SessionStopped += (s, e) =>
        {
            Console.WriteLine("Session stopped.");
            stopRecognition.TrySetResult(0);
        };

        await recognizer.StartContinuousRecognitionAsync();

        // Waits for completion.
        Task.WaitAny(new[] { stopRecognition.Task });

        await recognizer.StopContinuousRecognitionAsync();
        return subtitles;
    }
}