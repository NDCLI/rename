using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BatchFileRenamer.Models;
using BatchFileRenamer.Services;
using Xunit;

namespace BatchFileRenamer.Tests
{
    public class RenameExecutorTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly TemplateEngine _templateEngine = new();
        private readonly RenamePlanner _planner;
        private readonly RenameExecutor _executor = new();

        public RenameExecutorTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "BatchRenameExecTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _planner = new RenamePlanner(_templateEngine);
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
        public async Task ExecuteAsync_NormalRename_RenamesFilesAndUpdatesStatus()
        {
            string f1 = Path.Combine(_tempDir, "doc1.txt");
            string f2 = Path.Combine(_tempDir, "doc2.txt");
            File.WriteAllText(f1, "content1");
            File.WriteAllText(f2, "content2");

            var items = new List<RenameItem>
            {
                RenameItem.FromFileInfo(new FileInfo(f1), 1),
                RenameItem.FromFileInfo(new FileInfo(f2), 2)
            };

            var options = new RenameTemplateOptions
            {
                Template = "Renamed_{n:00}",
                StartNumber = 1,
                NumberStep = 1
            };

            var plan = _planner.GeneratePlan(items, options);
            var result = await _executor.ExecuteAsync(plan.Items, _tempDir);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.SuccessCount);

            string expected1 = Path.Combine(_tempDir, "Renamed_01.txt");
            string expected2 = Path.Combine(_tempDir, "Renamed_02.txt");

            Assert.True(File.Exists(expected1));
            Assert.True(File.Exists(expected2));
            Assert.False(File.Exists(f1));
            Assert.False(File.Exists(f2));

            Assert.Equal("content1", File.ReadAllText(expected1));
            Assert.Equal("content2", File.ReadAllText(expected2));
        }

        [Fact]
        public async Task ExecuteAsync_SwapFileNames_SucceedsViaTwoPhaseRenaming()
        {
            // File A contains 'ALPHA', File B contains 'BETA'
            // We swap their names: A.txt becomes B.txt, and B.txt becomes A.txt
            string pathA = Path.Combine(_tempDir, "A.txt");
            string pathB = Path.Combine(_tempDir, "B.txt");
            File.WriteAllText(pathA, "ALPHA");
            File.WriteAllText(pathB, "BETA");

            var itemA = RenameItem.FromFileInfo(new FileInfo(pathA), 1);
            var itemB = RenameItem.FromFileInfo(new FileInfo(pathB), 2);

            // Plan manually or with custom template
            itemA.NewFileNameWithoutExtension = "B";
            itemA.NewFullPath = pathB;
            itemA.TemporaryFullPath = Path.Combine(_tempDir, "__tmp_A.tmp");
            itemA.Status = RenameStatus.Valid;

            itemB.NewFileNameWithoutExtension = "A";
            itemB.NewFullPath = pathA;
            itemB.TemporaryFullPath = Path.Combine(_tempDir, "__tmp_B.tmp");
            itemB.Status = RenameStatus.Valid;

            var result = await _executor.ExecuteAsync(new[] { itemA, itemB }, _tempDir);

            Assert.True(result.IsSuccess);
            Assert.True(File.Exists(pathA));
            Assert.True(File.Exists(pathB));

            // Verify the swap! A.txt should now contain BETA, and B.txt should contain ALPHA
            Assert.Equal("BETA", File.ReadAllText(pathA));
            Assert.Equal("ALPHA", File.ReadAllText(pathB));
        }

        [Fact]
        public async Task ExecuteAsync_RollbackOnFailure_RestoresOriginalFiles()
        {
            string f1 = Path.Combine(_tempDir, "first.txt");
            string f2 = Path.Combine(_tempDir, "second.txt");
            File.WriteAllText(f1, "content1");
            File.WriteAllText(f2, "content2");

            var item1 = RenameItem.FromFileInfo(new FileInfo(f1), 1);
            var item2 = RenameItem.FromFileInfo(new FileInfo(f2), 2);

            item1.NewFileNameWithoutExtension = "new_first";
            item1.NewFullPath = Path.Combine(_tempDir, "new_first.txt");
            item1.TemporaryFullPath = Path.Combine(_tempDir, "__tmp_1.tmp");
            item1.Status = RenameStatus.Valid;

            // Intentionally provide an invalid temporary path for item2 to trigger failure during phase 1
            item2.NewFileNameWithoutExtension = "new_second";
            item2.NewFullPath = Path.Combine(_tempDir, "new_second.txt");
            item2.TemporaryFullPath = "Z:\\NonExistentDrive\\invalid.tmp";
            item2.Status = RenameStatus.Valid;

            var result = await _executor.ExecuteAsync(new[] { item1, item2 }, _tempDir);

            Assert.False(result.IsSuccess);
            Assert.NotEmpty(result.ErrorMessage!);

            // Both original files MUST still exist and remain untouched
            Assert.True(File.Exists(f1));
            Assert.True(File.Exists(f2));
            Assert.Equal("content1", File.ReadAllText(f1));
            Assert.Equal("content2", File.ReadAllText(f2));
        }

        [Fact]
        public async Task RollbackSessionAsync_Undo_RestoresFilesToOriginalState()
        {
            string f1 = Path.Combine(_tempDir, "original_1.txt");
            string f2 = Path.Combine(_tempDir, "original_2.txt");
            File.WriteAllText(f1, "data1");
            File.WriteAllText(f2, "data2");

            var items = new List<RenameItem>
            {
                RenameItem.FromFileInfo(new FileInfo(f1), 1),
                RenameItem.FromFileInfo(new FileInfo(f2), 2)
            };

            var options = new RenameTemplateOptions
            {
                Template = "renamed_{n}",
                StartNumber = 10
            };

            var plan = _planner.GeneratePlan(items, options);
            var execResult = await _executor.ExecuteAsync(plan.Items, _tempDir);

            Assert.True(execResult.IsSuccess);
            Assert.NotNull(execResult.Session);

            string ren1 = Path.Combine(_tempDir, "renamed_10.txt");
            string ren2 = Path.Combine(_tempDir, "renamed_11.txt");
            Assert.True(File.Exists(ren1));
            Assert.True(File.Exists(ren2));

            // Perform Rollback/Undo
            var undoResult = await _executor.RollbackSessionAsync(execResult.Session!);

            Assert.True(undoResult.IsSuccess);
            Assert.True(File.Exists(f1));
            Assert.True(File.Exists(f2));
            Assert.False(File.Exists(ren1));
            Assert.False(File.Exists(ren2));
            Assert.Equal("data1", File.ReadAllText(f1));
            Assert.Equal("data2", File.ReadAllText(f2));
        }
    }
}
