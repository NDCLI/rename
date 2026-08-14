using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BatchFileRenamer.Models;
using BatchFileRenamer.Services;
using Xunit;

namespace BatchFileRenamer.Tests
{
    public class RenamePlannerTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly TemplateEngine _templateEngine = new();
        private readonly RenamePlanner _planner;

        public RenamePlannerTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "BatchFileRenamer_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            _planner = new RenamePlanner(_templateEngine);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch { }
        }

        [Fact]
        public void GeneratePlan_ValidItems_GeneratesExpectedNamesAndPreservesExtensions()
        {
            string f1 = Path.Combine(_tempDirectory, "sample_a.jpg");
            string f2 = Path.Combine(_tempDirectory, "sample_b.PNG");
            string f3 = Path.Combine(_tempDirectory, "sample_c.docx");
            File.WriteAllText(f1, "a");
            File.WriteAllText(f2, "b");
            File.WriteAllText(f3, "c");

            var items = new List<RenameItem>
            {
                RenameItem.FromFileInfo(new FileInfo(f1), 1),
                RenameItem.FromFileInfo(new FileInfo(f2), 2),
                RenameItem.FromFileInfo(new FileInfo(f3), 3)
            };

            var options = new RenameTemplateOptions
            {
                Template = "Photo_{n:00}",
                StartNumber = 1,
                NumberStep = 1
            };

            var plan = _planner.GeneratePlan(items, options);

            Assert.True(plan.CanExecute);
            Assert.Equal(3, plan.SelectedCount);
            Assert.Equal(0, plan.ConflictCount);

            Assert.Equal("Photo_01.jpg", plan.Items[0].NewFileNameWithExtension);
            Assert.Equal("Photo_02.PNG", plan.Items[1].NewFileNameWithExtension);
            Assert.Equal("Photo_03.docx", plan.Items[2].NewFileNameWithExtension);
        }

        [Fact]
        public void GeneratePlan_InvalidWindowsChars_FlagsConflict()
        {
            string f1 = Path.Combine(_tempDirectory, "test.txt");
            File.WriteAllText(f1, "data");

            var items = new List<RenameItem> { RenameItem.FromFileInfo(new FileInfo(f1), 1) };
            var options = new RenameTemplateOptions
            {
                Template = "File:Invalid*Name",
                BaseName = "Test"
            };

            var plan = _planner.GeneratePlan(items, options);

            Assert.False(plan.CanExecute);
            Assert.Equal(1, plan.ConflictCount);
            Assert.Equal(ConflictType.InvalidCharacters, plan.Items[0].ConflictType);
        }

        [Fact]
        public void GeneratePlan_ReservedWindowsName_FlagsConflict()
        {
            string f1 = Path.Combine(_tempDirectory, "source.txt");
            File.WriteAllText(f1, "data");

            var items = new List<RenameItem> { RenameItem.FromFileInfo(new FileInfo(f1), 1) };
            var options = new RenameTemplateOptions
            {
                Template = "CON",
                BaseName = "CON"
            };

            var plan = _planner.GeneratePlan(items, options);

            Assert.False(plan.CanExecute);
            Assert.Equal(1, plan.ConflictCount);
            Assert.Equal(ConflictType.ReservedWindowsName, plan.Items[0].ConflictType);
        }

        [Fact]
        public void GeneratePlan_DuplicateInBatch_FlagsConflict()
        {
            string f1 = Path.Combine(_tempDirectory, "item1.txt");
            string f2 = Path.Combine(_tempDirectory, "item2.txt");
            File.WriteAllText(f1, "1");
            File.WriteAllText(f2, "2");

            var items = new List<RenameItem>
            {
                RenameItem.FromFileInfo(new FileInfo(f1), 1),
                RenameItem.FromFileInfo(new FileInfo(f2), 2)
            };

            // Template produces identical name for all items
            var options = new RenameTemplateOptions
            {
                Template = "StaticName",
                BaseName = "Fixed"
            };

            var plan = _planner.GeneratePlan(items, options);

            Assert.False(plan.CanExecute);
            Assert.Equal(2, plan.ConflictCount);
            Assert.Equal(ConflictType.DuplicateInBatch, plan.Items[0].ConflictType);
            Assert.Equal(ConflictType.DuplicateInBatch, plan.Items[1].ConflictType);
        }

        [Fact]
        public void GeneratePlan_TargetAlreadyExistsOnDisk_OutsideBatch_FlagsConflict()
        {
            string f1 = Path.Combine(_tempDirectory, "item1.txt");
            string fExisting = Path.Combine(_tempDirectory, "Target.txt");
            File.WriteAllText(f1, "1");
            File.WriteAllText(fExisting, "existing outside file");

            var items = new List<RenameItem>
            {
                RenameItem.FromFileInfo(new FileInfo(f1), 1)
            };

            var options = new RenameTemplateOptions
            {
                Template = "Target",
                BaseName = "Target"
            };

            var plan = _planner.GeneratePlan(items, options);

            Assert.False(plan.CanExecute);
            Assert.Equal(1, plan.ConflictCount);
            Assert.Equal(ConflictType.TargetAlreadyExistsOnDisk, plan.Items[0].ConflictType);
        }

        [Fact]
        public void GeneratePlan_SwapWithinBatch_IsPermittedWithoutFalseCollision()
        {
            // A.txt wants to become B.txt, and B.txt wants to become A.txt
            string fA = Path.Combine(_tempDirectory, "A.txt");
            string fB = Path.Combine(_tempDirectory, "B.txt");
            File.WriteAllText(fA, "content A");
            File.WriteAllText(fB, "content B");

            var itemA = RenameItem.FromFileInfo(new FileInfo(fA), 1);
            var itemB = RenameItem.FromFileInfo(new FileInfo(fB), 2);

            // Reorder items so itemA gets B and itemB gets A
            var items = new List<RenameItem> { itemA, itemB };

            // We test template where 1st item becomes B and 2nd item becomes A
            // We can test by setting options or custom template
            var options = new RenameTemplateOptions
            {
                Template = "{n:A;B}", // n=1 => A, n=2 => B? Or test custom planner directly
                BaseName = "Swap"
            };

            // Let's test with a template that generates "File_2" for 1st item and "File_1" for 2nd
            // First rename files to File_1.txt and File_2.txt
            string f1 = Path.Combine(_tempDirectory, "File_1.txt");
            string f2 = Path.Combine(_tempDirectory, "File_2.txt");
            File.Move(fA, f1);
            File.Move(fB, f2);

            var list = new List<RenameItem>
            {
                RenameItem.FromFileInfo(new FileInfo(f1), 1),
                RenameItem.FromFileInfo(new FileInfo(f2), 2)
            };

            // Item 1 (File_1.txt) will be renamed to File_2.txt (startNumber=2, step=0? Or step=-1, start=2)
            var swapOptions = new RenameTemplateOptions
            {
                Template = "File_{n}",
                StartNumber = 2,
                NumberStep = -1 // so 2, 1
            };

            var plan = _planner.GeneratePlan(list, swapOptions);

            // The target "File_2.txt" and "File_1.txt" already exist on disk, BUT both are in the batch!
            Assert.True(plan.CanExecute);
            Assert.Equal(0, plan.ConflictCount);
        }
    }
}
