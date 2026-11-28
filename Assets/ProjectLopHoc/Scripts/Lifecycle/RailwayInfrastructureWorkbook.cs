using System;
using System.Collections.Generic;
using System.Linq;
using UttVr.BridgePilot;

public static class RailwayInfrastructureWorkbook
{
    public static List<RailwayBimWorkbook.Sheet> Export(BridgeInfrastructureStore store,BridgeBimEntry entry)
    {
        var result=new List<RailwayBimWorkbook.Sheet>();
        var technical=new RailwayBimWorkbook.Sheet{name="Technical"};
        var row=new Dictionary<string,string>{{"AssetID",entry.assetId},{"TechnicalNote",store.TechnicalNote(entry.assetId)}};
        foreach(var parameter in store.Parameters(entry.assetId))
        {
            row[$"Parameter:{parameter.Name}"]=parameter.Value;
            row[$"Unit:{parameter.Name}"]=parameter.Unit;
        }
        technical.headers.AddRange(row.Keys);
        technical.rows.Add(row);
        result.Add(technical);
        foreach(var stage in RailwayBimStages.All)
        {
            var values=store.Stage(entry.assetId,stage.key);
            var sheet=new RailwayBimWorkbook.Sheet{name=stage.sheet};
            var stageRow=new Dictionary<string,string>{{"AssetID",entry.assetId},{"Status",values.Status},{"Notes",values.Notes}};
            foreach(var field in stage.fields.Where(field=>!field.readOnly))
                stageRow[field.key]=store.StageField(entry.assetId,stage.key,field.key);
            sheet.headers.AddRange(stageRow.Keys);
            sheet.rows.Add(stageRow);
            result.Add(sheet);
        }
        return result;
    }

    public static List<RailwayBimWorkbook.Sheet> Read(string path,string assetId)
    {
        var sheets=RailwayBimWorkbook.Read(path);
        foreach(var sheet in sheets)
        {
            if(sheet.rows.Count!=1||sheet.rows[0]["AssetID"]!=assetId)
                throw new ArgumentException("Tệp phải khớp đúng AssetID đang chọn và có một dòng mỗi sheet.");
            var row=sheet.rows[0];
            if(sheet.name=="Technical")
            {
                if(row.Keys.Any(key=>key!="AssetID"&&key!="TechnicalNote"&&
                    !(key.StartsWith("Parameter:",StringComparison.Ordinal)&&key.Length>10)&&
                    !(key.StartsWith("Unit:",StringComparison.Ordinal)&&key.Length>5)))
                    throw new ArgumentException("Cột thông số hạ tầng không hợp lệ.");
                continue;
            }
            var stage=RailwayBimStages.All.SingleOrDefault(item=>item.sheet==sheet.name);
            if(stage==null)throw new ArgumentException("Sheet ngoài cấu trúc vòng đời hạ tầng.");
            var fields=stage.fields.Where(field=>!field.readOnly).ToDictionary(field=>field.key);
            foreach(var field in row.Where(field=>field.Key!="AssetID"&&field.Key!="Status"&&field.Key!="Notes"))
            {
                if(!fields.TryGetValue(field.Key,out var spec))throw new ArgumentException("Cột vòng đời không hợp lệ.");
                if(!string.IsNullOrWhiteSpace(field.Value))RailwayBimStore.ValueValid(field.Value,spec.type);
            }
        }
        return sheets;
    }
}
