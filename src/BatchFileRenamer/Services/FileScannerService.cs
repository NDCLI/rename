using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BatchFileRenamer.Helpers;
using BatchFileRenamer.Models;

namespace BatchFileRenamer.Services
{
    public enum SortCriterion
    {
        Name,
        CreatedDate,
        ModifiedDate,
        Size,
        FullPath
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }

    public interface IFileScannerService
    {
        List<RenameItem> ScanDirectory(
            string directoryPath, 
            bool includeSubdirectories, 
            string? extensionFilter, 
            SortCriterion sortCriterion = SortCriterion.Name, 
            SortDirection sortDirection = SortDirection.Ascending);

        List<RenameItem> SortItems(
            IEnumerable<RenameItem> items, 
            SortCriterion sortCriterion, 
            SortDirection sortDirection);
    }

    public class FileScannerService : IFileScannerService
    {
        public List<RenameItem> ScanDirectory(
            string directoryPath, 
            bool includeSubdirectories, 
            string? extensionFilter, 
            SortCriterion sortCriterion = SortCriterion.Name, 
            SortDirection sortDirection = SortDirection.Ascending)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return new List<RenameItem>();
            }

            var allowedExtensions = ParseExtensionFilter(extensionFilter);
            var searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            var items = new List<RenameItem>();

            try
            {
                var directoryInfo = new DirectoryInfo(directoryPath);
                var enumerationOptions = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = includeSubdirectories,
                    AttributesToSkip = FileAttributes.ReparsePoint
                };

                var fileInfos = directoryInfo.EnumerateFiles("*", enumerationOptions);

                foreach (var fi in fileInfos)
                {
                    if (allowedExtensions.Count > 0 && !allowedExtensions.Contains(fi.Extension.ToLowerInvariant()))
                    {
                        continue;
                    }

                    items.Add(RenameItem.FromFileInfo(fi));
                }
            }
            catch (Exception)
            {
                // Return whatever collected or empty list on fatal directory access error
            }

            return SortItems(items, sortCriterion, sortDirection);
        }

        public List<RenameItem> SortItems(
            IEnumerable<RenameItem> items, 
            SortCriterion sortCriterion, 
            SortDirection sortDirection)
        {
            IOrderedEnumerable<RenameItem> ordered;

            switch (sortCriterion)
            {
                case SortCriterion.Name:
                    ordered = sortDirection == SortDirection.Ascending
                        ? items.OrderBy(x => x.OriginalFileNameWithoutExtension, NaturalStringComparer.Default)
                        : items.OrderByDescending(x => x.OriginalFileNameWithoutExtension, NaturalStringComparer.Default);
                    break;

                case SortCriterion.CreatedDate:
                    ordered = sortDirection == SortDirection.Ascending
                        ? items.OrderBy(x => x.CreatedDate)
                        : items.OrderByDescending(x => x.CreatedDate);
                    break;

                case SortCriterion.ModifiedDate:
                    ordered = sortDirection == SortDirection.Ascending
                        ? items.OrderBy(x => x.ModifiedDate)
                        : items.OrderByDescending(x => x.ModifiedDate);
                    break;

                case SortCriterion.Size:
                    ordered = sortDirection == SortDirection.Ascending
                        ? items.OrderBy(x => x.SizeBytes)
                        : items.OrderByDescending(x => x.SizeBytes);
                    break;

                case SortCriterion.FullPath:
                    ordered = sortDirection == SortDirection.Ascending
                        ? items.OrderBy(x => x.OriginalFullPath, NaturalStringComparer.Default)
                        : items.OrderByDescending(x => x.OriginalFullPath, NaturalStringComparer.Default);
                    break;

                default:
                    ordered = items.OrderBy(x => x.OrderIndex);
                    break;
            }

            var result = ordered.ToList();
            for (int i = 0; i < result.Count; i++)
            {
                result[i].OrderIndex = i + 1;
            }

            return result;
        }

        private static HashSet<string> ParseExtensionFilter(string? filter)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(filter) || filter.Trim() == "*" || filter.Trim() == "*.*")
            {
                return set;
            }

            var tokens = filter.Split(new[] { ',', ';', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                var ext = token.Trim();
                if (!ext.StartsWith("."))
                {
                    ext = "." + ext;
                }
                set.Add(ext.ToLowerInvariant());
            }

            return set;
        }
    }
}
