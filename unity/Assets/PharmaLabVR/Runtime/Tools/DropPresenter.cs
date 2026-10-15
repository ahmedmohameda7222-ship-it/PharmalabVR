using System;
using UnityEngine;

namespace PharmaLabVR.Tools
{
    // A presentation of represented Native material, never a material owner.
    public sealed class DropPresenter : MonoBehaviour
    {
        [SerializeField] private string toolId = "research-tool";
        [SerializeField] private GameObject visual;
        private readonly DropParcelState state = new DropParcelState();
        public string ActiveParcelId => state.ActiveParcelId;
        public double CommittedVolumeM3 => state.CommittedVolumeM3;

        public void Configure(string id, GameObject dropVisual)
        {
            toolId = id;
            visual = dropVisual;
            ApplySnapshot(null);
        }

        public void ApplySnapshot(string snapshot)
        {
            var envelope = string.IsNullOrEmpty(snapshot) ? null : JsonUtility.FromJson<Snapshot>(snapshot);
            var id = string.Empty;
            var volume = 0.0;
            if (envelope?.tools != null)
                foreach (var tool in envelope.tools)
                    if (tool != null && tool.id == toolId && tool.inFlightInventory != null)
                    {
                        id = tool.inFlightParcelId;
                        volume = tool.inFlightInventory.researchAdditiveVolumeM3;
                        break;
                    }
            state.ApplySnapshot(id, volume);
            RefreshVisual();
        }

        public void ApplyNativeEvent(string eventJson)
        {
            if (string.IsNullOrEmpty(eventJson)) return;
            var receipt = JsonUtility.FromJson<ParcelEvent>(eventJson);
            if (receipt?.type != "InFlightLanded" || receipt.toolId != toolId) return;
            if (state.Landed(receipt.parcelId)) RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (visual != null) visual.SetActive(state.CommittedVolumeM3 > 0.0);
        }

        [Serializable] private sealed class Snapshot { public Tool[] tools; }
        [Serializable] private sealed class Tool
        {
            public string id;
            public string inFlightParcelId;
            public Inventory inFlightInventory;
        }
        [Serializable] private sealed class Inventory { public double researchAdditiveVolumeM3; }
        [Serializable] private sealed class ParcelEvent
        {
            public string type;
            public string toolId;
            public string parcelId;
        }
    }
}
