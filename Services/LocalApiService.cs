using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using VERTER.Models;
using System.Collections.ObjectModel;
using System.IO;

namespace VERTER.Services
{
    public class LocalApiService
    {
        private HttpListener? _listener;
        private readonly ObservableCollection<DownloadItem> _waitingList;
        private readonly ObservableCollection<DownloadItem> _downloadingList;
        private readonly ObservableCollection<DownloadItem> _downloadedList;

        public LocalApiService(
            ObservableCollection<DownloadItem> waitingList,
            ObservableCollection<DownloadItem> downloadingList,
            ObservableCollection<DownloadItem> downloadedList)
        {
            _waitingList = waitingList;
            _downloadingList = downloadingList;
            _downloadedList = downloadedList;
        }

        public void Start()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://localhost:9001/");
                _listener.Start();
                Task.Run(() => HandleRequests());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Local API Error: {ex.Message}");
            }
        }

        private async Task HandleRequests()
        {
            while (_listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    var request = context.Request;
                    var response = context.Response;

                    // Add CORS headers
                    response.AddHeader("Access-Control-Allow-Origin", "*");
                    response.AddHeader("Access-Control-Allow-Methods", "GET, OPTIONS");

                    if (request.HttpMethod == "OPTIONS")
                    {
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.Close();
                        continue;
                    }

                    string status = "none";
                    string? url = request.QueryString["url"];
                    string version = "1.0.0";

                    // version.txt okuma
                    try {
                        string vPath = Path.Combine(AppContext.BaseDirectory, "version.txt");
                        if (File.Exists(vPath)) version = File.ReadAllText(vPath).Trim();
                    } catch { }

                    if (request.Url?.AbsolutePath == "/version")
                    {
                        string versionJson = "{\"version\": \"" + version + "\"}";
                        byte[] vBuffer = Encoding.UTF8.GetBytes(versionJson);
                        response.ContentType = "application/json";
                        response.ContentLength64 = vBuffer.Length;
                        await response.OutputStream.WriteAsync(vBuffer, 0, vBuffer.Length);
                        response.Close();
                        continue;
                    }

                    if (!string.IsNullOrEmpty(url))
                    {
                        status = GetUrlStatus(url);
                    }

                    string jsonResponse = "{\"status\": \"" + status + "\", \"version\": \"" + version + "\"}";
                    byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                    response.ContentType = "application/json";
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.Close();
                }
                catch { }
            }
        }

        private string GetUrlStatus(string url)
        {
            var vid = YoutubeExplode.Videos.VideoId.TryParse(url);
            if (vid == null) return "none";
            string targetId = vid.Value;

            lock (_downloadingList)
            {
                if (_downloadingList.Any(i => TryGetId(i.Url) == targetId)) return "downloading";
            }
            lock (_waitingList)
            {
                if (_waitingList.Any(i => TryGetId(i.Url) == targetId)) return "waiting";
            }
            lock (_downloadedList)
            {
                if (_downloadedList.Any(i => TryGetId(i.Url) == targetId)) return "downloaded";
            }

            return "none";
        }

        private string? TryGetId(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            return YoutubeExplode.Videos.VideoId.TryParse(url)?.Value;
        }

        public void Stop()
        {
            _listener?.Stop();
            _listener = null;
        }
    }
}
