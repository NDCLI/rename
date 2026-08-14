using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BatchFileRenamer.Models;
using BatchFileRenamer.Services;
using BatchFileRenamer.ViewModels;
using Xunit;

namespace BatchFileRenamer.Tests
{
    public class MainViewModelTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly TemplateEngine _templateEngine = new();
        private readonly RenamePlanner _planner;
        private readonly RenameExecutor _executor = new();
        private readonly HistoryStore _historyStore;
        private readonly FileScannerService _scanner = new();
        private readonly MainViewModel _viewModel;

        public MainViewModelTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "VMTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            string historyPath = Path.Combine(_tempDir, "history.json");
            _historyStore = new HistoryStore(historyPath);
            _planner = new RenamePlanner(_templateEngine);

            _viewModel = new MainViewModel(_templateEngine, _planner, _executor, _historyStore, _scanner);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public void ViewModel_InitialExampleConfig_MatchesSpec()
        {
            // Spec example:
            // Mẫu: {name} ({date:MMM d, yyyy})
            // Tên chính: Re-ID Hoa
            // Ngày bắt đầu: 14/08/2026
            // Bước tăng: 1 ngày
            Assert.Equal("{name} ({date:MMM d, yyyy})", _viewModel.Template);
            Assert.Equal("Re-ID Hoa", _viewModel.BaseName);
            Assert.Equal(new DateTime(2026, 8, 14), _viewModel.StartDate);
            Assert.Equal(1, _viewModel.DayStep);
            Assert.Equal("en-US", _viewModel.SelectedLanguage);
        }

        [Fact]
        public void ScanDirectory_UpdatesPreviewAndValidatesAutomatically()
        {
            File.WriteAllText(Path.Combine(_tempDir, "file_1.txt"), "a");
            File.WriteAllText(Path.Combine(_tempDir, "file_2.txt"), "b");

            _viewModel.CurrentDirectory = _tempDir;

            Assert.Equal(2, _viewModel.Items.Count);
            Assert.True(_viewModel.CanExecuteRename);

            Assert.Equal("Re-ID Hoa (Aug 14, 2026).txt", _viewModel.Items[0].NewFileNameWithExtension);
            Assert.Equal("Re-ID Hoa (Aug 15, 2026).txt", _viewModel.Items[1].NewFileNameWithExtension);
        }

        [Fact]
        public void MoveItem_ReordersItems_AndUpdatesCalculatedDatesImmediately()
        {
            File.WriteAllText(Path.Combine(_tempDir, "file_1.txt"), "a");
            File.WriteAllText(Path.Combine(_tempDir, "file_2.txt"), "b");

            _viewModel.CurrentDirectory = _tempDir;

            // Before move: file_1 is Aug 14, file_2 is Aug 15
            Assert.Equal("file_1", _viewModel.Items[0].OriginalFileNameWithoutExtension);
            Assert.Equal("Re-ID Hoa (Aug 14, 2026).txt", _viewModel.Items[0].NewFileNameWithExtension);

            // Move index 0 down to index 1
            _viewModel.MoveItem(0, 1);

            // After move: file_2 is at index 0 (Aug 14), file_1 is at index 1 (Aug 15)
            Assert.Equal("file_2", _viewModel.Items[0].OriginalFileNameWithoutExtension);
            Assert.Equal("Re-ID Hoa (Aug 14, 2026).txt", _viewModel.Items[0].NewFileNameWithExtension);
            Assert.Equal("file_1", _viewModel.Items[1].OriginalFileNameWithoutExtension);
            Assert.Equal("Re-ID Hoa (Aug 15, 2026).txt", _viewModel.Items[1].NewFileNameWithExtension);
        }

        [Fact]
        public void UncheckItem_ExcludesFromRenamingSequence()
        {
            File.WriteAllText(Path.Combine(_tempDir, "file_1.txt"), "a");
            File.WriteAllText(Path.Combine(_tempDir, "file_2.txt"), "b");
            File.WriteAllText(Path.Combine(_tempDir, "file_3.txt"), "c");

            _viewModel.CurrentDirectory = _tempDir;

            // Uncheck file_2 (index 1)
            _viewModel.Items[1].IsSelected = false;

            // file_1 is selected (seq 0 -> Aug 14)
            Assert.Equal("Re-ID Hoa (Aug 14, 2026).txt", _viewModel.Items[0].NewFileNameWithExtension);
            Assert.Equal(RenameStatus.Valid, _viewModel.Items[0].Status);

            // file_2 is unselected -> Skipped
            Assert.Equal(RenameStatus.Skipped, _viewModel.Items[1].Status);

            // file_3 is selected (seq 1 -> Aug 15)
            Assert.Equal("Re-ID Hoa (Aug 15, 2026).txt", _viewModel.Items[2].NewFileNameWithExtension);
            Assert.Equal(RenameStatus.Valid, _viewModel.Items[2].Status);
        }

        [Fact]
        public void DynamicTemplateChange_UpdatesPreviewRealtime()
        {
            File.WriteAllText(Path.Combine(_tempDir, "file_1.txt"), "a");
            _viewModel.CurrentDirectory = _tempDir;

            _viewModel.Template = "Prefix_{name}_{n:000}";
            _viewModel.BaseName = "TestDoc";
            _viewModel.StartNumber = 10;

            Assert.Equal("Prefix_TestDoc_010.txt", _viewModel.Items[0].NewFileNameWithExtension);
        }
    }
}
