using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ElevatorGUI
{
    public sealed class AsyncMongoLogger : IDisposable
    {
        private readonly IMongoCollection<BsonDocument>? _collection;
        private readonly BackgroundWorker _worker;
        private readonly ConcurrentQueue<LogEntry> _queue = new();
        private bool _disposed;
        public bool IsHealthy { get; private set; }

        public AsyncMongoLogger()
        {
            try
            {
                string cfgPath = Path.Combine(AppContext.BaseDirectory, "config.json");
                string connStr = "mongodb://localhost:27017";
                string dbName = "ElevatorDB";

                if (File.Exists(cfgPath))
                {
                    var raw = File.ReadAllText(cfgPath);
                    var doc = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(raw);
                    if (doc.Contains("ConnectionString")) connStr = doc["ConnectionString"].AsString;
                    if (doc.Contains("DatabaseName")) dbName = doc["DatabaseName"].AsString;
                }

                var client = new MongoClient(connStr);
                var db = client.GetDatabase(dbName);
                _collection = db.GetCollection<BsonDocument>("ElevatorLogs");
                IsHealthy = true;
            }
            catch (Exception ex)
            {
                IsHealthy = false;
                MessageBox.Show($"DB initialisation failed.\nLogging will continue in memory.\n\n{ex.Message}",
                    "MongoDB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            _worker = new BackgroundWorker { WorkerSupportsCancellation = true };
            _worker.DoWork += (_, __) => FlushLoop();
            _worker.RunWorkerAsync();
        }

        public void Enqueue(LogEntry entry) => _queue.Enqueue(entry);

        private void FlushLoop()
        {
            while (!_disposed && !_worker.CancellationPending)
            {
                try
                {
                    if (_collection == null)
                    {
                        System.Threading.Thread.Sleep(300);
                        continue;
                    }

                    var batch = new System.Collections.Generic.List<WriteModel<BsonDocument>>(32);
                    while (batch.Count < 32 && _queue.TryDequeue(out var e))
                    {
                        var doc = new BsonDocument
                        {
                            { "Timestamp", e.Timestamp },
                            { "EventType", e.EventType },
                            { "Floor", e.Floor },
                            { "Status", e.Status }
                        };
                        if (e.TravelMs.HasValue) doc.Add("TravelMs", e.TravelMs.Value);
                        batch.Add(new InsertOneModel<BsonDocument>(doc));
                    }

                    if (batch.Count > 0)
                        _collection.BulkWrite(batch);
                    else
                        System.Threading.Thread.Sleep(100);
                }
                catch (MongoConnectionException)
                {
                    IsHealthy = false;
                    System.Threading.Thread.Sleep(1000);
                }
                catch
                {
                    System.Threading.Thread.Sleep(300);
                }
            }
        }

        public BsonDocument[] GetAllLogsSafe()
        {
            try
            {
                if (_collection == null) return Array.Empty<BsonDocument>();
                return _collection.Find(FilterDefinition<BsonDocument>.Empty)
                                  .Sort(Builders<BsonDocument>.Sort.Descending("Timestamp"))
                                  .ToList()
                                  .ToArray();
            }
            catch
            {
                return Array.Empty<BsonDocument>();
            }
        }

        public void Dispose()
        {
            _disposed = true;
            if (_worker.IsBusy) _worker.CancelAsync();
        }
    }
}
