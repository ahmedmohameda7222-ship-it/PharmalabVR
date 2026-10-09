using System;
using System.IO;
using PharmaLabVR.Core;
using PharmaLabVR.Input;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using PharmaLabVR.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using Unity.XR.CoreUtils;

namespace PharmaLabVR.Editor
{
    public static class BuildPipelineEntry
    {
        private static readonly string[] Scenes = { "Assets/PharmaLabVR/Scenes/Boot.unity", "Assets/PharmaLabVR/Scenes/Lab.unity", "Assets/PharmaLabVR/Scenes/Review.unity" };

        public static void AllAssets()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureNativePlugins();
            ConfigureOpenXrLoaders();
            Directory.CreateDirectory("Assets/PharmaLabVR/Scenes");
            BuildLabAssets.Generate();
            CreateBootScene();
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

        private static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("BootRoot");
            root.AddComponent<BootMenu>();
            var camera = new GameObject("BootCamera");
            camera.AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            EditorSceneManager.SaveScene(scene, Scenes[0]);
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
            xrRig.transform.SetParent(lab.transform);
            var xrOrigin = xrRig.AddComponent<XROrigin>();
            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(xrRig.transform, false);
            var xrCameraObject = new GameObject("XR Camera");
            xrCameraObject.transform.SetParent(cameraOffset.transform, false);
            var xrCamera = xrCameraObject.AddComponent<Camera>();
            xrCameraObject.AddComponent<AudioListener>();
            var headPose = xrCameraObject.AddComponent<TrackedPoseDriver>();
            headPose.positionInput = Action("Head Position", InputActionType.Value, "Vector3", "<XRHMD>/devicePosition");
            headPose.rotationInput = Action("Head Rotation", InputActionType.Value, "Quaternion", "<XRHMD>/deviceRotation");
            headPose.trackingStateInput = Action("Head Tracking State", InputActionType.Value, "Integer", "<XRHMD>/trackingState");
            xrOrigin.CameraFloorOffsetObject = cameraOffset;
            xrOrigin.Camera = xrCamera;
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            var interactionManager = new GameObject("XRInteractionManager");
            interactionManager.transform.SetParent(xrRig.transform, false);
            interactionManager.AddComponent<XRInteractionManager>();
            var leftController = CreateXrController(cameraOffset.transform, true);
            var rightController = CreateXrController(cameraOffset.transform, false);
            var xrInput = xrRig.AddComponent<XRInputAdapter>();
            xrRig.SetActive(false);
            var desktopRig = new GameObject("DesktopPlayerRig");
            var desktopInput = desktopRig.AddComponent<DesktopInputAdapter>();
            desktopRig.transform.SetParent(lab.transform);
            var camera = new GameObject("DesktopCamera");
            camera.AddComponent<Camera>();
            camera.AddComponent<DesktopCameraController>();
            camera.transform.SetParent(desktopRig.transform);
            camera.transform.localPosition = new Vector3(0f, 1.55f, -1.2f);
            var holdAnchor = new GameObject("DesktopToolHoldAnchor");
            holdAnchor.transform.SetParent(desktopRig.transform, false);
            holdAnchor.AddComponent<DesktopHoldAnchorFollower>().Configure(
                camera.transform,
                new Vector3(0.25f, -0.18f, 0.65f));
            var researchTool = new GameObject("ResearchBurette");
            researchTool.name = "DesktopResearchTool";
            researchTool.transform.SetParent(lab.transform);
            researchTool.transform.localPosition = new Vector3(0.25f, 0.60f, 0.2f);
            var toolVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            toolVisual.name = "CalibratedBody";
            toolVisual.transform.SetParent(researchTool.transform, false);
            toolVisual.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            toolVisual.transform.localScale = new Vector3(0.025f, 0.25f, 0.025f);
            researchTool.AddComponent<DesktopGrabbable>();
            var body = researchTool.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = false;
            var grab = researchTool.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            var receiver = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            receiver.name = "ReceiverVessel";
            receiver.transform.SetParent(lab.transform);
            receiver.transform.localPosition = new Vector3(0.25f, 0.53f, 0.2f);
            receiver.transform.localScale = new Vector3(0.10f, 0.08f, 0.10f);
            var captureTarget = receiver.AddComponent<LabCaptureTarget>();
            captureTarget.Configure("receiver", 0.05f, 0.04f);
            desktopInput.Configure(lab.transform, camera.GetComponent<Camera>(), holdAnchor.transform,
                researchTool.transform, captureTarget, "burette-50ml-research-v1", coreDriver);
            xrInput.Configure(lab.transform, leftController, rightController, researchTool.transform, grab,
                captureTarget, "burette-50ml-research-v1");
            var modes = new GameObject("ModeCoordinator").AddComponent<ModeCoordinator>();
            modes.transform.SetParent(lab.transform);
            modes.Configure(desktopRig, xrRig, coreDriver);
            coreDriver.ConfigureInputs(desktopInput, xrRig.GetComponent<XRInputAdapter>());
            new GameObject("LabPanels").transform.SetParent(lab.transform);
            var sessionController = new GameObject("SessionController").AddComponent<PharmaLabVR.Session.SessionController>();
            sessionController.Configure(coreDriver);
            sessionController.transform.SetParent(lab.transform);
            new GameObject("PerformanceRecorder").transform.SetParent(lab.transform);
            EditorSceneManager.SaveScene(scene, Scenes[1]);
        }

