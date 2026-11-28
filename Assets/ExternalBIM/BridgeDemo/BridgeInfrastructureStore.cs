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
        public const string ProductionDatabaseName = "RailwayInfrastructure_G2_Production.db";

        readonly SQLiteConnection db;
        readonly HashSet<string> ids;

        public BridgeInfrastructureStore(BridgeBimCatalog catalog) : this(catalog,null) {}

        public BridgeInfrastructureStore(BridgeBimCatalog catalog,string databaseOverride)
        {
            ids = new HashSet<string>(catalog.entries.Where(e => e.IsElement).Select(e => e.globalId), StringComparer.Ordinal);
            string database = DatabaseFile(catalog.schema);
            db = new SQLiteConnection(databaseOverride??Path.Combine(Application.persistentDataPath, database));
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (database == ProductionDatabaseName)
                {
                    // xSync writes the VFS file_size record needed to reopen SQLite.
                    // Keep the rollback journal in memory and persist committed pages from the dashboard.
                    db.ExecuteScalar<string>("PRAGMA journal_mode=MEMORY");
                    db.Execute("PRAGMA synchronous=FULL");
                }
#endif
                db.Execute("CREATE TABLE IF NOT EXISTS BridgeNotes (GlobalId TEXT PRIMARY KEY, TechnicalNote TEXT NOT NULL)");
                db.Execute("CREATE TABLE IF NOT EXISTS BridgeStages (GlobalId TEXT NOT NULL, StageKey TEXT NOT NULL, Status TEXT NOT NULL, Notes TEXT NOT NULL, PRIMARY KEY (GlobalId, StageKey))");
                db.Execute("CREATE TABLE IF NOT EXISTS BridgeParameters (GlobalId TEXT NOT NULL, Name TEXT NOT NULL, Value TEXT NOT NULL, Unit TEXT NOT NULL, PRIMARY KEY (GlobalId, Name))");
                db.Execute("CREATE TABLE IF NOT EXISTS BridgeStageFields (GlobalId TEXT NOT NULL, StageKey TEXT NOT NULL, Name TEXT NOT NULL, Value TEXT NOT NULL, PRIMARY KEY (GlobalId, StageKey, Name))");
            }
            catch { db.Dispose(); throw; }
        }

        static string DatabaseFile(string schema) => schema switch
        {
            "YTH-MODULAR-SIMULATION" => "BridgeYTH_Infrastructure.db",
            BridgeBimCatalog.SO201RouteSchema => "BridgeYTH_SO201Route.db",
            "UTTVR-G2-PILOT-DETAILED" => "RailwayInfrastructure_G2_Pilot.db",
            "UTTVR-G2-PRODUCTION-DETAILED" => ProductionDatabaseName,
            _ => "BridgeSO201_Infrastructure.db"
        };

        public static IReadOnlyCollection<string> CompleteUserProfileIds(BridgeBimCatalog catalog)
        {
            if (catalog == null) return Array.Empty<string>();
            string path = Path.Combine(Application.persistentDataPath, DatabaseFile(catalog.schema));
            // The Web SQLite VFS persists a database as a directory of pages.
            if (!File.Exists(path) && !Directory.Exists(path)) return Array.Empty<string>();
            try
            {
                using var source = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly);
                var ids = source.QueryScalars<string>(
                    @"SELECT p.GlobalId FROM BridgeParameters p
                      WHERE length(trim(p.Value))>0
                        AND (SELECT count(*) FROM BridgeStages s WHERE s.GlobalId=p.GlobalId AND
                             (length(trim(s.Status))>0 OR length(trim(s.Notes))>0))=6
                      GROUP BY p.GlobalId HAVING count(*)>=2").ToHashSet(StringComparer.Ordinal);
                return catalog.entries.Where(e => e.IsElement && ids.Contains(e.globalId)).Select(e => e.assetId).ToArray();
            }
            catch (SQLiteException ex)
            {
                Debug.LogWarning("Không đọc được trạng thái hồ sơ BIM đã lưu: " + ex.Message);
                return Array.Empty<string>();
            }
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

        public void ApplyInfrastructureWorkbook(string id,IReadOnlyList<RailwayBimWorkbook.Sheet> sheets)
        {
            Require(id);
            string Read(Dictionary<string,string> row,string key,string existing)=>
                row.TryGetValue(key,out var value)&&!string.IsNullOrWhiteSpace(value)?value:existing;
            db.RunInTransaction(()=>
            {
                foreach(var sheet in sheets)
                {
                    var row=sheet.rows.Single();
                    if(row["AssetID"]!=id)throw new ArgumentException("Workbook AssetID changed after preview.");
                    if(sheet.name=="Technical")
                    {
                        db.Execute("INSERT OR REPLACE INTO BridgeNotes VALUES (?,?)",id,Read(row,"TechnicalNote",TechnicalNote(id)));
                        var existing=Parameters(id).ToDictionary(item=>item.Name,StringComparer.Ordinal);
                        foreach(var field in row.Where(item=>item.Key.StartsWith("Parameter:",StringComparison.Ordinal)&&!string.IsNullOrWhiteSpace(item.Value)))
                        {
                            string name=field.Key.Substring(10);
                            string unit=Read(row,"Unit:"+name,existing.TryGetValue(name,out var previous)?previous.Unit:"");
                            db.Execute("INSERT OR REPLACE INTO BridgeParameters VALUES (?,?,?,?)",id,name,field.Value,unit);
                        }
                        continue;
                    }
                    var stage=RailwayBimStages.All.Single(candidate=>candidate.sheet==sheet.name);
                    var previousStage=Stage(id,stage.key);
                    db.Execute("INSERT OR REPLACE INTO BridgeStages VALUES (?,?,?,?)",
                        id,stage.key,Read(row,"Status",previousStage.Status),Read(row,"Notes",previousStage.Notes));
                    foreach(var field in stage.fields.Where(field=>!field.readOnly))
                        if(row.TryGetValue(field.key,out var value)&&!string.IsNullOrWhiteSpace(value))
                        {
                            RailwayBimStore.ValueValid(value,field.type);
                            db.Execute("INSERT OR REPLACE INTO BridgeStageFields VALUES (?,?,?,?)",id,stage.key,field.key,value);
                        }
                }
            });
        }
    }
}
