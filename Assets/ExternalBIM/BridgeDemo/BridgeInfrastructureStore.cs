using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SQLite;
using UnityEngine;

namespace UttVr.BridgePilot
{
    [UnityEngine.Scripting.PreserveAttribute]
    public sealed class BridgeParameter
    {
        [UnityEngine.Scripting.PreserveAttribute] public string Name { get; set; }
        [UnityEngine.Scripting.PreserveAttribute] public string Value { get; set; }
        [UnityEngine.Scripting.PreserveAttribute] public string Unit { get; set; }
    }

    // User annotations only. IFC facts stay in the immutable catalog.
    public sealed class BridgeInfrastructureStore : IDisposable
    {
        readonly SQLiteConnection db;
        readonly HashSet<string> ids;

        public BridgeInfrastructureStore(BridgeBimCatalog catalog)
        {
            ids = new HashSet<string>(catalog.entries.Where(e => e.IsElement).Select(e => e.globalId), StringComparer.Ordinal);
            string database = catalog.schema switch
            {
                "YTH-MODULAR-SIMULATION" => "BridgeYTH_Infrastructure.db",
                BridgeBimCatalog.SO201RouteSchema => "BridgeYTH_SO201Route.db",
                _ => "BridgeSO201_Infrastructure.db"
            };
            db = new SQLiteConnection(Path.Combine(Application.persistentDataPath, database));
            try
            {
                db.Execute("CREATE TABLE IF NOT EXISTS BridgeNotes (GlobalId TEXT PRIMARY KEY, TechnicalNote TEXT NOT NULL)");
                db.Execute("CREATE TABLE IF NOT EXISTS BridgeStages (GlobalId TEXT NOT NULL, StageKey TEXT NOT NULL, Status TEXT NOT NULL, Notes TEXT NOT NULL, PRIMARY KEY (GlobalId, StageKey))");
                db.Execute("CREATE TABLE IF NOT EXISTS BridgeParameters (GlobalId TEXT NOT NULL, Name TEXT NOT NULL, Value TEXT NOT NULL, Unit TEXT NOT NULL, PRIMARY KEY (GlobalId, Name))");
                db.Execute("CREATE TABLE IF NOT EXISTS BridgeStageFields (GlobalId TEXT NOT NULL, StageKey TEXT NOT NULL, Name TEXT NOT NULL, Value TEXT NOT NULL, PRIMARY KEY (GlobalId, StageKey, Name))");
            }
            catch { db.Dispose(); throw; }
        }

        void Require(string id)
        {
            if (!ids.Contains(id)) throw new ArgumentException("Mã cấu kiện không thuộc danh mục cầu.", nameof(id));
        }

        public string TechnicalNote(string id)
        {
            Require(id);
            return db.ExecuteScalar<string>("SELECT TechnicalNote FROM BridgeNotes WHERE GlobalId=?", id) ?? "";
        }

        public (string Status, string Notes) Stage(string id, string stage)
        {
            Require(id);
            RailwayBimStages.Get(stage);
            return (db.ExecuteScalar<string>("SELECT Status FROM BridgeStages WHERE GlobalId=? AND StageKey=?", id, stage) ?? "",
                db.ExecuteScalar<string>("SELECT Notes FROM BridgeStages WHERE GlobalId=? AND StageKey=?", id, stage) ?? "");
        }

        public List<BridgeParameter> Parameters(string id)
        {
            Require(id);
            return db.Query<BridgeParameter>("SELECT Name, Value, Unit FROM BridgeParameters WHERE GlobalId=? ORDER BY Name", id);
        }

        public void SaveTechnical(string id, string note, IEnumerable<BridgeParameter> parameters)
        {
            Require(id);
            db.RunInTransaction(() =>
            {
                db.Execute("INSERT OR REPLACE INTO BridgeNotes VALUES (?,?)", id, note);
                foreach (var parameter in parameters)
                    db.Execute("INSERT OR REPLACE INTO BridgeParameters VALUES (?,?,?,?)",
                        id, parameter.Name, parameter.Value, parameter.Unit);
            });
        }

        public string StageField(string id, string stage, string key)
        {
            Require(id);
            return db.ExecuteScalar<string>("SELECT Value FROM BridgeStageFields WHERE GlobalId=? AND StageKey=? AND Name=?",
                id, stage, key) ?? "";
        }

        public void SaveStage(string id, string stage, string status, string notes, IReadOnlyDictionary<string, string> fields)
        {
            Require(id);
            var definitions = RailwayBimStages.Get(stage).fields.Where(f => !f.readOnly).ToDictionary(f => f.key);
            foreach (var field in fields)
                if (!string.IsNullOrWhiteSpace(field.Value)) RailwayBimStore.ValueValid(field.Value, definitions[field.Key].type);

            db.RunInTransaction(() =>
            {
                db.Execute("INSERT OR REPLACE INTO BridgeStages VALUES (?,?,?,?)", id, stage, status, notes);
                foreach (var field in fields)
                {
                    if (string.IsNullOrWhiteSpace(field.Value))
                        db.Execute("DELETE FROM BridgeStageFields WHERE GlobalId=? AND StageKey=? AND Name=?", id, stage, field.Key);
                    else db.Execute("INSERT OR REPLACE INTO BridgeStageFields VALUES (?,?,?,?)", id, stage, field.Key, field.Value);
                }
            });
        }

        public void Dispose() => db.Dispose();
    }
}
