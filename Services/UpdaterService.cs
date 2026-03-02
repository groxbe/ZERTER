using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace VERTER.Services
{
    public class UpdaterService
    {
        private readonly HttpClient _httpClient;

        public UpdaterService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ZERTER-App");
        }

        public string GetLocalVersion()
        {
            try
            {
                // version.txt dosyasını okuyoruz
                string path = Path.Combine(AppContext.BaseDirectory, "version.txt");
                return File.Exists(path) ? File.ReadAllText(path).Trim() : "1.0.0";
            }
            catch { return "1.0.0"; }
        }

        public async Task<string?> GetRemoteVersionAsync()
        {
            try
            {
                // GitHub üzerinden version.txt dosyasını çekiyoruz
                string url = "https://raw.githubusercontent.com/groxbe/ZERTER/main/version.txt";
                var response = await _httpClient.GetStringAsync(url);
                return response?.Trim();
            }
            catch { return null; }
        }

        public async Task PerformUpdateAsync(Action<double> progressCallback)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "ZERTER_Update");
            if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
            Directory.CreateDirectory(tempPath);

            string zipUrl = "https://github.com/groxbe/ZERTER/archive/refs/heads/main.zip";
            string zipPath = Path.Combine(Path.GetTempPath(), "ZERTER_Update.zip");

            using (var response = await _httpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead))
            using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var totalRead = 0L;
                var buffer = new byte[8192];
                var isMoreToRead = true;

                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    do
                    {
                        var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (read == 0)
                        {
                            isMoreToRead = false;
                        }
                        else
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;
                            if (totalBytes != -1)
                                progressCallback?.Invoke((double)totalRead / totalBytes * 100);
                        }
                    } while (isMoreToRead);
                }
            }

            // Create the update script
            string batchPath = Path.Combine(Path.GetTempPath(), "ZERTER_Update.bat");
            string installDir = AppContext.BaseDirectory;

            string script = $@"
@echo off
timeout /t 2 /nobreak > nul
taskkill /f /im ZERTER.exe > nul
powershell -Command ""Expand-Archive -Path '{zipPath}' -DestinationPath '{tempPath}' -Force; $extDir = Join-Path '{tempPath}' 'ZERTER-main'; Get-ChildItem -Path $extDir | Where-Object {{ $_.Name -ne 'publish' }} | Copy-Item -Destination '{installDir}' -Recurse -Force; $pubDir = Join-Path $extDir 'publish'; if (Test-Path $pubDir) {{ Copy-Item -Path ($pubDir + '\*') -Destination '{installDir}' -Recurse -Force }}""
del ""{zipPath}""
start /d ""{installDir}"" ZERTER.exe
del ""%~f0""
";
            File.WriteAllText(batchPath, script);

            Process.Start(new ProcessStartInfo
            {
                FileName = batchPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            Environment.Exit(0);
        }
    }
}
