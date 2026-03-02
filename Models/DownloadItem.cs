using CommunityToolkit.Mvvm.ComponentModel;

namespace VERTER.Models
{
    public partial class DownloadItem : ObservableObject
    {
        [ObservableProperty]
        private string _title = "Arayan...";

        [ObservableProperty]
        private string _artist = "...";

        [ObservableProperty]
        private double _progress;

        [ObservableProperty]
        private string _status = "Bekleniyor";

        public string Url { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
    }
}
