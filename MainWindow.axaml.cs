using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VERTER.ViewModels;

namespace VERTER
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            
            var titleBar = this.FindControl<Control>("TitleBar");
            if (titleBar != null)
            {
                titleBar.PointerPressed += (s, e) => BeginMoveDrag(e);
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}