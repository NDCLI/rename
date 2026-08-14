using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BatchFileRenamer.Models;

namespace BatchFileRenamer.Services
{
    public class RenameExecutionResult
    {
        public bool IsSuccess { get; set; }
        public int SuccessCount { get; set; }
        public int TotalCount { get; set; }
        public string? ErrorMessage { get; set; }
        public RenameSession? Session { get; set; }
    }

    public interface IRenameExecutor
    {
        Task<RenameExecutionResult> ExecuteAsync(
            IEnumerable<RenameItem> items, 
            string directoryPath, 
            IProgress<(int current, int total, string currentFileName)>? progress = null,
            CancellationToken cancellationToken = default);

        Task<RenameExecutionResult> RollbackSessionAsync(
            RenameSession session, 
            IProgress<(int current, int total)>? progress = null,
            CancellationToken cancellationToken = default);
    }

    public class RenameExecutor : IRenameExecutor
    {
        public async Task<RenameExecutionResult> ExecuteAsync(
            IEnumerable<RenameItem> items, 
            string directoryPath, 
            IProgress<(int current, int total, string currentFileName)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var selectedItems = items.Where(x => x.IsSelected && x.IsValid).ToList();
            if (selectedItems.Count == 0)
            {
                return new RenameExecutionResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Không có tệp hợp lệ nào được chọn để đổi tên."
                };
            }

            var session = new RenameSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Timestamp = DateTime.Now,
                DirectoryPath = directoryPath,
                TotalFiles = selectedItems.Count
            };

            var phase1Success = new List<RenameItem>();
            var phase2Success = new List<RenameItem>();
            int total = selectedItems.Count;

            return await Task.Run(() =>
            {
                try
                {
                    // PHASE 1: Rename all original files to temporary GUID paths
                    for (int i = 0; i < selectedItems.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var item = selectedItems[i];

                        progress?.Report((i + 1, total * 2, $"Đang tạo tên tạm: {item.OriginalFileNameWithExtension}"));

                        // Skip physical rename if name is completely identical
                        if (string.Equals(item.OriginalFullPath, item.NewFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            phase1Success.Add(item);
                            continue;
                        }

                        if (!File.Exists(item.OriginalFullPath))
                        {
                            throw new FileNotFoundException($"Tệp không tồn tại: {item.OriginalFullPath}", item.OriginalFullPath);
                        }

                        File.Move(item.OriginalFullPath, item.TemporaryFullPath);
                        phase1Success.Add(item);
                    }

                    // PHASE 2: Rename all temporary files to final target new paths
                    for (int i = 0; i < selectedItems.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var item = selectedItems[i];

                        progress?.Report((total + i + 1, total * 2, $"Đang đổi tên chính thức: {item.NewFileNameWithExtension}"));

                        if (string.Equals(item.OriginalFullPath, item.NewFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            phase2Success.Add(item);
                            session.Mappings.Add(new RenameFileMapping
                            {
                                OriginalFullPath = item.OriginalFullPath,
                                NewFullPath = item.NewFullPath,
                                TemporaryFullPath = item.TemporaryFullPath,
                                IsSuccess = true
                            });
                            continue;
                        }

                        if (!File.Exists(item.TemporaryFullPath))
                        {
                            throw new FileNotFoundException($"Tệp tạm không tồn tại: {item.TemporaryFullPath}", item.TemporaryFullPath);
                        }

                        File.Move(item.TemporaryFullPath, item.NewFullPath);
                        phase2Success.Add(item);

                        session.Mappings.Add(new RenameFileMapping
                        {
                            OriginalFullPath = item.OriginalFullPath,
                            NewFullPath = item.NewFullPath,
                            TemporaryFullPath = item.TemporaryFullPath,
                            IsSuccess = true
                        });
                    }

                    // Complete success: update model items
                    foreach (var item in selectedItems)
                    {
                        item.Status = RenameStatus.Renamed;
                        item.StatusMessage = "Đổi tên thành công";
                        item.OriginalFullPath = item.NewFullPath;
                        item.OriginalFileNameWithoutExtension = item.NewFileNameWithoutExtension;
                    }

                    session.SuccessCount = selectedItems.Count;
                    return new RenameExecutionResult
                    {
                        IsSuccess = true,
                        SuccessCount = selectedItems.Count,
                        TotalCount = selectedItems.Count,
                        Session = session
                    };
                }
                catch (Exception ex)
                {
                    // Rollback phase 2 items from NewFullPath back to TemporaryFullPath
                    foreach (var item in phase2Success)
                    {
                        if (string.Equals(item.OriginalFullPath, item.NewFullPath, StringComparison.OrdinalIgnoreCase)) continue;
                        try
                        {
                            if (File.Exists(item.NewFullPath))
                            {
                                File.Move(item.NewFullPath, item.TemporaryFullPath);
                            }
                        }
                        catch { /* Ignore rollback individual error to continue restoring */ }
                    }

                    // Rollback phase 1 items from TemporaryFullPath back to OriginalFullPath
                    foreach (var item in phase1Success)
                    {
                        if (string.Equals(item.OriginalFullPath, item.NewFullPath, StringComparison.OrdinalIgnoreCase)) continue;
                        try
                        {
                            if (File.Exists(item.TemporaryFullPath))
                            {
                                File.Move(item.TemporaryFullPath, item.OriginalFullPath);
                            }
                        }
                        catch { /* Ignore rollback individual error */ }
                    }

                    foreach (var item in selectedItems)
                    {
                        item.Status = RenameStatus.RolledBack;
                        item.StatusMessage = $"Đã khôi phục về ban đầu do lỗi: {ex.Message}";
                    }

                    return new RenameExecutionResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Có lỗi xảy ra trong quá trình đổi tên: {ex.Message}. Hệ thống đã tự động khôi phục toàn bộ tệp về trạng thái ban đầu.",
                        Session = null
                    };
                }
            });
        }

