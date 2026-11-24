using UnityEngine;

public sealed class RailwayBridgeVisualPlatform : MonoBehaviour
{
    public GameObject so201Visual;
    public GameObject legacyVisual;
    public bool useLegacyOnAndroid = true;

    void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!useLegacyOnAndroid || so201Visual == null || legacyVisual == null) return;
        so201Visual.SetActive(false);
        legacyVisual.SetActive(true);
#endif
    }
}
