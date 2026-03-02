using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace VERTER.Services
{
    public class FFmpegService
    {
        private readonly string _ffmpegPath;

        public FFmpegService()
        {
            var appDir = AppContext.BaseDirectory;
            _ffmpegPath = Path.Combine(appDir, "ffmpeg.exe");
        }

        public string GetFFmpegPath() => _ffmpegPath;

        public bool IsReady() => File.Exists(_ffmpegPath);

        public async Task<bool> EnsureFFmpegAsync(Action<double>? progressCallback = null)
        {
            if (IsReady()) return true;

            try
            {
                // Download a minimal ffmpeg build if possible. 
                // Using a direct link to a known small build or common distribution.
                // For this example, we'll try to download from a reliable source.
                // Note: In a real app, you might want to host this yourself to ensure availability.
                string downloadUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
                
                using var client = new HttpClient();
                var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream("ffmpeg.zip", FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                var totalRead = 0L;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;
                    if (totalBytes != -1)
                        progressCallback?.Invoke((double)totalRead / totalBytes);
                }

                fileStream.Close();

                // Extract only ffmpeg.exe
                using (ZipArchive archive = ZipFile.OpenRead("ffmpeg.zip"))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            entry.ExtractToFile(_ffmpegPath, true);
                            break;
                        }
                    }
                }

                File.Delete("ffmpeg.zip");
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
