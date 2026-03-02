using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using VERTER.Models;

namespace VERTER.Services
{
    public class DownloadService
    {
        private readonly YoutubeClient _youtube;
        private readonly FFmpegService _ffmpegService;
        private readonly SettingsService _settingsService;

        public DownloadService()
        {
            _youtube = new YoutubeClient();
            _ffmpegService = new FFmpegService();
            _settingsService = new SettingsService();
        }

        public bool FFmpegReady() => _ffmpegService.IsReady();
        public async Task<bool> EnsureFFmpegAsync(Action<double> progress) => await _ffmpegService.EnsureFFmpegAsync(progress);

        public async Task PopulateMetadataAsync(DownloadItem item)
        {
            try
            {
                var video = await _youtube.Videos.GetAsync(item.Url);
                string title = video.Title;
                string artist = video.Author.ChannelTitle;

                if (title.Contains(" - "))
                {
                    var parts = title.Split(" - ", 2);
                    artist = parts[0].Trim();
                    title = parts[1].Trim();
                }

                item.Title = CleanTitle(title);
                item.Artist = artist;
            }
            catch { }
        }

        public async Task DownloadMp3Async(DownloadItem item, Action<double> progressCallback)
        {
            try
            {
                if (!_ffmpegService.IsReady())
                {
                    item.Status = "FFmpeg Hazırlanıyor...";
                    var ok = await _ffmpegService.EnsureFFmpegAsync(p => progressCallback(p * 0.1));
                    if (!ok) 
                    {
                        item.Status = "FFmpeg Hatası!";
                        return;
                    }
                }

                if (item.Title == "Arayan..." || item.Artist == "...")
                {
                    await PopulateMetadataAsync(item);
                }

                var downloadsPath = _settingsService.DownloadPath;
                if (!Directory.Exists(downloadsPath)) Directory.CreateDirectory(downloadsPath);

                var fileName = $"{CleanFileName(item.Artist)} - {CleanFileName(item.Title)}.mp3";
                var filePath = Path.Combine(downloadsPath, fileName);
                item.FileName = fileName;
                item.FilePath = filePath;

                if (File.Exists(filePath))
                {
                    item.Status = "Zaten Var";
                    item.Progress = 100;
                    return;
                }

                var manifest = await _youtube.Videos.Streams.GetManifestAsync(item.Url);
                var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

                item.Status = "İndiriliyor...";
                await _youtube.Videos.Streams.DownloadAsync(streamInfo, tempPath, new Progress<double>(p => progressCallback(p * 0.9)));

                item.Status = "Dönüştürülüyor...";
                progressCallback(0.95);
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegService.GetFFmpegPath(),
                    Arguments = $"-y -loglevel error -i \"{tempPath}\" -ab 320k \"{filePath}\"",
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        // Add a timeout just in case it takes too long
                        var completedTask = await Task.WhenAny(process.WaitForExitAsync(), Task.Delay(TimeSpan.FromMinutes(5)));
                        if (completedTask != process.WaitForExitAsync())
                        {
                            process.Kill();
                            item.Status = "Hata: Dönüştürme zaman aşımı";
                            return;
                        }
                    }
                }

                if (File.Exists(tempPath)) File.Delete(tempPath);

                item.Status = "Tamamlandı";
                item.Progress = 100;
            }
            catch (Exception ex)
            {
                item.Status = "Hata: " + ex.Message;
            }
        }

        public async Task DownloadPlaylistAsync(string playlistUrl, Action<DownloadItem> onItemAdded, Action<DownloadItem, double> onProgress)
        {
            try
            {
                var playlist = await _youtube.Playlists.GetAsync(playlistUrl);
                var videos = _youtube.Playlists.GetVideosAsync(playlist.Id);

                await foreach (var video in videos)
                {
                    var videoUrl = $"https://www.youtube.com/watch?v={video.Id}";
                    
                    string title = video.Title;
                    string artist = video.Author.ChannelTitle;

                    if (title.Contains(" - "))
                    {
                        var parts = title.Split(" - ", 2);
                        artist = parts[0].Trim();
                        title = parts[1].Trim();
                    }

                    var item = new DownloadItem 
                    { 
                        Url = videoUrl, 
                        Title = CleanTitle(title), 
                        Artist = artist 
                    };
                    onItemAdded(item);
                }
            }
            catch { }
        }

        private string CleanTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return title;

            string previousTitle;
            do
            {
                previousTitle = title;
                // Remove everything inside (), [], {}, <>
                title = Regex.Replace(title, @"\([^\)]*\)", "", RegexOptions.IgnoreCase);
                title = Regex.Replace(title, @"\[[^\]]*\]", "", RegexOptions.IgnoreCase);
                title = Regex.Replace(title, @"\{[^\}]*\}", "", RegexOptions.IgnoreCase);
                title = Regex.Replace(title, @"\<[^\>]*\>", "", RegexOptions.IgnoreCase);
            } while (title != previousTitle);

            // Targeted cleanup for leftover standalone terms
            string[] junk = { 
                "Official Video", "Official Audio", "Music Video",
                "OFFICIAL MUSIC VIDEO", "Lyrics Video", "Official Lyric Video",
                "Official", "Lyric", "Lyrics", "Video", "Audio", "4K", "HD", "HQ", "1080p", "720p",
                "Original Mix", "Extended Mix", "Remix", "Cover", "Live", "Performance",
                "Clip", "Full HD", "Premiere", "Prod.", "prod by", "ft.", "feat."
            };

            foreach (var s in junk)
            {
                title = Regex.Replace(title, @"\b" + Regex.Escape(s) + @"\b", "", RegexOptions.IgnoreCase);
            }

            // Clean up special characters and extra delimiters
            title = title.Replace("|", "").Replace(":", "").Replace("\"", "").Replace("*", "");
            
            // Final trimming of spaces and leftover dashes/dots
            title = title.Trim('-', ' ', '.', '_', ' ', '|', ':');

            // Collapse multiple spaces
            title = Regex.Replace(title, @"\s+", " ");

            return title.Trim();
        }

        private string CleanFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
