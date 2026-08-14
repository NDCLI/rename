using System;
using System.IO;
using System.Threading.Tasks;
using BatchFileRenamer.Models;
using BatchFileRenamer.Services;
using Xunit;

namespace BatchFileRenamer.Tests
{
    public class HistoryStoreTests : IDisposable
    {
        private readonly string _tempHistoryFile;
        private readonly HistoryStore _store;

        public HistoryStoreTests()
        {
            _tempHistoryFile = Path.Combine(Path.GetTempPath(), "test_history_" + Guid.NewGuid().ToString("N") + ".json");
            _store = new HistoryStore(_tempHistoryFile);
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_tempHistoryFile))
                {
                    File.Delete(_tempHistoryFile);
                }
            }
            catch { }
        }

        [Fact]
        public async Task HistoryStore_AddAndRetrieveSessions_PreservesMappings()
        {
            var session = new RenameSession
            {
                SessionId = "session-123",
                Timestamp = new DateTime(2026, 8, 14, 10, 0, 0),
                DirectoryPath = @"C:\TestDir",
                TotalFiles = 2,
                SuccessCount = 2
            };
            session.Mappings.Add(new RenameFileMapping
            {
                OriginalFullPath = @"C:\TestDir\old1.txt",
                NewFullPath = @"C:\TestDir\new1.txt",
                IsSuccess = true
            });
            session.Mappings.Add(new RenameFileMapping
            {
                OriginalFullPath = @"C:\TestDir\old2.txt",
                NewFullPath = @"C:\TestDir\new2.txt",
                IsSuccess = true
            });

            await _store.AddSessionAsync(session);

            // Re-instantiate store with same file to simulate app restart
            var reloadedStore = new HistoryStore(_tempHistoryFile);
            var sessions = await reloadedStore.GetSessionsAsync();

            Assert.Single(sessions);
            Assert.Equal("session-123", sessions[0].SessionId);
            Assert.Equal(2, sessions[0].Mappings.Count);
            Assert.Equal(@"C:\TestDir\new1.txt", sessions[0].Mappings[0].NewFullPath);
        }
    }
}
