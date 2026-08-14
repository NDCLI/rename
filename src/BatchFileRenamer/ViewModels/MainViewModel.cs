using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BatchFileRenamer.Models;
using BatchFileRenamer.Services;

namespace BatchFileRenamer.ViewModels
{
    public class TemplatePreset
    {
        public string DisplayName { get; set; } = string.Empty;
        public string TemplatePattern { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public override string ToString() => DisplayName;
    }

    public class MainViewModel : ViewModelBase
    {
        private readonly ITemplateEngine _templateEngine;
        private readonly IRenamePlanner _renamePlanner;
        private readonly IRenameExecutor _renameExecutor;
        private readonly IHistoryStore _historyStore;
        private readonly IFileScannerService _fileScanner;

        private string _currentDirectory = string.Empty;
        private bool _includeSubdirectories = false;
        private string _extensionFilter = string.Empty;
        private SortCriterion _selectedSortCriterion = SortCriterion.Name;
        private SortDirection _selectedSortDirection = SortDirection.Ascending;

        private string _template = "{name}_{n:000}";
        private string _baseName = "Re-ID Hoa";
        private DateTime _startDate = new DateTime(2026, 8, 14);
        private int _dayStep = 1;
        private int _startNumber = 1;
        private int _numberStep = 1;
        private string _selectedLanguage = "en-US";

        private ObservableCollection<RenameItem> _items = new();
        private ObservableCollection<TemplatePreset> _presets = new();
        private TemplatePreset? _selectedPreset;

        private bool? _isAllSelected = true;
        private bool _isScanning = false;
        private bool _isExecuting = false;
        private double _progressValue = 0;
        private string _progressText = string.Empty;
        private string _summaryText = "Vui lòng chọn một thư mục để bắt đầu.";
        private bool _canExecuteRename = false;
        private string _samplePreviewText = string.Empty;

        public string CurrentDirectory
        {
            get => _currentDirectory;
            set
            {
                if (SetProperty(ref _currentDirectory, value))
                {
                    ScanDirectory();
                }
            }
        }

        public bool IncludeSubdirectories
        {
            get => _includeSubdirectories;
            set
            {
                if (SetProperty(ref _includeSubdirectories, value))
                {
                    ScanDirectory();
                }
            }
        }

        public string ExtensionFilter
        {
            get => _extensionFilter;
            set
            {
                if (SetProperty(ref _extensionFilter, value))
                {
                    ScanDirectory();
                }
            }
        }

        public SortCriterion SelectedSortCriterion
        {
            get => _selectedSortCriterion;
            set
            {
                if (SetProperty(ref _selectedSortCriterion, value))
                {
                    ApplySorting();
                }
            }
        }

        public SortDirection SelectedSortDirection
        {
            get => _selectedSortDirection;
            set
            {
                if (SetProperty(ref _selectedSortDirection, value))
                {
                    ApplySorting();
                }
            }
        }

        public string Template
        {
            get => _template;
            set
            {
                if (SetProperty(ref _template, value))
                {
                    OnPropertyChanged(nameof(HasDateToken));
                    OnPropertyChanged(nameof(HasNumberToken));
                    UpdatePlan();
                }
            }
        }

        public bool HasDateToken => !string.IsNullOrEmpty(Template) && Template.Contains("{date", StringComparison.OrdinalIgnoreCase);
        public bool HasNumberToken => !string.IsNullOrEmpty(Template) && (Template.Contains("{n", StringComparison.OrdinalIgnoreCase) || Template.Contains("{seq", StringComparison.OrdinalIgnoreCase) || Template.Contains("{num", StringComparison.OrdinalIgnoreCase));

        public string BaseName
        {
            get => _baseName;
            set
            {
                if (SetProperty(ref _baseName, value))
                {
                    UpdatePlan();
                }
            }
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    UpdatePlan();
                }
            }
        }

        public int DayStep
        {
            get => _dayStep;
            set
            {
                int val = Math.Max(1, value);
                if (SetProperty(ref _dayStep, val))
                {
                    UpdatePlan();
                }
            }
        }

        public int StartNumber
        {
            get => _startNumber;
            set
            {
                if (SetProperty(ref _startNumber, value))
                {
                    UpdatePlan();
                }
            }
        }

