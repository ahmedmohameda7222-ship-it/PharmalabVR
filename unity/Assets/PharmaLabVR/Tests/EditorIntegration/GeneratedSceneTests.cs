using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PharmaLabVR.Editor;
using PharmaLabVR.Input;
using PharmaLabVR.Tools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEditor.XR.Management;
using Unity.XR.CoreUtils;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using PharmaLabVR.UI;

namespace PharmaLabVR.Tests.EditorIntegration
{
    public sealed class GeneratedSceneTests
    {
        [SetUp]
        public void OpenGeneratedLab()
        {
            EditorSceneManager.OpenScene("Assets/PharmaLabVR/Scenes/Lab.unity");
        }

        [TearDown]
        public void CloseGeneratedScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void B01_GeneratedLabContainsConfiguredXriOriginControllersAndInteractors()
        {
            var xrRig = FindInActiveScene("XRPlayerRig");
            Assert.That(xrRig, Is.Not.Null);
            Assert.That(xrRig.GetComponent<XROrigin>(), Is.Not.Null);
            var manager = FindInActiveScene("XRInteractionManager").GetComponent<XRInteractionManager>();
            Assert.That(manager, Is.Not.Null);
            Assert.That(manager.transform.IsChildOf(xrRig.transform), Is.False,
                "The shared manager must be active before the tool registers.");
            Assert.That(xrRig.GetComponentsInChildren<ActionBasedController>(true).Length, Is.EqualTo(2));
            Assert.That(xrRig.GetComponentsInChildren<XRDirectInteractor>(true).Length, Is.EqualTo(2));
            Assert.That(xrRig.GetComponentsInChildren<XRRayInteractor>(true).Length, Is.EqualTo(2));
            Assert.That(xrRig.GetComponentsInChildren<XRDirectInteractor>(true)
                .All(interactor => interactor.interactionManager == manager), Is.True);
            Assert.That(xrRig.GetComponentsInChildren<XRRayInteractor>(true)
                .All(interactor => interactor.interactionManager == manager), Is.True);
            Assert.That(FindInActiveScene("DesktopResearchTool").GetComponent<XRGrabInteractable>()
                .interactionManager, Is.SameAs(manager));

            var adapter = xrRig.GetComponent<XRInputAdapter>();
            Assert.That(adapter, Is.Not.Null);
            adapter.SetEnabled(true);
            var samples = adapter.SampleInputs(1);
            Assert.That(samples.Length, Is.EqualTo(1));
            Assert.That(samples.All(sample => sample.geometryProfileHash != "unassigned"), Is.True);
            Assert.That(samples.All(sample => sample.coordinateFrame == "lab"), Is.True);
            Assert.That(samples.All(sample => sample.toolId == "research-tool"), Is.True);
        }

        [Test]
        public void B03_DesktopHoldAnchorUsesWorldPoseInsteadOfCameraParenting()
        {
            var camera = FindInActiveScene("DesktopCamera");
            var anchor = FindInActiveScene("DesktopToolHoldAnchor");
            Assert.That(camera, Is.Not.Null);
            Assert.That(anchor, Is.Not.Null);
            Assert.That(anchor.transform.IsChildOf(camera.transform), Is.False);
            Assert.That(anchor.GetComponent("DesktopHoldAnchorFollower"), Is.Not.Null);

            var desktop = FindInActiveScene("DesktopPlayerRig").GetComponent<DesktopInputAdapter>();
            var samples = desktop.SampleInputs(1);
            Assert.That(samples, Has.Length.EqualTo(1));
            Assert.That(samples[0].toolId, Is.EqualTo("research-tool"));
        }

        [Test]
        public void D03_DesktopHoldPreservesToolLabWorldPose()
        {
            var desktop = FindInActiveScene("DesktopPlayerRig").GetComponent<DesktopInputAdapter>();
            var tool = FindInActiveScene("DesktopResearchTool").transform;
            var originalParent = tool.parent;
            var originalPosition = tool.position;
            var originalRotation = tool.rotation;
            var method = typeof(DesktopInputAdapter).GetMethod("BeginHold", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);

            method.Invoke(desktop, new object[] { tool });

            Assert.That(tool.parent, Is.SameAs(originalParent));
            Assert.That(tool.position, Is.EqualTo(originalPosition));
            Assert.That(tool.rotation, Is.EqualTo(originalRotation));
        }