        private static ActionBasedController CreateXrController(Transform parent, bool leftHand)
        {
            var hand = leftHand ? "Left" : "Right";
            var usage = leftHand ? "LeftHand" : "RightHand";
            var controllerObject = new GameObject($"{hand} Controller");
            controllerObject.transform.SetParent(parent, false);
            var controller = controllerObject.AddComponent<ActionBasedController>();
            controller.positionAction = Action($"{hand} Position", InputActionType.Value, "Vector3", $"<XRController>{{{usage}}}/devicePosition");
            controller.rotationAction = Action($"{hand} Rotation", InputActionType.Value, "Quaternion", $"<XRController>{{{usage}}}/deviceRotation");
            controller.isTrackedAction = Action($"{hand} Is Tracked", InputActionType.Button, "Button", $"<XRController>{{{usage}}}/isTracked");
            controller.trackingStateAction = Action($"{hand} Tracking State", InputActionType.Value, "Integer", $"<XRController>{{{usage}}}/trackingState");
            controller.selectAction = Action($"{hand} Select", InputActionType.Button, "Button", $"<XRController>{{{usage}}}/gripPressed");
            controller.selectActionValue = Action($"{hand} Select Value", InputActionType.Value, "Axis", $"<XRController>{{{usage}}}/grip");
            controller.activateAction = Action($"{hand} Activate", InputActionType.Button, "Button", $"<XRController>{{{usage}}}/triggerPressed");
            controller.activateActionValue = Action($"{hand} Activate Value", InputActionType.Value, "Axis", $"<XRController>{{{usage}}}/trigger");
            controller.hapticDeviceAction = Action($"{hand} Haptic Device", InputActionType.PassThrough, string.Empty, $"<XRController>{{{usage}}}/*");

            var direct = new GameObject($"{hand} Direct Interactor");
            direct.transform.SetParent(controllerObject.transform, false);
            var collider = direct.AddComponent<SphereCollider>();
            collider.radius = 0.08f;
            collider.isTrigger = true;
            direct.AddComponent<XRDirectInteractor>();

            var ray = new GameObject($"{hand} Ray Interactor");
            ray.transform.SetParent(controllerObject.transform, false);
            ray.AddComponent<XRRayInteractor>();
            return controller;
        }

        private static InputActionProperty Action(
            string name,
            InputActionType type,
            string expectedControlType,
            string binding)
        {
            var action = new InputAction(name, type, binding, expectedControlType: expectedControlType);
            return new InputActionProperty(action);
        }

        public static void WindowsDesktop() => Build(BuildTarget.StandaloneWindows64, "../artifacts/WindowsDesktop/PharmaLabVR.exe", false);
        public static void WindowsVR() => Build(BuildTarget.StandaloneWindows64, "../artifacts/WindowsVR/PharmaLabVR.exe", true);
        public static void AndroidVR() => Build(BuildTarget.Android, "../artifacts/AndroidVR/PharmaLabVR-research.apk", true);
        public static void AndroidVRPico() => Build(BuildTarget.Android, "../artifacts/AndroidVRPico/PharmaLabVR-pico-research.apk", true);

