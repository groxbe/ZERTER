using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using VERTER.Models;
using VERTER.Services;

namespace VERTER.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly DownloadService _downloadService;
        private readonly ClipboardMonitorService _clipboardService;
        private readonly SettingsService _settingsService;
        private readonly PlayerService _playerService;
        private readonly UpdaterService _updaterService;

        [ObservableProperty] private ObservableCollection<DownloadItem> _waitingList = new();
        [ObservableProperty] private ObservableCollection<DownloadItem> _downloadingList = new();
        [ObservableProperty] private ObservableCollection<DownloadItem> _downloadedList = new();

        [ObservableProperty] private string _downloadPath;
        [ObservableProperty] private DownloadItem? _currentSong;
        [ObservableProperty] private bool _isPlaying;
        [ObservableProperty] private double _playerProgress;
        [ObservableProperty] private float _volume = 1.0f;
        [ObservableProperty] private string _playPauseIcon = "▶";
        
        [ObservableProperty] private bool _showInTaskbar = true;
        [ObservableProperty] private bool _isMinimized = false;
        [ObservableProperty] private bool _isAboutVisible = false;
        [ObservableProperty] private string _updateStatusText = "Güncellemeleri Denetle";
        [ObservableProperty] private double _updateProgress = 0;
        [ObservableProperty] private bool _isUpdating = false;
        public string AppVersion => _updaterService?.GetLocalVersion() ?? "1.0.0";
        public string DeveloperName => "GROXBE";
        public string LastUpdateDate => "02.03.2026 15:30";

        private int _activeDownloads = 0;
        private const int MaxConcurrentDownloads = 3;
        private System.Timers.Timer _playerTimer;
        private bool _isUpdatingProgress = false;
        private readonly LocalApiService _apiService;

        public MainWindowViewModel()
        {
            _downloadService = new DownloadService();
            _clipboardService = new ClipboardMonitorService();
            _settingsService = new SettingsService();
            _playerService = new PlayerService();
            _updaterService = new UpdaterService();
            
            // Start local API for browser extension
            _apiService = new LocalApiService(WaitingList, DownloadingList, DownloadedList);
            _apiService.Start();

            _downloadPath = _settingsService.DownloadPath;

            _clipboardService.OnYouTubeUrlDetected += async (url) => await HandleUrlDetectedAsync(url);
            Task.Run(() => _clipboardService.StartMonitoringAsync());

            // Initial update check
            Task.Run(async () => await CheckUpdateAsync());

            _playerTimer = new System.Timers.Timer(500);
            _playerTimer.Elapsed += (s, e) => {
                if (IsPlaying) 
                {
                    _isUpdatingProgress = true;
                    PlayerProgress = _playerService.GetCurrentPositionPercent();
                    _isUpdatingProgress = false;
                }
            };
            _playerTimer.Start();

            _playerService.PlaybackStopped += () => {
                AvalonUIThreadPost(() => {
                    IsPlaying = false;
                    PlayerProgress = 0;
                    PlayPauseIcon = "▶";
                });
            };

            // Startup tasks
            Task.Run(async () => {
                await LoadExistingFilesAsync();
                await CheckFFmpegAsync();
            });
        }

        [RelayCommand]
        public void ToggleAbout() => IsAboutVisible = !IsAboutVisible;

        [RelayCommand]
        public async Task CheckUpdateAsync()
        {
            if (IsUpdating) return;
            UpdateStatusText = "Denetleniyor...";
            
            var remoteVersion = await _updaterService.GetRemoteVersionAsync();
            var localVersion = _updaterService.GetLocalVersion();

            if (remoteVersion != null && remoteVersion != localVersion)
            {
                UpdateStatusText = "Güncelleme Mevcut!";
                await Task.Delay(1000);
                UpdateStatusText = "İndir ve Kur";
            }
            else
            {
                UpdateStatusText = "Güncel";
                await Task.Delay(2000);
                UpdateStatusText = "Güncellemeleri Denetle";
            }
        }

        [RelayCommand]
        public async Task StartUpdateAsync()
        {
            if (UpdateStatusText != "İndir ve Kur") 
            {
                await CheckUpdateAsync();
                return;
            }

            IsUpdating = true;
            await _updaterService.PerformUpdateAsync((progress) => {
                UpdateProgress = progress;
            });
        }

        [RelayCommand]
        public void MinimizeApp()
        {
             if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
             if (desktop.MainWindow != null)
             {
                 desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Minimized;
                 ShowInTaskbar = false;
                 IsMinimized = true;
             }
        }

        [RelayCommand]
        public void RestoreApp()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow != null)
                {
                    desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                    ShowInTaskbar = true;
                    IsMinimized = false;
                }
            }
        }

        [RelayCommand]
        public void CloseApp() => Environment.Exit(0);

        [RelayCommand]
        public void ToggleWindowState()
        {
            if (IsMinimized) RestoreApp();
            else MinimizeApp();
        }

        partial void OnPlayerProgressChanged(double value)
        {
            if (!_isUpdatingProgress && _playerService != null)
            {
                _playerService.Seek(value);
            }
        }

        private async Task CheckFFmpegAsync()
        {
            if (!_downloadService.FFmpegReady())
            {
                var item = new DownloadItem { Title = "FFmpeg Kontrolü...", Status = "Hazırlanıyor..." };
                AvalonUIThreadPost(() => DownloadingList.Add(item));
                await _downloadService.EnsureFFmpegAsync(p => item.Progress = p * 100);
                AvalonUIThreadPost(() => DownloadingList.Remove(item));
            }
        }

        private Task LoadExistingFilesAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(DownloadPath) || !System.IO.Directory.Exists(DownloadPath)) return;

                    var files = System.IO.Directory.GetFiles(DownloadPath, "*.mp3");
                    foreach (var file in files)
                    {
                        var fileName = System.IO.Path.GetFileName(file);
                        var item = new DownloadItem 
                        { 
                            Title = fileName.Replace(".mp3", ""), 
                            Status = "Zaten Var", 
                            Progress = 100,
                            FilePath = file,
                            FileName = fileName
                        };
                        
                        if (!DownloadedList.Any(d => d.FilePath == file))
                        {
                            AvalonUIThreadPost(() => DownloadedList.Add(item));
                        }
                    }
                }
                catch { }
            });
        }

        private string? _lastDetectedUrl;
        private DateTime _lastDetectedTime = DateTime.MinValue;

        private async Task HandleUrlDetectedAsync(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            url = url.Trim();

            if (url == _lastDetectedUrl && (DateTime.Now - _lastDetectedTime).TotalSeconds < 3) return;
            _lastDetectedUrl = url;
            _lastDetectedTime = DateTime.Now;

            var videoId = YoutubeExplode.Videos.VideoId.TryParse(url);
            if (videoId != null)
            {
                var cleanUrl = $"https://www.youtube.com/watch?v={videoId.Value}";
                if (IsInAnyList(cleanUrl)) return;
                await AddToQueueAsync(cleanUrl);
                return;
            }

            var playlistId = YoutubeExplode.Playlists.PlaylistId.TryParse(url);
            if (playlistId != null)
            {
                if (IsInAnyList(url)) return;
                await _downloadService.DownloadPlaylistAsync(url, 
                    (item) => AvalonUIThreadPost(() => {
                        if (!IsInAnyList(item.Url)) WaitingList.Add(item);
                        _ = StartDownloadsAsync(); // Auto-start
                    }),
                    (item, progress) => item.Progress = progress * 100);
            }
        }

        private bool IsInAnyList(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;

            var vId = YoutubeExplode.Videos.VideoId.TryParse(url);
            if (vId != null)
            {
                string targetId = vId.Value;
                return WaitingList.Any(i => YoutubeExplode.Videos.VideoId.TryParse(i.Url)?.Value == targetId) || 
                       DownloadingList.Any(i => YoutubeExplode.Videos.VideoId.TryParse(i.Url)?.Value == targetId) || 
                       DownloadedList.Any(i => YoutubeExplode.Videos.VideoId.TryParse(i.Url)?.Value == targetId);
            }

            return WaitingList.Any(i => i.Url == url) || 
                   DownloadingList.Any(i => i.Url == url) || 
                   DownloadedList.Any(i => i.Url == url);
        }

        private readonly System.Collections.Generic.HashSet<string> _recentlyAddedIds = new();

        private Task AddToQueueAsync(string url)
        {
            var vid = YoutubeExplode.Videos.VideoId.TryParse(url);
            if (vid != null && _recentlyAddedIds.Contains(vid.Value)) return Task.CompletedTask;
            if (vid != null) _recentlyAddedIds.Add(vid.Value);

            var item = new DownloadItem { Url = url, Title = "Arayan...", Status = "Bekliyor" };
            AvalonUIThreadPost(() => WaitingList.Add(item));

            _ = Task.Run(async () => {
                await _downloadService.PopulateMetadataAsync(item);
                await StartDownloadsAsync(); // Auto-start
            });

            return Task.CompletedTask;
        }

        [RelayCommand]
        public void RemoveFromWaiting(DownloadItem item) => WaitingList.Remove(item);

        [RelayCommand]
        public async Task StartDownloadsAsync()
        {
            if (_activeDownloads >= MaxConcurrentDownloads) return;

            while (WaitingList.Count > 0 && _activeDownloads < MaxConcurrentDownloads)
            {
                var item = WaitingList[0];
                WaitingList.RemoveAt(0);
                DownloadingList.Add(item);
                _activeDownloads++;

                _ = ProcessDownloadAsync(item);
                await Task.Delay(100);
            }
        }

        private async Task ProcessDownloadAsync(DownloadItem item)
        {
            await _downloadService.DownloadMp3Async(item, (p) => item.Progress = p * 100);
            
            AvalonUIThreadPost(() => {
                DownloadingList.Remove(item);
                if (!DownloadedList.Any(d => d.Url == item.Url && item.Url != null))
                {
                    DownloadedList.Insert(0, item);
                }
                _activeDownloads--;
                _ = StartDownloadsAsync(); // Keep queue moving
            });
        }

        [RelayCommand]
        public void TogglePlay(DownloadItem item)
        {
            if (item == null) return;

            if (CurrentSong == item && IsPlaying)
            {
                _playerService.Pause();
                IsPlaying = false;
                PlayPauseIcon = "▶";
            }
            else if (CurrentSong == item && !IsPlaying)
            {
                _playerService.Resume();
                IsPlaying = true;
                PlayPauseIcon = "⏸";
            }
            else
            {
                if (item.FilePath != null && System.IO.File.Exists(item.FilePath))
                {
                    CurrentSong = item;
                    _playerService.Play(item.FilePath);
                    IsPlaying = true;
                    PlayPauseIcon = "⏸";
                }
            }
        }

        partial void OnVolumeChanged(float value) => _playerService.Volume = value;

        [RelayCommand]
        public async Task SelectFolderAsync()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
                if (topLevel != null)
                {
                    var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = "İndirme Klasörünü Seç",
                        AllowMultiple = false
                    });

                    if (folders.Count > 0)
                    {
                        DownloadPath = folders[0].Path.LocalPath;
                        _settingsService.DownloadPath = DownloadPath;
                    }
                }
            }
        }

        private void AvalonUIThreadPost(System.Action action)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(action);
        }
    }
}