        public async Task<RenameExecutionResult> RollbackSessionAsync(
            RenameSession session, 
            IProgress<(int current, int total)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (session.IsRolledBack)
            {
                return new RenameExecutionResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Phiên này đã được hoàn tác trước đó."
                };
            }

            var validMappings = session.Mappings.Where(x => x.IsSuccess).ToList();
            if (validMappings.Count == 0)
            {
                return new RenameExecutionResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Không tìm thấy ánh xạ tệp hợp lệ nào trong phiên này."
                };
            }

            // Pre-check safety before undo
            foreach (var m in validMappings)
            {
                if (!File.Exists(m.NewFullPath))
                {
                    return new RenameExecutionResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Không thể hoàn tác: Tệp '{m.NewFullPath}' không còn tồn tại trên ổ đĩa."
                    };
                }

                // If original path already occupied by a different file
                if (!string.Equals(m.OriginalFullPath, m.NewFullPath, StringComparison.OrdinalIgnoreCase) && 
                    File.Exists(m.OriginalFullPath))
                {
                    return new RenameExecutionResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Không thể hoàn tác: Đã có một tệp khác chiếm chỗ tại '{m.OriginalFullPath}'."
                    };
                }
            }

            return await Task.Run(() =>
            {
                var phase1Rollback = new List<RenameFileMapping>();
                var phase2Rollback = new List<RenameFileMapping>();
                int total = validMappings.Count;

                try
                {
                    // Phase 1 Undo: Move from NewFullPath to TemporaryFullPath
                    for (int i = 0; i < validMappings.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var m = validMappings[i];

                        progress?.Report((i + 1, total * 2));

                        if (string.Equals(m.OriginalFullPath, m.NewFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            phase1Rollback.Add(m);
                            continue;
                        }

                        // Generate fresh temp path for rollback
                        string dir = Path.GetDirectoryName(m.NewFullPath) ?? string.Empty;
                        string temp = Path.Combine(dir, $"__tmp_undo_{Guid.NewGuid():N}.tmp");
                        m.TemporaryFullPath = temp;

                        File.Move(m.NewFullPath, m.TemporaryFullPath);
                        phase1Rollback.Add(m);
                    }

                    // Phase 2 Undo: Move from TemporaryFullPath to OriginalFullPath
                    for (int i = 0; i < validMappings.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var m = validMappings[i];

                        progress?.Report((total + i + 1, total * 2));

                        if (string.Equals(m.OriginalFullPath, m.NewFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            phase2Rollback.Add(m);
                            continue;
                        }

                        File.Move(m.TemporaryFullPath, m.OriginalFullPath);
                        phase2Rollback.Add(m);
                    }

                    session.IsRolledBack = true;
                    return new RenameExecutionResult
                    {
                        IsSuccess = true,
                        SuccessCount = validMappings.Count,
                        TotalCount = validMappings.Count,
                        Session = session
                    };
                }
                catch (Exception ex)
                {
                    // Restore in case of failure during undo
                    foreach (var m in phase2Rollback)
                    {
                        if (string.Equals(m.OriginalFullPath, m.NewFullPath, StringComparison.OrdinalIgnoreCase)) continue;
                        try { if (File.Exists(m.OriginalFullPath)) File.Move(m.OriginalFullPath, m.TemporaryFullPath); } catch { }
                    }
                    foreach (var m in phase1Rollback)
                    {
                        if (string.Equals(m.OriginalFullPath, m.NewFullPath, StringComparison.OrdinalIgnoreCase)) continue;
                        try { if (File.Exists(m.TemporaryFullPath)) File.Move(m.TemporaryFullPath, m.NewFullPath); } catch { }
                    }

                    return new RenameExecutionResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Lỗi khi thực hiện hoàn tác: {ex.Message}"
                    };
                }
            });
        }
    }
}