        [Test]
        public void D03_PointerOverUiBlocksDesktopValveInput()
        {
            var method = typeof(DesktopInputAdapter).GetMethod("ShouldAdjustActuator", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That((bool)method.Invoke(null, new object[] { true, true }), Is.False);
            Assert.That((bool)method.Invoke(null, new object[] { false, true }), Is.True);
        }

        [Test]
        public void D04_FocusLossRequiresExplicitDesktopContinue()
        {
            var desktop = FindInActiveScene("DesktopPlayerRig").GetComponent<DesktopInputAdapter>();
            var focus = typeof(DesktopInputAdapter).GetMethod("OnApplicationFocus", BindingFlags.NonPublic | BindingFlags.Instance);
            var awaiting = typeof(DesktopInputAdapter).GetProperty("AwaitingFocusContinue", BindingFlags.Public | BindingFlags.Instance);
            var resume = typeof(DesktopInputAdapter).GetMethod("ContinueAfterFocusRecovery", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(focus, Is.Not.Null);
            Assert.That(awaiting, Is.Not.Null);
            Assert.That(resume, Is.Not.Null);

            desktop.SetEnabled(true);
            focus.Invoke(desktop, new object[] { false });
            focus.Invoke(desktop, new object[] { true });
            Assert.That((bool)awaiting.GetValue(desktop), Is.True);
            Assert.That((bool)resume.Invoke(desktop, null), Is.True);
            Assert.That((bool)awaiting.GetValue(desktop), Is.False);
        }

        [Test]
        public void B01_ResearchToolPrefabIsAnXriGrabInteractable()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PharmaLabVR/Prefabs/Tools/ResearchBurette.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<XRGrabInteractable>(), Is.Not.Null);
        }

        [Test]
        public void B01_OpenXrLoaderIsAssignedForStandaloneAndAndroid()
        {
            BuildPipelineEntry.ConfigureOpenXrLoaders();

            Assert.That(
                EditorBuildSettings.TryGetConfigObject<XRGeneralSettingsPerBuildTarget>(
                    XRGeneralSettings.k_SettingsKey,
                    out var perTarget),
                Is.True);
            Assert.That(perTarget, Is.Not.Null);

            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                var settings = perTarget.SettingsForBuildTarget(group);
                Assert.That(settings, Is.Not.Null, $"Missing XR settings for {group}.");
                Assert.That(settings.InitManagerOnStart, Is.True);
                Assert.That(settings.Manager, Is.Not.Null, $"Missing XR manager for {group}.");
                Assert.That(settings.Manager.activeLoaders.Any(loader => loader is OpenXRLoader), Is.True,
                    $"OpenXR loader is not active for {group}.");
            }
        }

