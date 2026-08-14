using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BatchFileRenamer.Models;
using BatchFileRenamer.ViewModels;
using Microsoft.Win32;

namespace BatchFileRenamer.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly HistoryViewModel _historyViewModel;
        private Point _dragStartPoint;
        private RenameItem? _draggedItem;

        public MainWindow(MainViewModel viewModel, HistoryViewModel historyViewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _historyViewModel = historyViewModel;
            DataContext = _viewModel;

            _viewModel.RequestBrowseDirectory += OnRequestBrowseDirectory;
            _viewModel.RequestOpenHistory += OnRequestOpenHistory;
            _historyViewModel.SessionUndone += () => _viewModel.ScanDirectory();
        }

        private string? OnRequestBrowseDirectory()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Chọn thư mục chứa tệp cần đổi tên",
                Multiselect = false
            };

            if (!string.IsNullOrEmpty(_viewModel.CurrentDirectory) && System.IO.Directory.Exists(_viewModel.CurrentDirectory))
            {
                dialog.InitialDirectory = _viewModel.CurrentDirectory;
            }

            if (dialog.ShowDialog(this) == true)
            {
                return dialog.FolderName;
            }

            return null;
        }

        private void OnRequestOpenHistory()
        {
            var historyWindow = new HistoryWindow(_historyViewModel)
            {
                Owner = this
            };
            historyWindow.ShowDialog();
        }

        #region Drag and Drop Reordering in DataGrid

        private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedItem = GetItemFromPoint(PreviewDataGrid, _dragStartPoint);
        }

        private void DataGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
            {
                Point currentPosition = e.GetPosition(null);
                Vector diff = _dragStartPoint - currentPosition;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var data = new DataObject(typeof(RenameItem), _draggedItem);
                    DragDrop.DoDragDrop(PreviewDataGrid, data, DragDropEffects.Move);
                    _draggedItem = null;
                }
            }
        }

        private void DataGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(RenameItem)))
            {
                if (e.Data.GetData(typeof(RenameItem)) is RenameItem droppedItem)
                {
                    Point dropPoint = e.GetPosition(PreviewDataGrid);
                    var targetItem = GetItemFromPoint(PreviewDataGrid, dropPoint);

                    int oldIndex = _viewModel.Items.IndexOf(droppedItem);
                    int newIndex = targetItem != null ? _viewModel.Items.IndexOf(targetItem) : _viewModel.Items.Count - 1;

                    if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
                    {
                        _viewModel.MoveItem(oldIndex, newIndex);
                    }
                }
            }
        }

        private static RenameItem? GetItemFromPoint(DataGrid grid, Point point)
        {
            HitTestResult hitTest = VisualTreeHelper.HitTest(grid, point);
            if (hitTest?.VisualHit == null) return null;

            DependencyObject? current = hitTest.VisualHit;
            while (current != null && current != grid)
            {
                if (current is DataGridRow row && row.Item is RenameItem item)
                {
                    return item;
                }
                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        #endregion
    }
}
