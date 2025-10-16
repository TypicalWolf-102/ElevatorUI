using System;
using System.Collections.Generic;
using System.IO;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ElevatorGUI
{
    /// <summary>
    /// Centralised MongoDB access (no duplication in event handlers).
    /// Uses relative config.json near the EXE for portability.
    /// </summary>
    public sealed class MongoLogService
    {
        private readonly IMongoCollection<BsonDocument> _collection;

        public MongoLogService()
        {
            string cfgPath = Path.Combine(AppContext.BaseDirectory, "config.json");
            string connStr = "mongodb://localhost:27017";   // local default
            string dbName = "ElevatorDB";

            if (File.Exists(cfgPath))
            {
                var json = File.ReadAllText(cfgPath);
                var doc = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(json);
                if (doc.Contains("ConnectionString")) connStr = doc["ConnectionString"].AsString;
                if (doc.Contains("DatabaseName")) dbName = doc["DatabaseName"].AsString;
            }

            var client = new MongoClient(connStr);
            var database = client.GetDatabase(dbName);
            _collection = database.GetCollection<BsonDocument>("ElevatorLogs");
        }

        public void InsertLog(string eventType, int floor, string status, int? travelMs = null)
        {
            var doc = new BsonDocument
            {
                { "Timestamp", DateTime.Now },
                { "EventType", eventType },
                { "Floor", floor },
                { "Status", status }
            };

            if (travelMs.HasValue) doc.Add("TravelMs", travelMs.Value);

            _collection.InsertOne(doc);
        }

        public List<BsonDocument> GetAllLogs()
        {
            return _collection.Find(FilterDefinition<BsonDocument>.Empty)
                              .Sort(Builders<BsonDocument>.Sort.Descending("Timestamp"))
                              .ToList();
        }
    }
}
