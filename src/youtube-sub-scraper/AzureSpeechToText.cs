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

        Console.WriteLine("Recognizing...");
        recognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                Console.WriteLine($"Recognized: {e.Result.Text}");
                Console.WriteLine("JSON Response:");
                Console.WriteLine(e.Result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult));
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
                Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
            }
        };

        recognizer.SessionStopped += (s, e) =>
        {
            Console.WriteLine("Session stopped. Stopping recognition.");
        };

        // Start continuous recognition
        Console.WriteLine("Processing the WAV file...");
        await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

        // Wait until the session stops or the entire file is processed
        Console.WriteLine("Recognition started. Press any key to stop...");
        Console.ReadKey();

        // Stop recognition
        await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
        return new List<BombaSubtitles>();
    }
}