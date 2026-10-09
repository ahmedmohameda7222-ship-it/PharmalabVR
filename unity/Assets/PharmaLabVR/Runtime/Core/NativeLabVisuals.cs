using System;
using PharmaLabVR.Tools;
using UnityEngine;

namespace PharmaLabVR.Core
{
    public sealed class NativeLabVisuals : MonoBehaviour
    {
        private CoreDriver driver;
        private LiquidPresenter source;
        private LiquidPresenter receiver;

        public void Configure(CoreDriver core)
        {
            driver = core;
            var roots = core.gameObject.scene.GetRootGameObjects();
            var tool = FindNamed(roots, "DesktopResearchTool");
            var vessel = FindNamed(roots, "ReceiverVessel");
            if (tool == null || vessel == null) return;
            source = CreateLiquid(tool, "NativeSourceLiquid", 0.012f, 0.46f, 0f);
            receiver = CreateLiquid(vessel, "NativeReceiverLiquid", 0.75f, 0.8f, -0.5f);
            core.SnapshotChanged += Apply;
        }

        private static Transform FindNamed(GameObject[] roots, string name)
        {
            foreach (var root in roots)
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name) return child;
            return null;
        }

        private static LiquidPresenter CreateLiquid(Transform parent, string name, float radius, float height, float baseY)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = new Vector3(radius, 0f, radius);
            visual.GetComponent<Collider>().enabled = false;
            visual.GetComponent<Renderer>().material.color = new Color(0.15f, 0.55f, 0.85f, 0.75f);
            var presenter = parent.gameObject.AddComponent<LiquidPresenter>();
            presenter.Configure(visual.transform, 1.0, height, baseY);
            return presenter;
        }

        private void Apply(string snapshot)
        {
            if (source == null || receiver == null) return;
            var state = JsonUtility.FromJson<LabSnapshot>(snapshot);
            if (state?.vessels == null) return;
            foreach (var vessel in state.vessels)
            {
                if (vessel.inventory == null || vessel.capacityM3 <= 0.0) continue;
                if (vessel.id == "source") source.Configure(source.transform.Find("NativeSourceLiquid"), vessel.capacityM3, 0.46f, 0f);
                if (vessel.id == "receiver") receiver.Configure(receiver.transform.Find("NativeReceiverLiquid"), vessel.capacityM3, 0.8f, -0.5f);
                if (vessel.id == "source") source.SetCommittedVolume(vessel.inventory.researchAdditiveVolumeM3);
                if (vessel.id == "receiver") receiver.SetCommittedVolume(vessel.inventory.researchAdditiveVolumeM3);
            }
        }

        private void OnDestroy()
        {
            if (driver != null) driver.SnapshotChanged -= Apply;
        }

        [Serializable]
        private sealed class LabSnapshot { public VesselState[] vessels; }
        [Serializable]
        private sealed class VesselState
        {
            public string id;
            public double capacityM3;
            public MaterialState inventory;
        }
        [Serializable]
        private sealed class MaterialState { public double researchAdditiveVolumeM3; }
    }
}
