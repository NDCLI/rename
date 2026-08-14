using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BatchFileRenamer.Models;
using BatchFileRenamer.Services;

namespace BatchFileRenamer.ViewModels
{
    public class HistoryViewModel : ViewModelBase
    {
        private readonly IHistoryStore _historyStore;
        private readonly IRenameExecutor _renameExecutor;
        private ObservableCollection<RenameSession> _sessions = new();
        private RenameSession? _selectedSession;
        private bool _isLoading;
        private bool _isUndoing;
        private string _statusMessage = string.Empty;

        public ObservableCollection<RenameSession> Sessions
        {
            get => _sessions;
            set => SetProperty(ref _sessions, value);
        }

        public RenameSession? SelectedSession
        {
            get => _selectedSession;
            set
            {
                if (SetProperty(ref _selectedSession, value))
                {
                    OnPropertyChanged(nameof(CanUndo));
                    (UndoCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsUndoing
        {
            get => _isUndoing;
            set
            {
                if (SetProperty(ref _isUndoing, value))
                {
                    OnPropertyChanged(nameof(CanUndo));
                    (UndoCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool CanUndo => SelectedSession != null && !SelectedSession.IsRolledBack && !IsUndoing;

        public ICommand LoadCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand ClearHistoryCommand { get; }

        public event Action? SessionUndone;

        public HistoryViewModel(IHistoryStore historyStore, IRenameExecutor renameExecutor)
        {
            _historyStore = historyStore;
            _renameExecutor = renameExecutor;

            LoadCommand = new AsyncRelayCommand(LoadSessionsAsync);
            UndoCommand = new AsyncRelayCommand(UndoSelectedSessionAsync, () => CanUndo);
            ClearHistoryCommand = new AsyncRelayCommand(ClearHistoryAsync);
        }

        public async Task LoadSessionsAsync()
        {
            IsLoading = true;
            try
            {
                var list = await _historyStore.GetSessionsAsync();
                Sessions = new ObservableCollection<RenameSession>(list);
                SelectedSession = Sessions.FirstOrDefault();
                StatusMessage = $"Đã tải {Sessions.Count} phiên lịch sử.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi khi tải lịch sử: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task UndoSelectedSessionAsync()
        {
            if (SelectedSession == null || SelectedSession.IsRolledBack) return;

            var confirm = MessageBox.Show(
                $"Bạn có chắc chắn muốn hoàn tác phiên đổi tên ngày {SelectedSession.Timestamp:dd/MM/yyyy HH:mm:ss} với {SelectedSession.SuccessCount} tệp không?\n\nThao tác này sẽ đổi các tệp trở lại tên ban đầu.",
                "Xác nhận hoàn tác",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            IsUndoing = true;
            StatusMessage = "Đang thực hiện hoàn tác các tệp...";

            try
            {
                var result = await _renameExecutor.RollbackSessionAsync(SelectedSession);
                if (result.IsSuccess)
                {
                    await _historyStore.UpdateSessionAsync(SelectedSession);
                    OnPropertyChanged(nameof(CanUndo));
                    StatusMessage = $"Hoàn tác thành công {result.SuccessCount} tệp!";
                    MessageBox.Show(
                        $"Đã hoàn tác thành công {result.SuccessCount} tệp về tên ban đầu!",
                        "Hoàn tác thành công",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    SessionUndone?.Invoke();
                }
                else
                {
                    StatusMessage = $"Hoàn tác thất bại: {result.ErrorMessage}";
                    MessageBox.Show(
                        $"Không thể hoàn tác phiên này:\n\n{result.ErrorMessage}",
                        "Lỗi hoàn tác",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi hoàn tác: {ex.Message}";
                MessageBox.Show($"Lỗi không mong muốn: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsUndoing = false;
            }
        }

        public async Task ClearHistoryAsync()
        {
            var confirm = MessageBox.Show(
                "Bạn có chắc chắn muốn xóa toàn bộ lịch sử các phiên đổi tên không? (Các tệp trên ổ đĩa sẽ không bị ảnh hưởng)",
                "Xóa lịch sử",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                await _historyStore.ClearHistoryAsync();
                Sessions.Clear();
                SelectedSession = null;
                StatusMessage = "Đã xóa toàn bộ lịch sử.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi khi xóa lịch sử: {ex.Message}";
            }
        }
    }
}
