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
            GenerateResearchPipette();
            GenerateResearchStockBottle();
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
            var meniscus = root.AddComponent<MeniscusPresenter>();
            meniscus.Configure(root.transform, 0.00005, 0.48f, -0.24f, 0.006f);
            // Runtime-generated meshes/materials are NOT persistent prefab references.
            // Save them as project sub-assets before serializing the tool prefab.
            var surfaceFilter = meniscus.Surface.GetComponent<MeshFilter>();
            const string meshPath = "Assets/PharmaLabVR/Materials/ResearchMeniscus.asset";
            var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (savedMesh == null)
                AssetDatabase.CreateAsset(surfaceFilter.sharedMesh, meshPath);
            else
            {
                Object.DestroyImmediate(surfaceFilter.sharedMesh);
                surfaceFilter.sharedMesh = savedMesh;
            }
            var surfaceRenderer = meniscus.Surface.GetComponent<MeshRenderer>();
            const string meniscusMatPath = "Assets/PharmaLabVR/Materials/ResearchMeniscus.mat";
            var savedMaterial = AssetDatabase.LoadAssetAtPath<Material>(meniscusMatPath);
            if (savedMaterial == null)
                AssetDatabase.CreateAsset(surfaceRenderer.sharedMaterial, meniscusMatPath);
            else
            {
                Object.DestroyImmediate(surfaceRenderer.sharedMaterial);
                surfaceRenderer.sharedMaterial = savedMaterial;
            }
            var liquidPresenter = root.AddComponent<LiquidPresenter>();
            liquidPresenter.Configure(liquid.transform, 0.00005, 0.48f, -0.24f);
            var graduationInk = CreateMaterial("GraduationInk", new Color(0.07f, 0.11f, 0.13f), 0f, 0.10f);
            InstrumentMeshBuilder.BuildBuretteGraduations(root.transform, 0.22f, 0.44f, 0.02f,
                graduationInk);
            var valve = root.AddComponent<ValveActuator>();
            root.AddComponent<ToolPresenter>().Configure("burette-50ml-research-v1", liquidPresenter, valve);
            root.AddComponent<Rigidbody>().isKinematic = false;
            var grip = root.AddComponent<XRGrabInteractable>();
            grip.throwOnDetach = false;
            grip.forceGravityOnDetach = false;
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/PharmaLabVR/Prefabs/Tools/ResearchBurette.prefab");
            Object.DestroyImmediate(root);
        }

        // Research geometry only; these are distinct view/actuator prefabs,
        // not calibrated pipettes or a substitute for Native volume transfers.
        private static void GenerateResearchPipette()
        {
            var glass = CreateMaterial("ResearchPipetteGlass",
                new Color(0.77f, 0.91f, 0.95f, 0.55f), 0.02f, 0.75f);
            var ink = CreateMaterial("ResearchPipetteInk",
                new Color(0.06f, 0.16f, 0.19f), 0f, 0.18f);
            var root = new GameObject("ResearchPipette");
            try
            {
                Part(root.transform, "GraduatedGlassTube", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.15f, 0f), new Vector3(0.015f, 0.135f, 0.015f), glass, true);
                var pipetteTip = Part(root.transform, "MeteredOutletTip", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.012f, 0f), new Vector3(0.004f, 0.019f, 0.004f), glass);
                // The real mesh bottom anchors a profile-specific point, not
                // a magic offset inherited from the unrelated burette.
                var pipetteOutlet = new GameObject("OutletExit").transform;
                pipetteOutlet.SetParent(root.transform, false);
                pipetteOutlet.localPosition = pipetteTip.transform.localPosition +
                    Vector3.up * (pipetteTip.GetComponent<MeshFilter>().sharedMesh.bounds.min.y *
                                  pipetteTip.transform.localScale.y);
                pipetteOutlet.localRotation = Quaternion.identity;
                Part(root.transform, "SuctionCollar", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.287f, 0f), new Vector3(0.022f, 0.008f, 0.022f), ink);
                for (var ml = 0; ml <= 25; ++ml)
                {
                    var major = ml % 5 == 0;
                    Part(root.transform, "PipetteScale " + ml + " mL", PrimitiveType.Cube,
                        new Vector3(0.016f, 0.275f - 0.25f * ml / 25f, 0f),
                        new Vector3(major ? 0.012f : 0.005f, 0.001f, 0.0015f), ink);
                }
                var fillMaterial = CreateMaterial("ResearchPipetteLiquid",
                    new Color(0.21f, 0.56f, 0.84f, 0.73f), 0f, 0.55f);
                var liquid = Part(root.transform, "NativePipetteLiquid", PrimitiveType.Cylinder,
                    Vector3.zero, new Vector3(0.012f, 0f, 0.012f), fillMaterial);
                root.AddComponent<NativeMeteredInstrumentVisual>().ConfigureRepresentation(
                    liquid.transform, "research-pipette", true, 25e-6, 0.25f, 0.025f);
                root.AddComponent<PipetteActuator>();
                // The grabbable body is a physical view. Explicit metered
                // operations still need Native commands and valid revisions.
                var pipetteBody = root.AddComponent<Rigidbody>();
                pipetteBody.isKinematic = true;
                pipetteBody.useGravity = false;
                root.AddComponent<XRGrabInteractable>();
                PrefabUtility.SaveAsPrefabAsset(root,
                    "Assets/PharmaLabVR/Prefabs/Tools/ResearchPipette.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void GenerateResearchStockBottle()
        {
            var glass = CreateMaterial("ResearchBottleGlass",
                new Color(0.69f, 0.85f, 0.91f, 0.65f), 0.03f, 0.70f);
            var ink = CreateMaterial("ResearchBottleCap",
                new Color(0.09f, 0.30f, 0.35f), 0.04f, 0.35f);
            var root = new GameObject("ResearchStockBottle");
            try
            {
                Part(root.transform, "StockBottleBody", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.083f, 0f), new Vector3(0.075f, 0.075f, 0.075f), glass, true);
                var bottleNeck = Part(root.transform, "NarrowNeck", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.181f, 0f), new Vector3(0.033f, 0.024f, 0.033f), glass);
                var bottleOutlet = new GameObject("OutletExit").transform;
                bottleOutlet.SetParent(root.transform, false);
                bottleOutlet.localPosition = bottleNeck.transform.localPosition +
                    Vector3.up * (bottleNeck.GetComponent<MeshFilter>().sharedMesh.bounds.max.y *
                                  bottleNeck.transform.localScale.y);
                // Upright closed-cap stock bottle is NOT a downward outlet.
                // Measured pour is explicit Native command; tilt dynamics
                // and cap opening are unvalidated and not inferred here.
                bottleOutlet.localRotation = Quaternion.Euler(180f, 0f, 0f);
                Part(root.transform, "ClosedResearchCap", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.214f, 0f), new Vector3(0.042f, 0.012f, 0.042f), ink);
                var label = new GameObject("100mL_Research_NotCalibrated");
                label.transform.SetParent(root.transform, false);
                label.transform.localPosition = new Vector3(-0.052f, 0.09f, -0.076f);
                var text = label.AddComponent<TextMesh>();
                text.text = "100mL Research";
                text.fontSize = 20;
                text.characterSize = 0.007f;
                text.color = new Color(0.04f, 0.16f, 0.18f);
                var fillMaterial = CreateMaterial("ResearchStockLiquid",
                    new Color(0.18f, 0.54f, 0.82f, 0.73f), 0f, 0.48f);
                var liquid = Part(root.transform, "NativeBottleLiquid", PrimitiveType.Cylinder,
                    Vector3.zero, new Vector3(0.061f, 0f, 0.061f), fillMaterial);
                root.AddComponent<NativeMeteredInstrumentVisual>().ConfigureRepresentation(
                    liquid.transform, "source", false, 1e-4, 0.12f, 0.013f);
                root.AddComponent<BottlePourActuator>();
                var bottleBody = root.AddComponent<Rigidbody>();
                bottleBody.isKinematic = true;
                bottleBody.useGravity = false;
                root.AddComponent<XRGrabInteractable>();
                PrefabUtility.SaveAsPrefabAsset(root,
                    "Assets/PharmaLabVR/Prefabs/Tools/ResearchStockBottle.prefab");
            }
            finally { Object.DestroyImmediate(root); }
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
