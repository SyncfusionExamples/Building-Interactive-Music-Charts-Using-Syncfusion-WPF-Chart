using System.Windows;

namespace MusicalChart
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            playbackLine.X1 = 0;
            _ = viewModel.InitializeAsync(playbackLine);
        }
    }
}