using System.IO;
using PharmaLabVR.Tools;
using PharmaLabVR.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PharmaLabVR.Editor
{
    public static class BuildLabAssets
    {
        public static void Generate()
        {
            Directory.CreateDirectory("Assets/PharmaLabVR/Prefabs/Tools");
            Directory.CreateDirectory("Assets/PharmaLabVR/Prefabs/UI");
            Directory.CreateDirectory("Assets/PharmaLabVR/Prefabs/Environment");
            Directory.CreateDirectory("Assets/PharmaLabVR/Materials");
            GenerateEnvironment();
            GenerateResearchBurette();
            GenerateMeasurementPanel();
            AssetDatabase.SaveAssets();
        }


        private static Material CreateMaterial(string name, Color color, float metallic, float smoothness)
        {
            var path = "Assets/PharmaLabVR/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) throw new System.InvalidOperationException("A surface shader is required.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType primitive,
            Vector3 position, Vector3 size, Material material, bool collider = false)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            part.GetComponent<Collider>().enabled = collider;
            return part;
        }

        // Persist inspectable geometry as real prefab/material assets instead of creating
        // the classroom shell on every runtime launch. Apparatus is not parented to decor.
        private static void GenerateEnvironment()
        {
            var plaster = CreateMaterial("WarmWhiteWall", new Color(0.86f, 0.90f, 0.89f), 0f, 0.42f);
            var floorMat = CreateMaterial("PaleLaboratoryFloor", new Color(0.64f, 0.70f, 0.71f), 0f, 0.28f);
            var graphite = CreateMaterial("GraphiteBench", new Color(0.20f, 0.27f, 0.30f), 0.04f, 0.30f);
            var satin = CreateMaterial("SatinMetal", new Color(0.49f, 0.55f, 0.56f), 0.58f, 0.55f);
            var teal = CreateMaterial("MutedTeal", new Color(0.15f, 0.38f, 0.40f), 0.02f, 0.32f);
            var cabinetMat = CreateMaterial("CabinetWhite", new Color(0.75f, 0.81f, 0.81f), 0.12f, 0.35f);

            var room = new GameObject("PremiumLabShell");
            try
            {
                Part(room.transform, "LaboratoryFloor", PrimitiveType.Cube,
                    new Vector3(0f, -0.10f, 0.3f), new Vector3(6.0f, 0.18f, 5.2f), floorMat, true);
                Part(room.transform, "RearWall", PrimitiveType.Cube,
                    new Vector3(0f, 1.40f, 2.85f), new Vector3(6.0f, 2.8f, 0.15f), plaster);
                Part(room.transform, "LeftWall", PrimitiveType.Cube,
                    new Vector3(-3f, 1.40f, 0.3f), new Vector3(0.15f, 2.8f, 5.2f), plaster);
                Part(room.transform, "RightWall", PrimitiveType.Cube,
                    new Vector3(3f, 1.40f, 0.3f), new Vector3(0.15f, 2.8f, 5.2f), plaster);
                Part(room.transform, "Ceiling", PrimitiveType.Cube,
                    new Vector3(0f, 2.85f, 0.3f), new Vector3(6.0f, 0.10f, 5.2f), plaster);
                foreach (var x in new[] { -2.25f, 2.25f })
                {
                    var cabinet = new GameObject(x < 0 ? "LeftCabinet" : "RightCabinet");
                    cabinet.transform.SetParent(room.transform, false);
                    Part(cabinet.transform, "CabinetBody", PrimitiveType.Cube,
                        new Vector3(x, 0.76f, 1.95f), new Vector3(0.8f, 1.5f, 0.52f), cabinetMat);
                    Part(cabinet.transform, "DoorHandle", PrimitiveType.Cylinder,
                        new Vector3(x + 0.27f, 0.75f, 1.66f), new Vector3(0.018f, 0.13f, 0.018f), satin);
                }
                foreach (var x in new[] { -0.75f, 0.75f })
                {
                    var fixture = new GameObject(x < 0 ? "LeftTaskLight" : "RightTaskLight");
                    fixture.transform.SetParent(room.transform, false);
                    fixture.transform.localPosition = new Vector3(x, 2.55f, 0.16f);
                    var light = fixture.AddComponent<Light>();
                    light.type = LightType.Spot;
                    light.spotAngle = 75f;
                    light.range = 4f;
                    light.intensity = 4.0f;
                    fixture.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Part(fixture.transform, "LampHousing", PrimitiveType.Cylinder,
                        Vector3.zero, new Vector3(0.20f, 0.04f, 0.20f), satin);
                }
                PrefabUtility.SaveAsPrefabAsset(room,
                    "Assets/PharmaLabVR/Prefabs/Environment/PremiumLabShell.prefab");
            }
            finally { Object.DestroyImmediate(room); }

            var bench = new GameObject("BenchAssembly");
            try
            {
                Part(bench.transform, "BenchSurface", PrimitiveType.Cube,
                    new Vector3(0f, 0.44f, 0.2f), new Vector3(1.8f, 0.08f, 1.1f), graphite, true);
                foreach (var x in new[] { -0.82f, 0.82f })
                    foreach (var z in new[] { -0.26f, 0.66f })
                        Part(bench.transform, "AdjustableStandLeg", PrimitiveType.Cylinder,
                            new Vector3(x, 0.22f, z), new Vector3(0.036f, 0.22f, 0.036f), satin);
                Part(bench.transform, "SafeWorkMat", PrimitiveType.Cube,
                    new Vector3(0.23f, 0.485f, 0.2f), new Vector3(0.75f, 0.008f, 0.56f), teal);
                Part(bench.transform, "BenchEdge", PrimitiveType.Cube,
                    new Vector3(0f, 0.43f, -0.36f), new Vector3(1.82f, 0.018f, 0.018f), satin);
                Part(bench.transform, "CabinetSupport", PrimitiveType.Cube,
                    new Vector3(-0.68f, 0.2f, 0.2f), new Vector3(0.38f, 0.38f, 0.6f), cabinetMat);
                PrefabUtility.SaveAsPrefabAsset(bench,
                    "Assets/PharmaLabVR/Prefabs/Environment/BenchAssembly.prefab");
            }
            finally { Object.DestroyImmediate(bench); }
        }

        private static void GenerateResearchBurette()
        {
            var root = new GameObject("ResearchBurette");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "InspectableGlassBody";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.018f, 0.25f, 0.018f);
            var liquid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            liquid.name = "CommittedLiquid";
            liquid.transform.SetParent(root.transform, false);
            liquid.transform.localScale = new Vector3(0.014f, 0f, 0.014f);
            liquid.GetComponent<Collider>().enabled = false;
            var liquidPresenter = root.AddComponent<LiquidPresenter>();
            liquidPresenter.Configure(liquid.transform, 0.00005, 0.48f, -0.24f);
            var valve = root.AddComponent<ValveActuator>();
            root.AddComponent<ToolPresenter>().Configure("burette-50ml-research-v1", liquidPresenter, valve);
            root.AddComponent<Rigidbody>().isKinematic = false;
            root.AddComponent<XRGrabInteractable>();
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/PharmaLabVR/Prefabs/Tools/ResearchBurette.prefab");
            Object.DestroyImmediate(root);
        }

        private static void GenerateMeasurementPanel()
        {
            var root = new GameObject("MeasurementPanel");
            root.AddComponent<MeasurementView>();
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/PharmaLabVR/Prefabs/UI/MeasurementPanel.prefab");
            Object.DestroyImmediate(root);
        }
    }
}