        [Test]
        public void B02_OpenXrControllerProfilesAreEnabledForStandaloneAndAndroid()
        {
            BuildPipelineEntry.ConfigureOpenXrLoaders();

            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                Assert.That(settings, Is.Not.Null);
                Assert.That(settings.GetFeatures<OculusTouchControllerProfile>().Any(feature => feature.enabled), Is.True,
                    $"Oculus Touch profile is disabled for {group}.");
                Assert.That(settings.GetFeatures<KHRSimpleControllerProfile>().Any(feature => feature.enabled), Is.True,
                    $"KHR simple-controller profile is disabled for {group}.");
            }
        }

        [Test]
        public void X01_XrActuatorReadsTriggerAndNotGrip()
        {
            var method = typeof(XRInputAdapter).GetMethod("ChooseActuatorValue", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "XR adapter must distinguish grip selection from trigger actuation.");
            Assert.That((float)method.Invoke(null, new object[] { 0.8f, 0.25f }), Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        public void B03_DesktopBuildRemovesStandaloneXrLoader()
        {
            try
            {
                BuildPipelineEntry.ConfigureOpenXrLoaders(false, true);
                Assert.That(EditorBuildSettings.TryGetConfigObject<XRGeneralSettingsPerBuildTarget>(
                    XRGeneralSettings.k_SettingsKey, out var perTarget), Is.True);

                var standalone = perTarget.SettingsForBuildTarget(BuildTargetGroup.Standalone);
                Assert.That(standalone.InitManagerOnStart, Is.False);
                Assert.That(standalone.Manager.activeLoaders.Any(loader => loader is OpenXRLoader), Is.False);

                var android = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
                Assert.That(android.InitManagerOnStart, Is.True);
            }
            finally
            {
                BuildPipelineEntry.ConfigureOpenXrLoaders();
            }
        }

        [Test]
        public void P08_01_LabHasSerializedEnvironmentAndBenchPrefabsWithTaskLighting()
        {
            var environment = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Environment/PremiumLabShell.prefab");
            var benchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Environment/BenchAssembly.prefab");
            Assert.That(environment, Is.Not.Null, "Generate real reusable environment assets.");
            Assert.That(benchPrefab, Is.Not.Null, "The bench must not be a runtime decoration.");
            var shell = FindInActiveScene("PremiumLabShell");
            var bench = FindInActiveScene("BenchAssembly");
            Assert.That(shell, Is.Not.Null);
            Assert.That(bench, Is.Not.Null);
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(shell), Is.True);
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(bench), Is.True);
            Assert.That(bench.transform.IsChildOf(FindInActiveScene("BenchRoot").transform), Is.True);
            Assert.That(shell.GetComponentsInChildren<Light>(true).Length,
                Is.GreaterThanOrEqualTo(2), "Bench and room require visible task lights.");
            Assert.That(shell.GetComponentsInChildren<MeshRenderer>(true).Length,
                Is.GreaterThanOrEqualTo(6));
            Assert.That(bench.GetComponentsInChildren<MeshRenderer>(true).Length,
                Is.GreaterThanOrEqualTo(4));
            var camera = FindInActiveScene("DesktopCamera").GetComponent<Camera>();
            Assert.That(camera.WorldToViewportPoint(
                bench.transform.TransformPoint(new Vector3(0f, 0.48f, 0.2f))).z,
                Is.GreaterThan(0f), "Bench must remain in front of Desktop camera.");
        }


        [Test]
        public void P08_02_BuretteHasDescendingRealGraduationsAndVolumeBoundMeniscus()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchBurette.prefab");
            Assert.That(prefab, Is.Not.Null);
            var labels = prefab.GetComponentsInChildren<TextMesh>(true);
            var zero = labels.Single(mark => mark.text == "0");
            var fifty = labels.Single(mark => mark.text == "50");
            Assert.That(zero.transform.position.y, Is.GreaterThan(fifty.transform.position.y),
                "50 ml graduation is nearer the bottom of a burette.");
            var majors = labels.Count(mark => int.TryParse(mark.text, out var value) &&
                value >= 0 && value <= 50 && value % 10 == 0);
            Assert.That(majors, Is.EqualTo(6));
            var tick = prefab.GetComponentsInChildren<MeshRenderer>(true)
                .Single(mark => mark.name == "Graduation 10");
            Assert.That(tick.sharedMaterial, Is.Not.Null);
            Assert.That(AssetDatabase.Contains(tick.sharedMaterial), Is.True,
                "The burette graduation ink must be a real reusable prefab material.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var liquid = instance.GetComponent<LiquidPresenter>();
                var meniscus = instance.GetComponent<MeniscusPresenter>();
                Assert.That(meniscus, Is.Not.Null);
                liquid.SetCommittedVolume(0.000025);
                Assert.That(meniscus.Fill01, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(meniscus.Surface.gameObject.activeSelf, Is.True);
                var filter = meniscus.Surface.GetComponent<MeshFilter>();
                Assert.That(filter.sharedMesh, Is.Not.Null);
                Assert.That(AssetDatabase.Contains(filter.sharedMesh), Is.True,
                    "Prefab surface geometry must be a durable asset, not an unsaved Editor mesh.");
                Assert.That(filter.sharedMesh.normals[0].y, Is.GreaterThan(0.5f),
                    "Concave meniscus must face the reader from above.");
                Assert.That(AssetDatabase.Contains(meniscus.Surface.GetComponent<MeshRenderer>().sharedMaterial),
                    Is.True, "The meniscus material must survive a Player build.");
                liquid.SetCommittedVolume(0.0);
                Assert.That(meniscus.Surface.gameObject.activeSelf, Is.False,
                    "Empty native state must hide the meniscus, not preserve an animation.");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void P08_02_ReconfiguredNativeCapacityKeepsMeniscusAndLiquidInSync()
        {
            var root = new GameObject("NativeVolumeVisualProbe");
            try
            {
                var liquidGeometry = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                liquidGeometry.transform.SetParent(root.transform, false);
                var meniscus = root.AddComponent<MeniscusPresenter>();
                meniscus.Configure(root.transform, 1.0, 0.48f, 0f, 0.006f);
                var liquid = root.AddComponent<LiquidPresenter>();
                liquid.Configure(liquidGeometry.transform, 0.00005, 0.48f, 0f);
                liquid.SetCommittedVolume(0.000025);
                Assert.That(liquid.Fill01, Is.EqualTo(0.5f).Within(0.000001f));
                Assert.That(meniscus.Fill01, Is.EqualTo(0.5f).Within(0.000001f),
                    "The meniscus must use the live Native vessel capacity, not its bootstrap placeholder.");
                Assert.That(meniscus.Surface.localPosition.y, Is.EqualTo(0.24f).Within(0.00001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void P07_01_BootSceneHasControllerRayDrivenWorldSpaceSetupWithOneEventSystem()
        {
            EditorSceneManager.OpenScene("Assets/PharmaLabVR/Scenes/Boot.unity");
            var rig = FindInActiveScene("BootXRPlayerRig");
            Assert.That(rig, Is.Not.Null, "Boot must support headset cold start, not only IMGUI.");
            var origin = rig.GetComponent<XROrigin>();
            Assert.That(origin, Is.Not.Null);
            Assert.That(origin.Camera, Is.Not.Null);
            Assert.That(rig.GetComponentsInChildren<XRRayInteractor>(true).Length, Is.EqualTo(2));
            var world = FindInActiveScene("BootWorldPanel");
            Assert.That(world, Is.Not.Null);
            Assert.That(world.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(world.GetComponent<TrackedDeviceGraphicRaycaster>(), Is.Not.Null);
            Assert.That(world.GetComponent<BootSpatialPanelPresenter>(), Is.Not.Null);
            foreach (var label in new[] { "Enter VR", "Desktop", "Seated", "Standing",
                "Left hand", "Right hand", "Enter lab", "Back" })
                Assert.That(world.GetComponentsInChildren<Button>(true)
                    .Any(button => button.name == label), Is.True, "Missing real VR button " + label);
            var systems = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).ToArray();
            Assert.That(systems, Has.Length.EqualTo(1), "Two active EventSystems double-dispatch XR UI.");
            Assert.That(systems[0].GetComponent<XRUIInputModule>(), Is.Not.Null);
        }

        [Test]
        public void P07_02_IndependentReceiverGrabAndNonThrowableResearchTool()
        {
            var manager = FindInActiveScene("XRInteractionManager").GetComponent<XRInteractionManager>();
            var tool = FindInActiveScene("DesktopResearchTool");
            var receiver = FindInActiveScene("ReceiverVessel");
            Assert.That(tool, Is.Not.Null);
            Assert.That(receiver, Is.Not.Null);
            var toolGrab = tool.GetComponent<XRGrabInteractable>();
            var receiverGrab = receiver.GetComponent<XRGrabInteractable>();
            Assert.That(toolGrab, Is.Not.Null);
            Assert.That(receiverGrab, Is.Not.Null,
                "A second hand must be able to own and position the receiver independently.");
            Assert.That(receiver.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(toolGrab, Is.Not.SameAs(receiverGrab));
            Assert.That(toolGrab.interactionManager, Is.SameAs(manager));
            Assert.That(receiverGrab.interactionManager, Is.SameAs(manager));
            Assert.That(toolGrab.throwOnDetach, Is.False,
                "No liquid-tool throws before validated disposal/recovery.");
            Assert.That(receiverGrab.throwOnDetach, Is.False);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchBurette.prefab");
            Assert.That(prefab.GetComponent<XRGrabInteractable>().throwOnDetach, Is.False);
        }

        [Test]
        public void P06_02_BootAndLabKeepOneActiveAudioListenerPerMode()
        {
            EditorSceneManager.OpenScene("Assets/PharmaLabVR/Scenes/Boot.unity");
            var bootDesktop = FindInActiveScene("BootCamera").GetComponent<AudioListener>();
            var bootHead = FindInActiveScene("XR Boot Camera").GetComponent<AudioListener>();
            Assert.That(bootDesktop, Is.Not.Null);
            Assert.That(bootHead, Is.Not.Null);
            Assert.That(bootDesktop.isActiveAndEnabled, Is.True,
                "The Desktop Boot cannot be silent while XR is unavailable.");
            Assert.That(bootHead.isActiveAndEnabled, Is.False,
                "XR audio cannot compete with Desktop audio before tracking.");

            EditorSceneManager.OpenScene("Assets/PharmaLabVR/Scenes/Lab.unity");
            var desktop = FindInActiveScene("DesktopCamera").GetComponent<AudioListener>();
            var xr = FindInActiveScene("XR Camera").GetComponent<AudioListener>();
            Assert.That(desktop, Is.Not.Null);
            Assert.That(xr, Is.Not.Null);
            Assert.That(desktop.isActiveAndEnabled, Is.True);
            Assert.That(xr.isActiveAndEnabled, Is.False);
        }

        private static GameObject FindInActiveScene(string name)
        {
            return SceneManager.GetActiveScene()
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(candidate => candidate.name == name)
                ?.gameObject;
        }
    }
}
