using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Input;

namespace VERTER.Services
{
    public class ClipboardMonitorService
    {
        private string _lastClipboardText = string.Empty;
        public event Action<string>? OnYouTubeUrlDetected;

        private static readonly Regex YouTubeRegex = new Regex(
            @"(?:https?:\/\/)?(?:www\.|m\.|music\.)?(?:youtube\.com\/(?:watch\?v=|v\/|embed\/|shorts\/)|youtu\.be\/)([a-zA-Z0-9_-]{11})(?:[&?]list=([a-zA-Z0-9_-]+))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public async Task StartMonitoringAsync()
        {
            while (true)
            {
                await Task.Delay(2000); // 2 second delay between ticks for stability
                try
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var text = await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            var clipboard = desktop.MainWindow?.Clipboard;
#pragma warning disable CS0618
                            return clipboard != null ? await clipboard.GetTextAsync() : null;
#pragma warning restore CS0618
                        });

                        if (string.IsNullOrWhiteSpace(text)) 
                        {
                            _lastClipboardText = string.Empty;
                            continue;
                        }

                        // Case-insensitive comparison and trim for robustness
                        text = text.Trim();
                        if (text.Equals(_lastClipboardText, StringComparison.OrdinalIgnoreCase)) continue;

                        if (IsYouTubeUrl(text))
                        {
                            _lastClipboardText = text;
                            OnYouTubeUrlDetected?.Invoke(text);
                            
                            // Extra sleep after detection to let the clipboard "settle" and avoid double triggers
                            await Task.Delay(1000);
                        }
                        else
                        {
                            // If it's not a YT url, still update last text so we don't keep checking it
                            _lastClipboardText = text;
                        }
                    }
                }
                catch
                {
                    // Ignore errors during polling
                }
            }
        }

        private bool IsYouTubeUrl(string url)
        {
            return YouTubeRegex.IsMatch(url);
        }
    }
}
