using UnityEngine;

namespace UttVr.BridgePilot
{
    public sealed class BridgeBimIdentity : MonoBehaviour
    {
        [SerializeField] string globalId;
        [SerializeField] Renderer[] renderers;

        public string GlobalId => globalId;
        public Renderer[] Renderers => renderers;

        public void Configure(string id, Renderer[] renderersForId)
        {
            globalId = id;
            renderers = renderersForId;
        }
    }
}
