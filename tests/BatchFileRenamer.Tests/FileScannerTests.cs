using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BatchFileRenamer.Services;
using Xunit;

namespace BatchFileRenamer.Tests
{
    public class FileScannerTests : IDisposable
    {
        private readonly string _tempRoot;
        private readonly FileScannerService _scanner = new();

        public FileScannerTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "ScannerTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempRoot))
                {
                    Directory.Delete(_tempRoot, true);
                }
            }
            catch { }
        }

        [Fact]
        public void ScanDirectory_TopDirectoryOnly_DoesNotIncludeSubdirectories()
        {
            string subDir = Path.Combine(_tempRoot, "SubFolder");
            Directory.CreateDirectory(subDir);

            File.WriteAllText(Path.Combine(_tempRoot, "root1.txt"), "a");
            File.WriteAllText(Path.Combine(_tempRoot, "root2.txt"), "b");
            File.WriteAllText(Path.Combine(subDir, "sub1.txt"), "c");

            var items = _scanner.ScanDirectory(_tempRoot, includeSubdirectories: false, extensionFilter: null);

            Assert.Equal(2, items.Count);
            Assert.Contains(items, x => x.OriginalFileNameWithoutExtension == "root1");
            Assert.Contains(items, x => x.OriginalFileNameWithoutExtension == "root2");
            Assert.DoesNotContain(items, x => x.OriginalFileNameWithoutExtension == "sub1");
        }

        [Fact]
        public void ScanDirectory_IncludeSubdirectories_FindsFilesInAllLevels()
        {
            string subDir = Path.Combine(_tempRoot, "Level1", "Level2");
            Directory.CreateDirectory(subDir);

            File.WriteAllText(Path.Combine(_tempRoot, "root.txt"), "a");
            File.WriteAllText(Path.Combine(subDir, "nested.txt"), "b");

            var items = _scanner.ScanDirectory(_tempRoot, includeSubdirectories: true, extensionFilter: null);

            Assert.Equal(2, items.Count);
            Assert.Contains(items, x => x.OriginalFileNameWithoutExtension == "root");
            Assert.Contains(items, x => x.OriginalFileNameWithoutExtension == "nested");
        }

        [Fact]
        public void ScanDirectory_ExtensionFilter_FiltersCorrectly()
        {
            File.WriteAllText(Path.Combine(_tempRoot, "photo1.jpg"), "1");
            File.WriteAllText(Path.Combine(_tempRoot, "photo2.PNG"), "2");
            File.WriteAllText(Path.Combine(_tempRoot, "doc.pdf"), "3");
            File.WriteAllText(Path.Combine(_tempRoot, "video.mp4"), "4");

            // Filter multiple extensions separated by comma
            var items = _scanner.ScanDirectory(_tempRoot, includeSubdirectories: false, extensionFilter: ".jpg, .png");

            Assert.Equal(2, items.Count);
            Assert.Contains(items, x => x.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(items, x => x.Extension.Equals(".PNG", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(items, x => x.Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void SortItems_SortsCorrectlyBySizeAndDate()
        {
            string fSmall = Path.Combine(_tempRoot, "small.txt");
            string fLarge = Path.Combine(_tempRoot, "large.txt");
            File.WriteAllText(fSmall, "small");
            File.WriteAllText(fLarge, new string('x', 5000));

            var items = _scanner.ScanDirectory(_tempRoot, false, null);

            var sortedAsc = _scanner.SortItems(items, SortCriterion.Size, SortDirection.Ascending);
            Assert.Equal("small", sortedAsc[0].OriginalFileNameWithoutExtension);
            Assert.Equal("large", sortedAsc[1].OriginalFileNameWithoutExtension);

            var sortedDesc = _scanner.SortItems(items, SortCriterion.Size, SortDirection.Descending);
            Assert.Equal("large", sortedDesc[0].OriginalFileNameWithoutExtension);
            Assert.Equal("small", sortedDesc[1].OriginalFileNameWithoutExtension);
        }
    }
}
