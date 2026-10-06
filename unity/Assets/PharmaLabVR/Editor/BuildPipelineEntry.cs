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
            new GameObject("CoreDriver").AddComponent<CoreDriver>().transform.SetParent(lab.transform);
            var xrRig = new GameObject("XRPlayerRig");
            xrRig.AddComponent<XRInputAdapter>();
            xrRig.transform.SetParent(lab.transform);
            xrRig.SetActive(false);
            var desktopRig = new GameObject("DesktopPlayerRig");
            desktopRig.AddComponent<DesktopInputAdapter>();
            desktopRig.transform.SetParent(lab.transform);
            new GameObject("LabPanels").transform.SetParent(lab.transform);
            new GameObject("SessionController").transform.SetParent(lab.transform);
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