        public int NumberStep
        {
            get => _numberStep;
            set
            {
                int val = Math.Max(1, value);
                if (SetProperty(ref _numberStep, val))
                {
                    UpdatePlan();
                }
            }
        }

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value))
                {
                    UpdatePlan();
                }
            }
        }

        public ObservableCollection<RenameItem> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public ObservableCollection<TemplatePreset> Presets
        {
            get => _presets;
            set => SetProperty(ref _presets, value);
        }

        public TemplatePreset? SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (SetProperty(ref _selectedPreset, value) && value != null)
                {
                    Template = value.TemplatePattern;
                }
            }
        }

        public bool? IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (SetProperty(ref _isAllSelected, value) && value.HasValue)
                {
                    SetAllItemsSelected(value.Value);
                }
            }
        }

        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                if (SetProperty(ref _isExecuting, value))
                {
                    (ExecuteRenameCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        public string SummaryText
        {
            get => _summaryText;
            set => SetProperty(ref _summaryText, value);
        }

        public bool CanExecuteRename
        {
            get => _canExecuteRename;
            set
            {
                if (SetProperty(ref _canExecuteRename, value))
                {
                    (ExecuteRenameCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string SamplePreviewText
        {
            get => _samplePreviewText;
            set => SetProperty(ref _samplePreviewText, value);
        }

        public ICommand BrowseDirectoryCommand { get; }
        public ICommand RefreshScanCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand UnselectAllCommand { get; }
        public ICommand InvertSelectionCommand { get; }
        public ICommand InsertTokenCommand { get; }
        public ICommand ApplyPresetCommand { get; }
        public ICommand MoveItemUpCommand { get; }
        public ICommand MoveItemDownCommand { get; }
        public ICommand ExecuteRenameCommand { get; }
        public ICommand OpenHistoryCommand { get; }

        public event Action? RequestOpenHistory;
        public event Func<string?>? RequestBrowseDirectory;

        public MainViewModel(
            ITemplateEngine templateEngine,
            IRenamePlanner renamePlanner,
            IRenameExecutor renameExecutor,
            IHistoryStore historyStore,
            IFileScannerService fileScanner)
        {
            _templateEngine = templateEngine;
            _renamePlanner = renamePlanner;
            _renameExecutor = renameExecutor;
            _historyStore = historyStore;
            _fileScanner = fileScanner;

            InitializePresets();

            BrowseDirectoryCommand = new RelayCommand(BrowseDirectory);
            RefreshScanCommand = new RelayCommand(ScanDirectory);
            SelectAllCommand = new RelayCommand(() => SetAllItemsSelected(true));
            UnselectAllCommand = new RelayCommand(() => SetAllItemsSelected(false));
            InvertSelectionCommand = new RelayCommand(InvertSelection);
            InsertTokenCommand = new RelayCommand(InsertToken);
            ApplyPresetCommand = new RelayCommand(ApplyPreset);
            MoveItemUpCommand = new RelayCommand(MoveItemUp);
            MoveItemDownCommand = new RelayCommand(MoveItemDown);
            ExecuteRenameCommand = new AsyncRelayCommand(ExecuteRenameAsync, () => CanExecuteRename && !IsExecuting);
            OpenHistoryCommand = new RelayCommand(() => RequestOpenHistory?.Invoke());

            UpdateSamplePreview();
        }

        private void InitializePresets()
        {
            _presets = new ObservableCollection<TemplatePreset>
            {
                new TemplatePreset 
                { 
                    DisplayName = "📌 Tên chính + Số thứ tự 3 số ({name}_{n:000})", 
                    TemplatePattern = "{name}_{n:000}",
                    Description = "Ví dụ: Re-ID Hoa_001, Re-ID Hoa_002..."
                },
                new TemplatePreset 
                { 
                    DisplayName = "📌 Tên chính + Số trong ngoặc ({name} ({n}))", 
                    TemplatePattern = "{name} ({n})",
                    Description = "Ví dụ: Re-ID Hoa (1), Re-ID Hoa (2)..."
                },
                new TemplatePreset 
                { 
                    DisplayName = "📌 Tên chính + Ngày tháng ({name} ({date:MMM d, yyyy}))", 
                    TemplatePattern = "{name} ({date:MMM d, yyyy})",
                    Description = "Ví dụ: Re-ID Hoa (Aug 14, 2026)..."
                },
                new TemplatePreset 
                { 
                    DisplayName = "📌 Tên chính + Ngày chuẩn + Số ({name}_{date:yyyyMMdd}_{n:000})", 
                    TemplatePattern = "{name}_{date:yyyyMMdd}_{n:000}",
                    Description = "Ví dụ: Re-ID Hoa_20260814_001..."
                },
                new TemplatePreset 
                { 
                    DisplayName = "📌 Thêm tiền tố vào tên gốc ({name}_{orig})", 
                    TemplatePattern = "{name}_{orig}",
                    Description = "Ví dụ: Re-ID Hoa_ImageOriginalName"
                },
                new TemplatePreset 
                { 
                    DisplayName = "📌 Thêm hậu tố vào tên gốc ({orig}_{name})", 
                    TemplatePattern = "{orig}_{name}",
                    Description = "Ví dụ: ImageOriginalName_Re-ID Hoa"
                },
                new TemplatePreset 
                { 
                    DisplayName = "📌 Tên gốc + Số thứ tự ({orig}_{n:00})", 
                    TemplatePattern = "{orig}_{n:00}",
                    Description = "Ví dụ: DocName_01, DocName_02..."
                },
                new TemplatePreset 
                { 
                    DisplayName = "📌 Tên thư mục cha + Số ({parent}_{n:000})", 
                    TemplatePattern = "{parent}_{n:000}",
                    Description = "Ví dụ: FolderName_001, FolderName_002..."
                },
                new TemplatePreset 
                { 
                    DisplayName = "📌 Chỉ số thứ tự ({n:000})", 
                    TemplatePattern = "{n:000}",
                    Description = "Ví dụ: 001, 002, 003..."
                }
            };

            _selectedPreset = _presets.FirstOrDefault();
        }

        private void ApplyPreset(object? param)
        {
            if (param is TemplatePreset preset)
            {
                SelectedPreset = preset;
            }
            else if (param is string pattern && !string.IsNullOrEmpty(pattern))
            {
                Template = pattern;
            }
        }

        public void BrowseDirectory()
        {
            string? selected = RequestBrowseDirectory?.Invoke();
            if (!string.IsNullOrWhiteSpace(selected))
            {
                CurrentDirectory = selected;
            }
        }

        public void ScanDirectory()
        {
            if (string.IsNullOrWhiteSpace(CurrentDirectory) || !Directory.Exists(CurrentDirectory))
            {
                Items.Clear();
                SummaryText = "Thư mục không hợp lệ hoặc chưa được chọn.";
                CanExecuteRename = false;
                return;
            }

            IsScanning = true;
            try
            {
                var list = _fileScanner.ScanDirectory(
                    CurrentDirectory, 
                    IncludeSubdirectories, 
                    ExtensionFilter, 
                    SelectedSortCriterion, 
                    SelectedSortDirection);

                // Unsubscribe previous handlers
                foreach (var old in Items)
                {
                    old.PropertyChanged -= Item_PropertyChanged;
                }

                Items = new ObservableCollection<RenameItem>(list);

                foreach (var item in Items)
                {
                    item.PropertyChanged += Item_PropertyChanged;
                }

                _isAllSelected = true;
                OnPropertyChanged(nameof(IsAllSelected));

                UpdatePlan();
            }
            finally
            {
                IsScanning = false;
            }
        }

        public void ApplySorting()
        {
            if (Items.Count == 0) return;

            var sorted = _fileScanner.SortItems(Items, SelectedSortCriterion, SelectedSortDirection);
            
            // Re-assign without full recreation
            Items = new ObservableCollection<RenameItem>(sorted);
            foreach (var item in Items)
            {
                item.PropertyChanged -= Item_PropertyChanged;
                item.PropertyChanged += Item_PropertyChanged;
            }

            UpdatePlan();
        }

        public void MoveItem(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= Items.Count || newIndex < 0 || newIndex >= Items.Count || oldIndex == newIndex)
            {
                return;
            }

            var item = Items[oldIndex];
            Items.RemoveAt(oldIndex);
            Items.Insert(newIndex, item);

            // Re-index
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].OrderIndex = i + 1;
            }

            UpdatePlan();
        }

        private void MoveItemUp(object? param)
        {
            if (param is RenameItem item)
            {
                int index = Items.IndexOf(item);
                if (index > 0)
                {
                    MoveItem(index, index - 1);
                }
            }
        }

        private void MoveItemDown(object? param)
        {
            if (param is RenameItem item)
            {
                int index = Items.IndexOf(item);
                if (index >= 0 && index < Items.Count - 1)
                {
                    MoveItem(index, index + 1);
                }
            }
        }

        private void SetAllItemsSelected(bool isSelected)
        {
            foreach (var item in Items)
            {
                item.IsSelected = isSelected;
            }
            UpdatePlan();
        }

        private void InvertSelection()
        {
            foreach (var item in Items)
            {
                item.IsSelected = !item.IsSelected;
            }
            UpdatePlan();
        }

        private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RenameItem.IsSelected))
            {
                UpdateSelectionState();
                UpdatePlan();
            }
        }

        private void UpdateSelectionState()
        {
            int total = Items.Count;
            if (total == 0)
            {
                _isAllSelected = false;
            }
            else
            {
                int selectedCount = Items.Count(x => x.IsSelected);
                if (selectedCount == total) _isAllSelected = true;
                else if (selectedCount == 0) _isAllSelected = false;
                else _isAllSelected = null;
            }
            OnPropertyChanged(nameof(IsAllSelected));
        }

        public void InsertToken(object? param)
        {
            if (param is string token && !string.IsNullOrEmpty(token))
            {
                Template += token;
            }
        }

        public void UpdatePlan()
        {
            UpdateSamplePreview();

            if (Items.Count == 0)
            {
                SummaryText = "Không có tệp nào trong danh sách.";
                CanExecuteRename = false;
                return;
            }

            var options = GetCurrentOptions();
            var plan = _renamePlanner.GeneratePlan(Items, options);

            SummaryText = plan.SummaryMessage;
            CanExecuteRename = plan.CanExecute;
        }

        private void UpdateSamplePreview()
        {
            var options = GetCurrentOptions();
            string sample = _templateEngine.GenerateFileName(options, 0);
            SamplePreviewText = $"Ví dụ file #1: {sample}.ext";
        }

        private RenameTemplateOptions GetCurrentOptions()
        {
            return new RenameTemplateOptions
            {
                Template = Template,
                BaseName = BaseName,
                StartDate = StartDate,
                DayStep = DayStep,
                StartNumber = StartNumber,
                NumberStep = NumberStep,
                CultureLanguage = SelectedLanguage
            };
        }

        public async Task ExecuteRenameAsync()
        {
            if (!CanExecuteRename || IsExecuting) return;

            int selectedCount = Items.Count(x => x.IsSelected && x.IsValid);
            if (selectedCount == 0) return;

            var confirm = MessageBox.Show(
                $"Xác nhận đổi tên hàng loạt:\n\n• Tổng số tệp sẽ đổi: {selectedCount} tệp\n• Thư mục: {CurrentDirectory}\n\nBạn có muốn tiếp tục không?",
                "Xác nhận đổi tên",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            IsExecuting = true;
            ProgressValue = 0;
            ProgressText = "Đang chuẩn bị đổi tên an toàn...";

            var progress = new Progress<(int current, int total, string currentFileName)>(p =>
            {
                ProgressValue = (double)p.current / p.total * 100.0;
                ProgressText = $"[{p.current}/{p.total}] {p.currentFileName}";
            });

            try
            {
                var result = await _renameExecutor.ExecuteAsync(Items, CurrentDirectory, progress);

                if (result.IsSuccess && result.Session != null)
                {
                    await _historyStore.AddSessionAsync(result.Session);
                    ProgressValue = 100;
                    ProgressText = $"Hoàn tất đổi tên {result.SuccessCount} tệp thành công!";
                    SummaryText = $"Thành công: Đã đổi tên {result.SuccessCount} tệp.";

                    MessageBox.Show(
                        $"Đã đổi tên thành công {result.SuccessCount} tệp!\n\nLịch sử phiên đã được lưu lại để có thể hoàn tác bất kỳ lúc nào.",
                        "Thành công",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Re-scan to sync latest disk state
                    ScanDirectory();
                }
                else
                {
                    ProgressText = "Thao tác thất bại!";
                    SummaryText = result.ErrorMessage ?? "Lỗi không xác định khi đổi tên.";

                    MessageBox.Show(
                        result.ErrorMessage ?? "Có lỗi xảy ra trong quá trình đổi tên.",
                        "Lỗi đổi tên",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                SummaryText = $"Lỗi: {ex.Message}";
                MessageBox.Show($"Lỗi ngoại lệ: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExecuting = false;
            }
        }
    }
}