        private static void ConfigureNativePlugins()
        {
            ConfigurePlugin("Assets/Plugins/x86_64/pharmalab_core.dll", BuildTarget.StandaloneWindows64, "x86_64", true);
            ConfigurePlugin("Assets/Plugins/Android/arm64-v8a/libpharmalab_core.so", BuildTarget.Android, "ARM64", false);
        }

        public static void ConfigureOpenXrLoaders(bool standaloneAutoStart = true, bool androidAutoStart = true)
        {
            const string settingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(settingsPath);
            if (perTarget == null)
            {
                Directory.CreateDirectory("Assets/XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, settingsPath);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
                if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
                var settings = perTarget.SettingsForBuildTarget(group);
                var autoStart = group == BuildTargetGroup.Standalone
                    ? standaloneAutoStart
                    : androidAutoStart;
                settings.InitManagerOnStart = autoStart;
                if (autoStart)
                {
                    if (!XRPackageMetadataStore.AssignLoader(settings.Manager, typeof(OpenXRLoader).FullName, group))
                        throw new InvalidOperationException($"Failed to assign OpenXR loader for {group}.");
                }
                else if (!XRPackageMetadataStore.RemoveLoader(settings.Manager, typeof(OpenXRLoader).FullName, group))
                {
                    throw new InvalidOperationException($"Failed to remove OpenXR loader for {group}.");
                }
                EditorUtility.SetDirty(settings);
                EditorUtility.SetDirty(settings.Manager);
                ConfigureControllerProfiles(group);
            }
            EditorUtility.SetDirty(perTarget);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureControllerProfiles(BuildTargetGroup group)
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (settings == null) throw new InvalidOperationException($"Missing OpenXR package settings for {group}.");
            EnableProfiles(settings.GetFeatures<OculusTouchControllerProfile>());
            EnableProfiles(settings.GetFeatures<KHRSimpleControllerProfile>());
            if (group == BuildTargetGroup.Standalone)
            {
                EnableProfiles(settings.GetFeatures<MicrosoftMotionControllerProfile>());
                EnableProfiles(settings.GetFeatures<HTCViveControllerProfile>());
                EnableProfiles(settings.GetFeatures<ValveIndexControllerProfile>());
            }
            EditorUtility.SetDirty(settings);
        }

        private static void EnableProfiles<T>(T[] profiles) where T : UnityEngine.XR.OpenXR.Features.OpenXRFeature
        {
            if (profiles == null || profiles.Length == 0)
                throw new InvalidOperationException($"OpenXR profile {typeof(T).Name} is unavailable.");
            foreach (var profile in profiles)
            {
                profile.enabled = true;
                EditorUtility.SetDirty(profile);
            }
        }

        private static void ConfigurePlugin(string path, BuildTarget target, string cpu, bool editorCompatible)
        {
            if (AssetImporter.GetAtPath(path) is not PluginImporter importer) return;
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(editorCompatible);
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, target == BuildTarget.StandaloneWindows64);
            importer.SetCompatibleWithPlatform(BuildTarget.Android, target == BuildTarget.Android);
            importer.SetPlatformData(target, "CPU", cpu);
            importer.SaveAndReimport();
        }

        private static void Build(BuildTarget target, string location, bool vrFirst)
        {
            PrepareForBuild(target, vrFirst);
            PlayerSettings.productName = "PharmaLabVR";
            PlayerSettings.bundleVersion = "0.1.0-research";
            if (target == BuildTarget.Android)
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.pharmalabvr.lab");
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                target = target,
                locationPathName = location,
                options = BuildOptions.StrictMode,
                extraScriptingDefines = vrFirst ? new[] { "PHARMALABVR_VR_FIRST" } : Array.Empty<string>()
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException($"Build failed: {report.summary.result}");
        }

        private static void PrepareForBuild(BuildTarget target, bool vrFirst)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureNativePlugins();
            ConfigureOpenXrLoaders(target != BuildTarget.StandaloneWindows64 || vrFirst, true);
            foreach (var scene in Scenes)
                if (!File.Exists(scene)) throw new FileNotFoundException("Generated build scene is missing.", scene);
        }
    }
}
