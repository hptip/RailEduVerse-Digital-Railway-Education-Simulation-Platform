using System;
using UnityEngine;

public sealed class RailwayBridgeReplacementSandbox : MonoBehaviour
{
    public GameObject oldBridge;
    public GameObject newBridge;

    // Called only after an aligned, reviewed candidate is assigned in the sandbox.
    public bool TryShowReplacement(bool showReplacement)
    {
        if (oldBridge == null || (showReplacement && newBridge == null)) return false;
        if (newBridge != null) newBridge.SetActive(false);
        oldBridge.SetActive(!showReplacement);
        if (showReplacement) newBridge.SetActive(true);
        return true;
    }

    [ContextMenu("Show OldBridge")]
    void ShowOldBridge() => TryShowReplacement(false);

    [ContextMenu("Show NewBridge")]
    void ShowNewBridge()
    {
        if (!TryShowReplacement(true)) Debug.LogWarning("Chưa có model cầu đường sắt tương thích.");
    }
}
