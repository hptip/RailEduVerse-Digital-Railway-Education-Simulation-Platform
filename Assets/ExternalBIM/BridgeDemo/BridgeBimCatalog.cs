using System;
using UnityEngine;

namespace UttVr.BridgePilot
{
    [Serializable]
    public sealed class BridgeBimEntry
    {
        public string globalId;
        public string assetId;
        public string parentGlobalId;
        public string ifcClass;
        public string name;
        public string description;
        public string predefinedType;
        public string tag;
        public string[] propertyLines;
        public string sourceGlobalId, sourceParentGlobalId, sourceIfcClass, sourceName;
        public float modeledStation;
        public Vector3 modeledBounds;
        public bool IsElement => !string.IsNullOrEmpty(assetId);
        public string Label => string.IsNullOrEmpty(name) ? ifcClass : name;
    }

    public sealed class BridgeBimCatalog : ScriptableObject
    {
        public const string SO201RouteSchema = "YTH-SO201-DERIVED";
        public string schema;
        public string sourceSha256;
        public BridgeBimEntry[] entries;
    }
}
