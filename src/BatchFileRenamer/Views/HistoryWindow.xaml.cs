using System.Windows;
using BatchFileRenamer.ViewModels;

namespace BatchFileRenamer.Views
{
    public partial class HistoryWindow : Window
    {
        public HistoryWindow(HistoryViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += async (s, e) => await viewModel.LoadSessionsAsync();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
