using System;
using UnityEngine;

namespace PharmaLabVR.Tools
{
    // A view of a represented native parcel. Its GameObject never owns material.
    public sealed class DropPresenter : MonoBehaviour
    {
        [SerializeField] private string toolId = "research-tool";
        [SerializeField] private GameObject visual;
        public string ActiveParcelId { get; private set; } = string.Empty;
        public double CommittedVolumeM3 { get; private set; }

        public void Configure(string id, GameObject dropVisual)
        {
            toolId = id;
            visual = dropVisual;
            ApplySnapshot(null);
        }

        public void ApplySnapshot(string snapshot)
        {
            var envelope = string.IsNullOrEmpty(snapshot) ? null : JsonUtility.FromJson<Snapshot>(snapshot);
            ActiveParcelId = string.Empty;
            CommittedVolumeM3 = 0.0;
            if (envelope?.tools != null)
                foreach (var tool in envelope.tools)
                    if (tool != null && tool.id == toolId && tool.inFlightInventory != null)
                    {
                        CommittedVolumeM3 = tool.inFlightInventory.researchAdditiveVolumeM3;
                        if (CommittedVolumeM3 > 0.0) ActiveParcelId = tool.inFlightParcelId ?? string.Empty;
                        break;
                    }
            if (visual != null) visual.SetActive(CommittedVolumeM3 > 0.0);
        }

        [Serializable] private sealed class Snapshot { public Tool[] tools; }
        [Serializable] private sealed class Tool
        {
            public string id;
            public string inFlightParcelId;
            public Inventory inFlightInventory;
        }
        [Serializable] private sealed class Inventory { public double researchAdditiveVolumeM3; }
    }
}
