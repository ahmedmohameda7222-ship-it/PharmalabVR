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
            DecorateLab(roots, tool, vessel);
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

        private static void DecorateLab(GameObject[] roots, Transform tool, Transform vessel)
        {
            var bench = FindNamed(roots, "BenchRoot");
            if (bench != null)
            {
                Primitive(bench, "BenchSurface", PrimitiveType.Cube,
                    new Vector3(0f, 0.44f, 0.2f), new Vector3(1.8f, 0.08f, 1.1f),
                    new Color(0.20f, 0.28f, 0.31f), true);
                foreach (var x in new[] { -0.82f, 0.82f })
                    foreach (var z in new[] { -0.26f, 0.66f })
                        Primitive(bench, "BenchLeg", PrimitiveType.Cube,
                            new Vector3(x, 0.2f, z), new Vector3(0.055f, 0.4f, 0.055f),
                            new Color(0.10f, 0.15f, 0.18f), false);
                Primitive(bench, "WorkMat", PrimitiveType.Cube,
                    new Vector3(0.23f, 0.486f, 0.2f), new Vector3(0.75f, 0.008f, 0.56f),
                    new Color(0.32f, 0.39f, 0.40f), false);
            }
            var body = tool.Find("CalibratedBody");
            if (body != null && body.TryGetComponent<Renderer>(out var bodyRenderer)) bodyRenderer.enabled = false;
            for (var index = 0; index < 4; index++)
            {
                var angle = index * Mathf.PI * 0.5f;
                Primitive(tool, "GlassRail", PrimitiveType.Cylinder,
                    new Vector3(0.014f * Mathf.Cos(angle), 0.25f, 0.014f * Mathf.Sin(angle)),
                    new Vector3(0.002f, 0.25f, 0.002f), Color.white, false);
            }
            for (var index = 0; index <= 20; index++)
                Primitive(tool, "Graduation", PrimitiveType.Cube,
                    new Vector3(0.020f, 0.02f + index * 0.022f, 0f),
                    new Vector3(index % 5 == 0 ? 0.018f : 0.010f, 0.0015f, 0.0015f),
                    new Color(0.05f, 0.08f, 0.10f), false);
            Primitive(tool, "Stopcock", PrimitiveType.Cube,
                new Vector3(0f, 0.012f, 0f), new Vector3(0.08f, 0.012f, 0.012f),
                new Color(0.89f, 0.45f, 0.13f), false);
            Primitive(tool, "OutletTip", PrimitiveType.Cylinder,
                new Vector3(0f, -0.018f, 0f), new Vector3(0.005f, 0.025f, 0.005f),
                Color.white, false);
            if (vessel.TryGetComponent<Renderer>(out var vesselRenderer)) vesselRenderer.enabled = false;
            Primitive(vessel, "BeakerBase", PrimitiveType.Cylinder,
                new Vector3(0f, -0.45f, 0f), new Vector3(0.52f, 0.04f, 0.52f),
                new Color(0.80f, 0.86f, 0.88f), false);
            for (var index = 0; index < 8; index++)
            {
                var angle = index * Mathf.PI * 0.25f;
                Primitive(vessel, "BeakerWall", PrimitiveType.Cube,
                    new Vector3(0.48f * Mathf.Cos(angle), 0f, 0.48f * Mathf.Sin(angle)),
                    new Vector3(0.035f, 0.9f, 0.035f),
                    new Color(0.80f, 0.86f, 0.88f), false);
            }
        }

        private static GameObject Primitive(Transform parent, string name, PrimitiveType kind,
            Vector3 position, Vector3 scale, Color color, bool collider)
        {
            var result = GameObject.CreatePrimitive(kind);
            result.name = name;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = position;
            result.transform.localScale = scale;
            result.GetComponent<Renderer>().material.color = color;
            result.GetComponent<Collider>().enabled = collider;
            return result;
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
