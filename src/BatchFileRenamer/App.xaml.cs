using System;
using System.Windows;
using BatchFileRenamer.Services;
using BatchFileRenamer.ViewModels;
using BatchFileRenamer.Views;

namespace BatchFileRenamer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Register services
            ITemplateEngine templateEngine = new TemplateEngine();
            IRenamePlanner renamePlanner = new RenamePlanner(templateEngine);
            IRenameExecutor renameExecutor = new RenameExecutor();
            IHistoryStore historyStore = new HistoryStore();
            IFileScannerService fileScanner = new FileScannerService();

            // Register view models
            var mainViewModel = new MainViewModel(templateEngine, renamePlanner, renameExecutor, historyStore, fileScanner);
            var historyViewModel = new HistoryViewModel(historyStore, renameExecutor);

            // Launch Main Window
            var mainWindow = new MainWindow(mainViewModel, historyViewModel);
            mainWindow.Show();
        }
    }
}
