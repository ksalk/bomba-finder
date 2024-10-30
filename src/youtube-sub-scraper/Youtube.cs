﻿using NAudio.Wave;
using YoutubeExplode;
using YoutubeExplode.Playlists;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;
using YoutubeSubScraper.Persistence;

namespace YoutubeSubScraper;

public static class Youtube
{
    public static async Task<List<string>> GetVideoUrlsFromPlaylistUrl(string playlistUrl)
    {
        var youtube = new YoutubeClient();
        try
        {
            var playlistId = PlaylistId.Parse(playlistUrl);
            var videos = await youtube.Playlists.GetVideosAsync(playlistId);

            return videos
                .Select(video => $"https://www.youtube.com/watch?v={video.Id}")
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred in {nameof(GetVideoUrlsFromPlaylistUrl)} method: {ex.Message}");
        }
        return new List<string>();
    }
    
    public static async Task<List<BombaSubtitles>> GetCaptionsForVideo(string videoUrl)
    {
        try
        {
            var youtube = new YoutubeClient();
            var videoId = VideoId.Parse(videoUrl);
            var video = await youtube.Videos.GetAsync(videoId);

            var trackManifest = await youtube.Videos.ClosedCaptions.GetManifestAsync(videoId);

            var trackInfo = trackManifest.Tracks.FirstOrDefault(t => t.Language.Code == "pl" && !t.IsAutoGenerated);
            var tracksLanguages = string.Join(", ", trackManifest.Tracks.Select(x => x.Language)) ?? "";
            
            Console.WriteLine($"{video.Title}\nFound subtitles: {tracksLanguages}\n\n");

            if (trackInfo != null)
            {
                var track = await youtube.Videos.ClosedCaptions.GetAsync(trackInfo);

                return track
                    .Captions
                    .Select(c => new BombaSubtitles(video.Title, videoUrl, c.Text, c.Offset))
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred in {nameof(GetCaptionsForVideo)} method: {ex.Message}");
        }
        return [];
    }
    
    public static async Task<string> SaveAudioToWavFile(string videoUrl)
    {
        var youtube = new YoutubeClient();
        var videoId = VideoId.Parse(videoUrl);
        var video = await youtube.Videos.GetAsync(videoId);

        var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoId);
        var audioStreamInfo = streamManifest
            .GetAudioOnlyStreams()
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault();

        if(audioStreamInfo is null)
        {
            Console.WriteLine($"No suitable audio stream found for: {video.Title}");
            return string.Empty;
        }

        // Download the audio stream to a file
        var audioFilePathMp3 = Path.Combine(Environment.CurrentDirectory, $"audio_{videoId}.mp3");
        await youtube.Videos.Streams.DownloadAsync(audioStreamInfo, audioFilePathMp3);

        Console.WriteLine($"Audio downloaded successfully: {audioFilePathMp3}");

        var audioFilePathWav = Path.ChangeExtension(audioFilePathMp3, "wav");
        ConvertMp3ToWav(audioFilePathMp3 , audioFilePathWav);

        return audioFilePathWav;

        // // Get this from config file
        // string subscriptionKey = "X";
        // string region = "eastus";
        //
        // var config = SpeechConfig.FromSubscription(subscriptionKey, region);
        // config.SpeechRecognitionLanguage = "pl-PL";
        // config.SetProfanity(ProfanityOption.Raw);
        // config.OutputFormat = OutputFormat.Detailed;
        // await RecognizeSpeechFromAudioFileAsync(config, audioFilePathWav);
        // // add response to cache or db to not get it again and use azure resources         
        //
        // return [];
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