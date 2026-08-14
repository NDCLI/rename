using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace BatchFileRenamer.Models
{
    public enum RenameStatus
    {
        Pending,
        Valid,
        Conflict,
        Renamed,
        Failed,
        RolledBack,
        Skipped
    }

    public enum ConflictType
    {
        None,
        EmptyName,
        InvalidCharacters,
        ReservedWindowsName,
        PathTooLong,
        SourceNotFound,
        DuplicateInBatch,
        TargetAlreadyExistsOnDisk,
        AccessDenied,
        UnknownError
    }

    public class RenameItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        private int _orderIndex;
        private string _newFileNameWithoutExtension = string.Empty;
        private string _newFullPath = string.Empty;
        private RenameStatus _status = RenameStatus.Pending;
        private ConflictType _conflictType = ConflictType.None;
        private string _statusMessage = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string OriginalFullPath { get; set; } = string.Empty;
        public string OriginalDirectory { get; set; } = string.Empty;
        public string OriginalFileNameWithoutExtension { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string OriginalFileNameWithExtension => OriginalFileNameWithoutExtension + Extension;
        public long SizeBytes { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string TemporaryFullPath { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        public int OrderIndex
        {
            get => _orderIndex;
            set { if (_orderIndex != value) { _orderIndex = value; OnPropertyChanged(); } }
        }

        public string NewFileNameWithoutExtension
        {
            get => _newFileNameWithoutExtension;
            set 
            { 
                if (_newFileNameWithoutExtension != value) 
                { 
                    _newFileNameWithoutExtension = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(NewFileNameWithExtension)); 
                } 
            }
        }

        public string NewFileNameWithExtension => string.IsNullOrEmpty(NewFileNameWithoutExtension) 
            ? string.Empty 
            : NewFileNameWithoutExtension + Extension;

        public string NewFullPath
        {
            get => _newFullPath;
            set { if (_newFullPath != value) { _newFullPath = value; OnPropertyChanged(); } }
        }

        public RenameStatus Status
        {
            get => _status;
            set 
            { 
                if (_status != value) 
                { 
                    _status = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(HasConflict));
                    OnPropertyChanged(nameof(IsValid));
                } 
            }
        }

        public ConflictType ConflictType
        {
            get => _conflictType;
            set { if (_conflictType != value) { _conflictType = value; OnPropertyChanged(); } }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { if (_statusMessage != value) { _statusMessage = value; OnPropertyChanged(); } }
        }

        public bool HasConflict => Status == RenameStatus.Conflict || Status == RenameStatus.Failed;
        public bool IsValid => Status == RenameStatus.Valid;

        public string FormattedSize
        {
            get
            {
                if (SizeBytes < 1024) return $"{SizeBytes} B";
                if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:F1} KB";
                if (SizeBytes < 1024 * 1024 * 1024) return $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
                return $"{SizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
            }
        }

        public static RenameItem FromFileInfo(FileInfo fileInfo, int orderIndex = 0)
        {
            string ext = fileInfo.Extension;
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileInfo.Name);

            return new RenameItem
            {
                OriginalFullPath = fileInfo.FullName,
                OriginalDirectory = fileInfo.DirectoryName ?? string.Empty,
                OriginalFileNameWithoutExtension = nameWithoutExt,
                Extension = ext,
                SizeBytes = fileInfo.Length,
                CreatedDate = fileInfo.CreationTime,
                ModifiedDate = fileInfo.LastWriteTime,
                OrderIndex = orderIndex,
                IsSelected = true,
                Status = RenameStatus.Pending
            };
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RenameTemplateOptions
    {
        public string Template { get; set; } = "{name} ({date:MMM d, yyyy})";
        public string BaseName { get; set; } = "Re-ID Hoa";
        public DateTime StartDate { get; set; } = new DateTime(2026, 8, 14);
        public int DayStep { get; set; } = 1;
        public int StartNumber { get; set; } = 1;
        public int NumberStep { get; set; } = 1;
        public string CultureLanguage { get; set; } = "en-US"; // "en-US" or "vi-VN"
    }

    public class RenameFileMapping
    {
        public string OriginalFullPath { get; set; } = string.Empty;
        public string NewFullPath { get; set; } = string.Empty;
        public string TemporaryFullPath { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? Error { get; set; }
    }

    public class RenameSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string DirectoryPath { get; set; } = string.Empty;
        public int TotalFiles { get; set; }
        public int SuccessCount { get; set; }
        public bool IsRolledBack { get; set; }
        public List<RenameFileMapping> Mappings { get; set; } = new();
    }
}
