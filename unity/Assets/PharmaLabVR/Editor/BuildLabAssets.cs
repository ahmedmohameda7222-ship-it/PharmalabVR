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
            GenerateResearchBurette();
            GenerateMeasurementPanel();
            AssetDatabase.SaveAssets();
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
