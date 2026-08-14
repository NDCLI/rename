using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using BatchFileRenamer.Models;

namespace BatchFileRenamer.Services
{
    public interface IHistoryStore
    {
        string HistoryFilePath { get; }
        Task<List<RenameSession>> GetSessionsAsync();
        Task AddSessionAsync(RenameSession session);
        Task UpdateSessionAsync(RenameSession session);
        Task ClearHistoryAsync();
    }

    public class HistoryStore : IHistoryStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        private readonly string _historyFilePath;
        private readonly object _lock = new();

        public string HistoryFilePath => _historyFilePath;

        public HistoryStore(string? customFilePath = null)
        {
            if (!string.IsNullOrWhiteSpace(customFilePath))
            {
                _historyFilePath = customFilePath;
            }
            else
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, "BatchFileRenamer");
                _historyFilePath = Path.Combine(folder, "history.json");
            }
        }

        public async Task<List<RenameSession>> GetSessionsAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!File.Exists(_historyFilePath))
                    {
                        return new List<RenameSession>();
                    }

                    try
                    {
                        string json = File.ReadAllText(_historyFilePath);
                        var sessions = JsonSerializer.Deserialize<List<RenameSession>>(json, JsonOptions);
                        return sessions ?? new List<RenameSession>();
                    }
                    catch
                    {
                        return new List<RenameSession>();
                    }
                }
            });
        }

        public async Task AddSessionAsync(RenameSession session)
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    var sessions = new List<RenameSession>();
                    if (File.Exists(_historyFilePath))
                    {
                        try
                        {
                            string json = File.ReadAllText(_historyFilePath);
                            sessions = JsonSerializer.Deserialize<List<RenameSession>>(json, JsonOptions) ?? new List<RenameSession>();
                        }
                        catch
                        {
                            sessions = new List<RenameSession>();
                        }
                    }

                    // Insert at beginning (newest first)
                    sessions.Insert(0, session);

                    // Limit history to last 500 sessions for performance
                    if (sessions.Count > 500)
                    {
                        sessions = sessions.Take(500).ToList();
                    }

                    SaveSessionsInternal(sessions);
                }
            });
        }

        public async Task UpdateSessionAsync(RenameSession session)
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!File.Exists(_historyFilePath)) return;

                    try
                    {
                        string json = File.ReadAllText(_historyFilePath);
                        var sessions = JsonSerializer.Deserialize<List<RenameSession>>(json, JsonOptions) ?? new List<RenameSession>();
                        int index = sessions.FindIndex(s => s.SessionId == session.SessionId);
                        if (index >= 0)
                        {
                            sessions[index] = session;
                            SaveSessionsInternal(sessions);
                        }
                    }
                    catch { }
                }
            });
        }

        public async Task ClearHistoryAsync()
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (File.Exists(_historyFilePath))
                    {
                        try
                        {
                            File.Delete(_historyFilePath);
                        }
                        catch { }
                    }
                }
            });
        }

        private void SaveSessionsInternal(List<RenameSession> sessions)
        {
            string? dir = Path.GetDirectoryName(_historyFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(sessions, JsonOptions);
            File.WriteAllText(_historyFilePath, json);
        }
    }
}
