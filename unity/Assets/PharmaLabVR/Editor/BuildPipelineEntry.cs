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
using PharmaLabVR.Tools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
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
            var menu = root.AddComponent<BootMenu>();
            var camera = new GameObject("BootCamera");
            camera.AddComponent<Camera>();
            camera.AddComponent<AudioListener>();
            camera.transform.SetParent(root.transform, false);
            var rig = new GameObject("BootXRPlayerRig");
            rig.transform.SetParent(root.transform, false);
            var origin = rig.AddComponent<XROrigin>();
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(rig.transform, false);
            var head = new GameObject("XR Boot Camera");
            head.transform.SetParent(offset.transform, false);
            var xrCamera = head.AddComponent<Camera>();
            var listener = head.AddComponent<AudioListener>();
            xrCamera.enabled = false;
            listener.enabled = false;
            var poseDriver = head.AddComponent<TrackedPoseDriver>();
            poseDriver.positionInput = Action("Boot Head Position", InputActionType.Value,
                "Vector3", "<XRHMD>/devicePosition");
            poseDriver.rotationInput = Action("Boot Head Rotation", InputActionType.Value,
                "Quaternion", "<XRHMD>/deviceRotation");
            poseDriver.trackingStateInput = Action("Boot Head Tracking", InputActionType.Value,
                "Integer", "<XRHMD>/trackingState");
            origin.CameraFloorOffsetObject = offset;
            origin.Camera = xrCamera;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            var interactionManager = new GameObject("BootXRInteractionManager");
            interactionManager.transform.SetParent(root.transform, false);
            var manager = interactionManager.AddComponent<XRInteractionManager>();
            CreateXrController(offset.transform, true, manager);
            CreateXrController(offset.transform, false, manager);

            var world = new GameObject("BootWorldPanel", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            world.transform.SetParent(rig.transform, false);
            var rect = world.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420f, 620f);
            rect.localScale = Vector3.one * 0.001f;
            var canvas = world.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = xrCamera;
            var canvasBackground = world.AddComponent<Image>();
            canvasBackground.color = new Color(0.89f, 0.94f, 0.94f, 0.97f);
            BootText(world.transform, "Boot Title", 240f, 46f, 30);
            BootText(world.transform, "Boot Status", 170f, 94f, 22);
            string[] names = { "Enter VR", "Desktop", "Seated", "Standing",
                "Left hand", "Right hand", "Bench lower", "Bench higher",
                "Text larger", "Text smaller", "Captions", "Reduced motion",
                "High contrast", "Audio mute", "Next", "Previous", "Enter lab", "Back" };
            for (var i = 0; i < names.Length; i++)
                BootButton(world.transform, names[i], 92f - (i % 4) * 58f);
            world.AddComponent<BootSpatialPanelPresenter>().Configure(menu);

            var events = new GameObject("BootXRUIEventSystem", typeof(EventSystem),
                typeof(XRUIInputModule));
            events.transform.SetParent(root.transform, false);
            var module = events.GetComponent<XRUIInputModule>();
            module.enableXRInput = true;
            module.enableMouseInput = false;
            module.enableTouchInput = false;
            module.enableGamepadInput = false;
            module.enableJoystickInput = false;
            events.SetActive(false); // Enabled only with a live tracked XR head.
            EditorSceneManager.SaveScene(scene, Scenes[0]);
        }

        private static Text BootText(Transform parent, string name, float y, float height, int size)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Text));
            child.transform.SetParent(parent, false);
            var rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(366f, height);
            rect.anchoredPosition = new Vector2(0f, y);
            var label = child.GetComponent<Text>();
            label.font = Resources.Load<Font>("Fonts/NotoSans-Regular");
            if (label.font == null) throw new InvalidOperationException("Missing licensed pinned UI font.");
            label.fontSize = size;
            label.color = new Color(0.09f, 0.18f, 0.2f);
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = name;
            return label;
        }

        private static void BootButton(Transform parent, string name, float y)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            child.transform.SetParent(parent, false);
            var rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(366f, 50f);
            rect.anchoredPosition = new Vector2(0f, y);
            child.GetComponent<Image>().color = new Color(0.13f, 0.33f, 0.36f);
            var text = BootText(child.transform, name + " label", 0f, 40f, 22);
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.color = Color.white;
        }

        private static void CreateLabScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lab = new GameObject("LabRoot");
            // The same visual/accessibility preference stream applies to
            // both Player rigs and all Lab panels, not a label-only setting.
            lab.AddComponent<AudioPreferencesOutput>();
            var benchRoot = new GameObject("BenchRoot");
            benchRoot.transform.SetParent(lab.transform);
            var roomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Environment/PremiumLabShell.prefab");
            var benchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Environment/BenchAssembly.prefab");
            if (roomPrefab == null || benchPrefab == null)
                throw new InvalidOperationException("Generate environment prefabs before creating the Lab scene.");
            var room = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab);
            room.transform.SetParent(lab.transform, false);
            var bench = (GameObject)PrefabUtility.InstantiatePrefab(benchPrefab);
            bench.transform.SetParent(benchRoot.transform, false);
            // Independent instrument assets are authored and linked in AllAssets,
            // never substituted with the burette valve. Their measured actions
            // remain explicit SessionController commands under Native authority.
            foreach (var item in new[] {
                ("ResearchPipette", new Vector3(-0.34f, 0.49f, 0.26f)),
                ("ResearchStockBottle", new Vector3(-0.58f, 0.49f, 0.22f))
            })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/PharmaLabVR/Prefabs/Tools/" + item.Item1 + ".prefab");
                if (prefab == null)
                    throw new InvalidOperationException("Missing generated research instrument prefab: " + item.Item1);
                var toolInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                toolInstance.transform.SetParent(lab.transform, false);
                toolInstance.transform.localPosition = item.Item2;
            }
            new GameObject("ToolRegistry").transform.SetParent(lab.transform);
            new GameObject("VesselRegistry").transform.SetParent(lab.transform);
            var coreDriver = new GameObject("CoreDriver").AddComponent<CoreDriver>();
            coreDriver.transform.SetParent(lab.transform);
            foreach (var visual in lab.GetComponentsInChildren<NativeMeteredInstrumentVisual>(true))
                visual.Configure(coreDriver);
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
            interactionManager.transform.SetParent(lab.transform, false);
            var xrInteractionManager = interactionManager.AddComponent<XRInteractionManager>();
            var leftController = CreateXrController(cameraOffset.transform, true, xrInteractionManager);
            var rightController = CreateXrController(cameraOffset.transform, false, xrInteractionManager);
            var xrInput = xrRig.AddComponent<XRInputAdapter>();
            xrRig.SetActive(false);
            var desktopRig = new GameObject("DesktopPlayerRig");
            var desktopInput = desktopRig.AddComponent<DesktopInputAdapter>();
            desktopRig.transform.SetParent(lab.transform);
            var camera = new GameObject("DesktopCamera");
            camera.AddComponent<Camera>();
            camera.AddComponent<AudioListener>();
            var desktopCameraController = camera.AddComponent<DesktopCameraController>();
            camera.transform.SetParent(desktopRig.transform);
            camera.transform.localPosition = new Vector3(0f, 1.25f, -0.8f);
            camera.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            // Bind after parenting AND rotation. Both were absent in Awake.
            desktopCameraController.Configure(desktopInput);
            var holdAnchor = new GameObject("DesktopToolHoldAnchor");
            holdAnchor.transform.SetParent(desktopRig.transform, false);
            holdAnchor.AddComponent<DesktopHoldAnchorFollower>().Configure(
                camera.transform,
                new Vector3(0.25f, -0.18f, 0.65f));
            // Use the same persisted, graduated asset for Desktop and XR.
            // Its meniscus/liquid display follows Native state; no primitive stand-in.
            var burettePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchBurette.prefab");
            if (burettePrefab == null)
                throw new InvalidOperationException("Missing generated ResearchBurette prefab.");
            var researchTool = (GameObject)PrefabUtility.InstantiatePrefab(burettePrefab);
            researchTool.name = "DesktopResearchTool";
            researchTool.transform.SetParent(lab.transform, false);
            researchTool.transform.localPosition = new Vector3(0.25f, 0.90f, 0.2f);
            if (researchTool.transform.Find("OutletExit") == null ||
                researchTool.GetComponent<ToolPresenter>() == null ||
                researchTool.GetComponent<LiquidPresenter>() == null)
                throw new InvalidOperationException("ResearchBurette lacks Native presentation or outlet geometry.");
            researchTool.AddComponent<DesktopGrabbable>();
            var body = researchTool.GetComponent<Rigidbody>();
            var grab = researchTool.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (body == null || grab == null)
                throw new InvalidOperationException("ResearchBurette requires its authored rigidbody and XR grip.");
            body.useGravity = false;
            body.isKinematic = false;
            grab.interactionManager = xrInteractionManager;
            grab.throwOnDetach = false;
            grab.forceGravityOnDetach = false;
            var dock = new GameObject("BuretteDockAnchor");
            dock.transform.SetParent(lab.transform, false);
            dock.transform.position = researchTool.transform.position;
            dock.transform.rotation = researchTool.transform.rotation;
            researchTool.AddComponent<ToolMount>().Configure(dock.transform, body, grab);
            xrRig.AddComponent<RecoveryAlignment>().Configure(xrOrigin, dock.transform);
            var receiver = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            receiver.name = "ReceiverVessel";
            receiver.transform.SetParent(lab.transform);
            receiver.transform.localPosition = new Vector3(0.25f, 0.53f, 0.2f);
            receiver.transform.localScale = new Vector3(0.10f, 0.08f, 0.10f);
            var receiverBody = receiver.AddComponent<Rigidbody>();
            receiverBody.useGravity = false;
            receiverBody.isKinematic = false;
            var receiverGrab = receiver.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            receiverGrab.interactionManager = xrInteractionManager;
            receiverGrab.throwOnDetach = false;
            receiverGrab.forceGravityOnDetach = false;
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

        private static ActionBasedController CreateXrController(Transform parent, bool leftHand,
            XRInteractionManager interactionManager)
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
            direct.AddComponent<XRDirectInteractor>().interactionManager = interactionManager;

            var ray = new GameObject($"{hand} Ray Interactor");
            ray.transform.SetParent(controllerObject.transform, false);
            ray.AddComponent<XRRayInteractor>().interactionManager = interactionManager;
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
            // Tracked scene YAML may lag asset-generation source. Rebuild the
            // complete first-use graph before every Player build.
            AllAssets();
            ConfigureOpenXrLoaders(target != BuildTarget.StandaloneWindows64 || vrFirst, true);
            foreach (var scene in Scenes)
                if (!File.Exists(scene)) throw new FileNotFoundException("Generated build scene is missing.", scene);
        }
    }
}
