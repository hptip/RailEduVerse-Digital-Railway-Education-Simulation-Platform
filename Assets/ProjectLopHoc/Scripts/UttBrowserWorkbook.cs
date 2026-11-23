using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

public sealed class UttBrowserWorkbook : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void UttPickWorkbook(string receiver);
    [DllImport("__Internal")] private static extern void UttDownloadWorkbook(string fileName, byte[] data, int size);
#endif
    private Action<string> import;
    private Action<string> error;

    public static void Pick(Component owner, Action<string> onImport, Action<string> onError)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var receiver = new GameObject("WorkbookPicker_" + Guid.NewGuid().ToString("N"));
        receiver.transform.SetParent(owner.transform, false);
        var picker = receiver.AddComponent<UttBrowserWorkbook>();
        picker.import = onImport;
        picker.error = onError;
        UttPickWorkbook(receiver.name);
#endif
    }

    [Preserve]
    public void ReceiveWorkbook(string payload)
    {
        string path = null;
        try
        {
            if (string.IsNullOrEmpty(payload)) return;
            if (payload[0] == '!') { error?.Invoke(payload.Substring(1)); return; }
            path = Path.Combine(Application.temporaryCachePath, gameObject.name + ".xlsx");
            Directory.CreateDirectory(Application.temporaryCachePath);
            File.WriteAllBytes(path, Convert.FromBase64String(payload));
            import?.Invoke(path);
        }
        catch (Exception exception) { error?.Invoke(exception.Message); }
        finally
        {
            if (path != null) File.Delete(path);
            Destroy(gameObject);
        }
    }

    public static void Download(string name, Action<string> write)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Directory.CreateDirectory(Application.temporaryCachePath);
        var path = Path.Combine(Application.temporaryCachePath, Guid.NewGuid() + ".xlsx");
        try
        {
            write(path);
            var bytes = File.ReadAllBytes(path);
            UttDownloadWorkbook(name, bytes, bytes.Length);
        }
        finally { File.Delete(path); }
#endif
    }
}
