using YoutubeExplode.Videos;
using YoutubeExplode;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;
using YoutubeSubScraper.Persistence;

namespace YoutubeSubScraper
{
    public static class AzureSpeechStudioSubtitlesProvider
    {
        public static async Task<List<BombaSubtitles>> Provide(string videoUrl)
        {
            var youtube = new YoutubeClient();
            // Get the video ID from the URL
            var videoId = VideoId.Parse(videoUrl);

            // Get video information
            var video = await youtube.Videos.GetAsync(videoId);

            // Get video manifest
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoId);

            // Select the best audio-only stream
            var audioStreamInfo = streamManifest.GetAudioOnlyStreams().OrderByDescending(s => s.Bitrate).FirstOrDefault();

            if(audioStreamInfo is null)
            {
                Console.WriteLine($"No suitable audio stream found for: {video.Title}");
                return [];
            }

            // Download the audio stream to a file
            var audioFilePathMp3 = Path.Combine(Environment.CurrentDirectory, $"audio_{videoId}.mp3");
            await youtube.Videos.Streams.DownloadAsync(audioStreamInfo, audioFilePathMp3);

            Console.WriteLine($"Audio downloaded successfully: {audioFilePathMp3}");

            var audioFilePathWav = Path.ChangeExtension(audioFilePathMp3, "wav");
            ConvertMp3ToWav(audioFilePathMp3 , audioFilePathWav);

            string subscriptionKey = "X";
            string region = "eastus";

            var config = SpeechConfig.FromSubscription(subscriptionKey, region);
            config.SpeechRecognitionLanguage = "pl-PL";
            config.SetProfanity(ProfanityOption.Raw);
            config.OutputFormat = OutputFormat.Detailed;
            await RecognizeSpeechFromAudioFileAsync(config, audioFilePathWav);
            // add response to cache or db to not get it again and use azure resources         

            return [];
        }

        static async Task RecognizeSpeechFromAudioFileAsync(SpeechConfig config, string audioFilePath)
        {
            // Configure the audio input for the recognizer to use the MP3 file
            using var audioInput = AudioConfig.FromWavFileInput(audioFilePath); // WAV file
                                                                                // If using MP3, you need to create the right audio stream.

            using var recognizer = new SpeechRecognizer(config, audioInput);

            Console.WriteLine("Recognizing...");
            // Subscribing to events
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
        }

        public static void ConvertMp3ToWav(string mp3File, string wavFile)
        {
            using (var reader = new MediaFoundationReader(mp3File))
            using (var writer = new WaveFileWriter(wavFile, reader.WaveFormat))
            {
                reader.CopyTo(writer);
            }
        }
    }
}
