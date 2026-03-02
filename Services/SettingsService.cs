using System;
using System.IO;
using System.Text.Json;

namespace VERTER.Services
{
    public class Settings
    {
        public string DownloadPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    public class SettingsService
    {
        private readonly string _settingsPath;
        private Settings _currentSettings;

        public SettingsService()
        {
            _settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
            _currentSettings = LoadSettings();
        }

        public string DownloadPath
        {
            get => _currentSettings.DownloadPath;
            set
            {
                _currentSettings.DownloadPath = value;
                SaveSettings();
            }
        }

        private Settings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                }
            }
            catch { }
            return new Settings();
        }

        private void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_currentSettings);
                File.WriteAllText(_settingsPath, json);
            }
            catch { }
        }
    }
}
