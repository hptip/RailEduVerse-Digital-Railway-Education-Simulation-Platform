using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

public sealed class UttWebLinker : IUnityLinkerProcessor, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
    {
        if (report.summary.platform != BuildTarget.WebGL) return null;
        // ExcelDataReader initializes a Windows-1252 fallback even for Open XML workbooks.
        const string path = "Temp/UttWebExcel.link.xml";
        File.WriteAllText(path,
            "<linker><assembly fullname=\"I18N\" preserve=\"all\" />" +
            "<assembly fullname=\"I18N.West\" preserve=\"all\" /></linker>");
        return Path.GetFullPath(path);
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;

        var path = Path.Combine(report.summary.outputPath, "index.html");
        var html = File.ReadAllText(path);
        const string autoSync = "config.autoSyncPersistentDataPath = true;";
        const string queuedSync = "config.autoSyncPersistentDataPath = false;";
        if (html.Contains(queuedSync)) return;

        // The SQLite idbvfs xSync implementation already serializes FS.syncfs calls.
        // Unity's independent auto-sync can overlap that queue while a journal is open.
        var updated = html.Replace("// " + autoSync, queuedSync).Replace(autoSync, queuedSync);
        if (updated == html)
            throw new BuildFailedException("The UTTVR Web template must expose autoSyncPersistentDataPath for the SQLite sync queue.");

        File.WriteAllText(path, updated);
    }
}
