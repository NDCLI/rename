using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BatchFileRenamer.Models;

namespace BatchFileRenamer.Services
{
    public class RenamePlan
    {
        public List<RenameItem> Items { get; set; } = new();
        public int TotalCount => Items.Count;
        public int SelectedCount => Items.Count(x => x.IsSelected);
        public int ConflictCount => Items.Count(x => x.IsSelected && x.HasConflict);
        public bool CanExecute => SelectedCount > 0 && ConflictCount == 0;
        public string SummaryMessage { get; set; } = string.Empty;
    }

    public interface IRenamePlanner
    {
        RenamePlan GeneratePlan(IEnumerable<RenameItem> items, RenameTemplateOptions options);
    }

    public class RenamePlanner : IRenamePlanner
    {
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
        private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        private readonly ITemplateEngine _templateEngine;

        public RenamePlanner(ITemplateEngine templateEngine)
        {
            _templateEngine = templateEngine;
        }

        public RenamePlan GeneratePlan(IEnumerable<RenameItem> items, RenameTemplateOptions options)
        {
            var plan = new RenamePlan();
            var itemList = items.ToList();
            plan.Items = itemList;

            if (!_templateEngine.ValidateTemplate(options.Template, out string templateError))
            {
                plan.SummaryMessage = $"Lỗi mẫu định dạng: {templateError}";
                foreach (var item in itemList)
                {
                    if (item.IsSelected)
                    {
                        item.Status = RenameStatus.Conflict;
                        item.ConflictType = ConflictType.InvalidCharacters;
                        item.StatusMessage = templateError;
                    }
                }
                return plan;
            }

            int selectedIndex = 0;

            // Phase 1: Generate proposed names for selected items
            foreach (var item in itemList)
            {
                if (!item.IsSelected)
                {
                    item.NewFileNameWithoutExtension = item.OriginalFileNameWithoutExtension;
                    item.NewFullPath = item.OriginalFullPath;
                    item.TemporaryFullPath = string.Empty;
                    item.Status = RenameStatus.Skipped;
                    item.ConflictType = ConflictType.None;
                    item.StatusMessage = "Bỏ qua (không chọn)";
                    continue;
                }

                string generatedName = _templateEngine.GenerateFileName(options, selectedIndex, item);
                item.NewFileNameWithoutExtension = generatedName;
                
                string newFileNameWithExt = item.NewFileNameWithExtension;
                item.NewFullPath = Path.Combine(item.OriginalDirectory, newFileNameWithExt);
                
                // Assign a unique GUID temporary path in the same directory
                string tempFileName = $"__tmp_renamer_{Guid.NewGuid():N}_{item.OriginalFileNameWithoutExtension}{item.Extension}.tmp";
                item.TemporaryFullPath = Path.Combine(item.OriginalDirectory, tempFileName);

                selectedIndex++;
            }

            // Phase 2: Conflict detection
            // Set of all original full paths in this batch
            var batchOriginalPaths = new HashSet<string>(
                itemList.Where(x => x.IsSelected).Select(x => x.OriginalFullPath), 
                StringComparer.OrdinalIgnoreCase);

            // Group by NewFullPath to detect duplicates within the batch
            var newPathGroups = itemList
                .Where(x => x.IsSelected)
                .GroupBy(x => x.NewFullPath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var item in itemList)
            {
                if (!item.IsSelected) continue;

                var (conflictType, message) = ValidateItem(item, newPathGroups, batchOriginalPaths);

                if (conflictType != ConflictType.None)
                {
                    item.Status = RenameStatus.Conflict;
                    item.ConflictType = conflictType;
                    item.StatusMessage = message;
                }
                else
                {
                    item.Status = RenameStatus.Valid;
                    item.ConflictType = ConflictType.None;
                    
                    if (string.Equals(item.OriginalFullPath, item.NewFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        item.StatusMessage = "Tên không đổi";
                    }
                    else
                    {
                        item.StatusMessage = "Hợp lệ";
                    }
                }
            }

            if (plan.ConflictCount > 0)
            {
                plan.SummaryMessage = $"Phát hiện {plan.ConflictCount} tệp có xung đột hoặc không hợp lệ. Vui lòng kiểm tra lại trước khi thực hiện.";
            }
            else if (plan.SelectedCount == 0)
            {
                plan.SummaryMessage = "Chưa chọn tệp nào để đổi tên.";
            }
            else
            {
                plan.SummaryMessage = $"Sẵn sàng đổi tên {plan.SelectedCount} tệp an toàn.";
            }

            return plan;
        }

        private (ConflictType conflictType, string message) ValidateItem(
            RenameItem item, 
            Dictionary<string, List<RenameItem>> newPathGroups,
            HashSet<string> batchOriginalPaths)
        {
            string newNameWithoutExt = item.NewFileNameWithoutExtension;

            // 1. Empty name
            if (string.IsNullOrWhiteSpace(newNameWithoutExt))
            {
                return (ConflictType.EmptyName, "Tên file mới không được để trống.");
            }

            // 2. Trailing spaces or dots
            if (newNameWithoutExt.EndsWith(" ") || newNameWithoutExt.EndsWith("."))
            {
                return (ConflictType.InvalidCharacters, "Tên file không được kết thúc bằng dấu cách hoặc dấu chấm.");
            }

            // 3. Invalid Windows characters
            if (newNameWithoutExt.IndexOfAny(InvalidFileNameChars) >= 0)
            {
                var invalidChars = string.Join(" ", newNameWithoutExt.Where(c => InvalidFileNameChars.Contains(c)).Distinct());
                return (ConflictType.InvalidCharacters, $"Tên file chứa ký tự cấm: {invalidChars}");
            }

            // 4. Windows reserved device names
            if (ReservedWindowsNames.Contains(newNameWithoutExt.Trim()))
            {
                return (ConflictType.ReservedWindowsName, $"'{newNameWithoutExt}' là tên thiết bị cấm của hệ điều hành Windows.");
            }

            // 5. Source file exists
            if (!File.Exists(item.OriginalFullPath))
            {
                return (ConflictType.SourceNotFound, "Tệp nguồn không còn tồn tại trên ổ đĩa.");
            }

            // 6. Path too long
            if (item.NewFullPath.Length >= 260)
            {
                return (ConflictType.PathTooLong, $"Đường dẫn đích quá dài ({item.NewFullPath.Length} ký tự, giới hạn thông thường 260).");
            }

            // 7. Duplicate in batch
            if (newPathGroups.TryGetValue(item.NewFullPath, out var duplicates) && duplicates.Count > 1)
            {
                return (ConflictType.DuplicateInBatch, $"Tên mới bị trùng lặp với {duplicates.Count - 1} tệp khác trong cùng thư mục.");
            }

            // 8. Target already exists on disk and is NOT one of the files in this renaming batch
            if (File.Exists(item.NewFullPath))
            {
                bool isSameAsSource = string.Equals(item.OriginalFullPath, item.NewFullPath, StringComparison.OrdinalIgnoreCase);
                bool isPartSelectedBatch = batchOriginalPaths.Contains(item.NewFullPath);

                if (!isSameAsSource && !isPartSelectedBatch)
                {
                    return (ConflictType.TargetAlreadyExistsOnDisk, "Đã có một tệp khác trên ổ đĩa trùng với tên đích mới.");
                }
            }

            return (ConflictType.None, string.Empty);
        }
    }
}
