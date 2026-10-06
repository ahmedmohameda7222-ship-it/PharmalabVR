using System;
using System.IO;
using PharmaLabVR.Core;
using PharmaLabVR.Input;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PharmaLabVR.Editor
{
    public static class BuildPipelineEntry
    {
        private static readonly string[] Scenes = { "Assets/PharmaLabVR/Scenes/Boot.unity", "Assets/PharmaLabVR/Scenes/Lab.unity", "Assets/PharmaLabVR/Scenes/Review.unity" };

        public static void AllAssets()
        {
            Directory.CreateDirectory("Assets/PharmaLabVR/Scenes");
            CreateScene(Scenes[0], "BootRoot");
            CreateLabScene();
            CreateScene(Scenes[2], "ReviewRoot");
            AssetDatabase.SaveAssets();
        }

        private static void CreateScene(string path, string rootName)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject(rootName);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void CreateLabScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lab = new GameObject("LabRoot");
            new GameObject("BenchRoot").transform.SetParent(lab.transform);
            new GameObject("ToolRegistry").transform.SetParent(lab.transform);
            new GameObject("VesselRegistry").transform.SetParent(lab.transform);
            var coreDriver = new GameObject("CoreDriver").AddComponent<CoreDriver>();
            coreDriver.transform.SetParent(lab.transform);
            var xrRig = new GameObject("XRPlayerRig");
            xrRig.AddComponent<XRInputAdapter>();
            xrRig.transform.SetParent(lab.transform);
            xrRig.SetActive(false);
            var desktopRig = new GameObject("DesktopPlayerRig");
            var desktopInput = desktopRig.AddComponent<DesktopInputAdapter>();
            desktopRig.transform.SetParent(lab.transform);
            var camera = new GameObject("DesktopCamera");
            camera.AddComponent<Camera>();
            camera.AddComponent<DesktopCameraController>();
            camera.transform.SetParent(desktopRig.transform);
            camera.transform.localPosition = new Vector3(0f, 1.55f, -1.2f);
            var holdAnchor = new GameObject("ToolHoldAnchor");
            holdAnchor.transform.SetParent(camera.transform);
            holdAnchor.transform.localPosition = new Vector3(0.25f, -0.18f, 0.65f);
            var researchTool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            researchTool.name = "DesktopResearchTool";
            researchTool.transform.SetParent(lab.transform);
            researchTool.transform.localPosition = new Vector3(0.25f, 0.85f, 0.2f);
            researchTool.transform.localScale = new Vector3(0.025f, 0.25f, 0.025f);
            researchTool.AddComponent<DesktopGrabbable>();
            desktopInput.Configure(lab.transform, camera.GetComponent<Camera>(), holdAnchor.transform, "burette-50ml-research-v1");
            var modes = new GameObject("ModeCoordinator").AddComponent<ModeCoordinator>();
            modes.transform.SetParent(lab.transform);
            modes.Configure(desktopRig, xrRig);
            coreDriver.ConfigureInputs(desktopInput, xrRig.GetComponent<XRInputAdapter>());
            new GameObject("LabPanels").transform.SetParent(lab.transform);
            var sessionController = new GameObject("SessionController").AddComponent<PharmaLabVR.Session.SessionController>();
            sessionController.Configure(coreDriver);
            sessionController.transform.SetParent(lab.transform);
            new GameObject("PerformanceRecorder").transform.SetParent(lab.transform);
            EditorSceneManager.SaveScene(scene, Scenes[1]);
        }

        public static void WindowsDesktop() => Build(BuildTarget.StandaloneWindows64, "../artifacts/WindowsDesktop/PharmaLabVR.exe");
        public static void WindowsVR() => Build(BuildTarget.StandaloneWindows64, "../artifacts/WindowsVR/PharmaLabVR.exe");
        public static void AndroidVR() => Build(BuildTarget.Android, "../artifacts/AndroidVR/PharmaLabVR-research.apk");
        public static void AndroidVRPico() => Build(BuildTarget.Android, "../artifacts/AndroidVRPico/PharmaLabVR-pico-research.apk");

        private static void Build(BuildTarget target, string location)
        {
            AllAssets();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, target = target, locationPathName = location, options = BuildOptions.StrictMode });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException($"Build failed: {report.summary.result}");
        }
    }
}
