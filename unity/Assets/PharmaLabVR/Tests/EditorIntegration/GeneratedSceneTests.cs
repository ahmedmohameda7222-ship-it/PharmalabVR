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
        public void M1_GeneratedLabUsesTheActualNativeBoundBurettePrefab()
        {
            var tool = FindInActiveScene("DesktopResearchTool");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchBurette.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(tool), Is.True,
                "Selectable Desktop/XR burette must be the authored apparatus, not a primitive stand-in.");
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(tool), Is.SameAs(prefab));
            Assert.That(tool.GetComponent<ToolPresenter>(), Is.Not.Null);
            Assert.That(tool.GetComponent<LiquidPresenter>(), Is.Not.Null);
            Assert.That(tool.GetComponent<MeniscusPresenter>(), Is.Not.Null);
            Assert.That(tool.GetComponent<DesktopGrabbable>(), Is.Not.Null);
            Assert.That(tool.GetComponent<ToolMount>(), Is.Not.Null);
            Assert.That(tool.GetComponentsInChildren<Transform>(true)
                .Count(item => item.name == "OutletExit"), Is.EqualTo(1));
            Assert.That(tool.GetComponentsInChildren<Transform>(true)
                .Any(item => item.name == "CommittedLiquid"), Is.True);
        }

        [Test]
        public void M2_AuxiliaryInstrumentsAreRealDesktopAndXRGrasps()
        {
            var manager = FindInActiveScene("XRInteractionManager").GetComponent<XRInteractionManager>();
            foreach (var expected in new[] {
                ("ResearchPipette", "research-pipette-visual"),
                ("ResearchStockBottle", "research-stock-bottle-visual")
            })
            {
                var instrument = FindInActiveScene(expected.Item1);
                var desktopGrab = instrument.GetComponent<DesktopGrabbable>();
                Assert.That(desktopGrab, Is.Not.Null);
                Assert.That(desktopGrab.ToolId, Is.EqualTo(expected.Item2));
                var body = instrument.GetComponent<Rigidbody>();
                Assert.That(body, Is.Not.Null);
                Assert.That(body.isKinematic, Is.True);
                var xrGrab = instrument.GetComponent<XRGrabInteractable>();
                Assert.That(xrGrab, Is.Not.Null);
                Assert.That(xrGrab.interactionManager, Is.SameAs(manager));
                Assert.That(xrGrab.throwOnDetach, Is.False);
                Assert.That(instrument.GetComponent<NativeMeteredInstrumentVisual>(), Is.Not.Null);
            }
            var burette = FindInActiveScene("DesktopResearchTool");
            Assert.That(burette.GetComponent<DesktopGrabbable>().ToolId, Is.EqualTo("research-tool"));
            var adapter = FindInActiveScene("DesktopPlayerRig").GetComponent<DesktopInputAdapter>();
            Assert.That(adapter, Is.Not.Null);
            Assert.That(adapter.IsHoldingNativeTool, Is.False);
        }

        [Test]
        public void M2_ActualBuretteStopcockTracksUserInputAndClosesSafely()
        {
            var tool = FindInActiveScene("DesktopResearchTool");
            var handle = tool.transform.Find("StopcockLever");
            var valve = tool.GetComponent<ValveActuator>();
            var presenter = tool.GetComponent<ToolPresenter>();
            Assert.That(handle, Is.Not.Null, "A real handle mesh must ship with the instrument.");
            Assert.That(valve, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            presenter.PresentActuatorInput(0.7f);
            Assert.That(valve.NormalizedOpening, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(Quaternion.Angle(handle.localRotation, Quaternion.identity),
                Is.GreaterThan(10f), "Actual input must visibly move the stopcock.");
            presenter.PresentActuatorInput(0f);
            Assert.That(valve.NormalizedOpening, Is.Zero);
            Assert.That(Quaternion.Angle(handle.localRotation, Quaternion.identity),
                Is.LessThan(0.01f), "Released or held input closes the physical handle.");
        }

        [Test]
        public void M1_ReceiverBeakerIsAPersistentNativeBoundPrefab()
        {
            var receiver = FindInActiveScene("ReceiverVessel");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Vessels/ResearchReceiver.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(receiver), Is.True);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(receiver), Is.SameAs(prefab));
            Assert.That(receiver.GetComponent<LiquidPresenter>(), Is.Not.Null);
            var meniscus = receiver.GetComponent<MeniscusPresenter>();
            Assert.That(meniscus, Is.Not.Null);
            Assert.That(AssetDatabase.Contains(meniscus.Surface.GetComponent<MeshFilter>().sharedMesh),
                Is.True, "Receiver meniscus needs persisted mesh geometry.");
            Assert.That(receiver.GetComponentsInChildren<Transform>(true)
                .Count(item => item.name.StartsWith("AuthoredReceiverWall")), Is.EqualTo(16));
            Assert.That(receiver.GetComponent<LabCaptureTarget>(), Is.Not.Null);
        }

        [Test]
        public void M1_LabSceneContainsDistinctNativeInstrumentViews()
        {
            var pipette = FindInActiveScene("ResearchPipette");
            var bottle = FindInActiveScene("ResearchStockBottle");
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(pipette), Is.True);
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(bottle), Is.True);
            Assert.That(pipette.GetComponent<PipetteActuator>(), Is.Not.Null);
            Assert.That(bottle.GetComponent<BottlePourActuator>(), Is.Not.Null);
            Assert.That(pipette.GetComponent<NativeMeteredInstrumentVisual>(), Is.Not.Null);
            Assert.That(bottle.GetComponent<NativeMeteredInstrumentVisual>(), Is.Not.Null);
            Assert.That(FindInActiveScene("ReceiverVessel"), Is.Not.Null);
            Assert.That(FindInActiveScene("SessionController"), Is.Not.Null);
            Assert.That(FindInActiveScene("CoreDriver"), Is.Not.Null);
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

        [Test]
        public void P07_02_XrDockedBuretteCannotBeGrabbedUntilExplicitRelease()
        {
            var tool = FindInActiveScene("DesktopResearchTool");
            var anchor = FindInActiveScene("BuretteDockAnchor");
            var mount = tool.GetComponent<ToolMount>();
            var grabbable = tool.GetComponent<XRGrabInteractable>();
            var body = tool.GetComponent<Rigidbody>();
            Assert.That(anchor, Is.Not.Null);
            Assert.That(mount, Is.Not.Null);
            Assert.That(mount.DockAnchor, Is.EqualTo(anchor.transform));
            Assert.That(mount.TryMount(), Is.True);
            Assert.That(mount.IsMounted, Is.True);
            Assert.That(grabbable.enabled, Is.False, "A docked burette cannot be displaced by either hand.");
            Assert.That(body.isKinematic, Is.True);
            Assert.That(tool.transform.position, Is.EqualTo(anchor.transform.position));
            Assert.That(mount.TryMount(), Is.False, "Duplicate mount must not reinitialize ownership.");
            Assert.That(mount.ReleaseFromDock(), Is.True);
            Assert.That(mount.IsMounted, Is.False);
            Assert.That(grabbable.enabled, Is.True);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(mount.ReleaseFromDock(), Is.False);
        }

        [Test]
        public void P07_03_RecenterIsHorizontalAndNeverScalesCalibratedTool()
        {
            var rig = FindInActiveScene("XRPlayerRig");
            var tool = FindInActiveScene("DesktopResearchTool");
            var dock = FindInActiveScene("BuretteDockAnchor");
            var alignment = rig.GetComponent<RecoveryAlignment>();
            Assert.That(alignment, Is.Not.Null,
                "Lab scene requires explicit recovery alignment on the XR rig.");
            Assert.That(tool, Is.Not.Null);
            Assert.That(dock, Is.Not.Null);
            var originalRigActive = rig.activeSelf;
            rig.SetActive(true);
            try
            {
                var originalToolScale = tool.transform.localScale;
                var originalToolPosition = tool.transform.position;
                var originalRigY = rig.transform.position.y;
                var head = rig.GetComponent<XROrigin>().Camera.transform;
                var originalHorizontalDistance = Vector3.Distance(
                    Vector3.ProjectOnPlane(head.position, Vector3.up),
                    Vector3.ProjectOnPlane(dock.transform.position, Vector3.up));
                Assert.That(alignment.TryRecenter(), Is.True);
                Assert.That(rig.transform.position.y, Is.EqualTo(originalRigY).Within(1e-5f),
                    "Recenter cannot fake a height calibration.");
                Assert.That(tool.transform.localScale, Is.EqualTo(originalToolScale),
                    "Instrument dimensions and native volume calibration are immutable.");
                Assert.That(tool.transform.position, Is.EqualTo(originalToolPosition),
                    "View alignment must never move native-registered instruments.");
                var horizontal = Vector3.Distance(
                    Vector3.ProjectOnPlane(head.position, Vector3.up),
                    Vector3.ProjectOnPlane(dock.transform.position, Vector3.up));
                Assert.That(horizontal, Is.EqualTo(0.78f).Within(0.02f));
                Assert.That(originalHorizontalDistance, Is.Not.EqualTo(horizontal).Within(1e-4f));
            }
            finally { rig.SetActive(originalRigActive); }
        }

        [Test]
        public void P07_03_SnapTurnRotatesThirtyDegreesAboutTrackedHeadWithoutMovingTool()
        {
            var rig = FindInActiveScene("XRPlayerRig");
            var tool = FindInActiveScene("DesktopResearchTool");
            var alignment = rig.GetComponent<RecoveryAlignment>();
            var originalActive = rig.activeSelf;
            rig.SetActive(true);
            try
            {
                var camera = rig.GetComponent<XROrigin>().Camera.transform;
                var originalEye = camera.position;
                var originalToolPosition = tool.transform.position;
                var originalToolScale = tool.transform.localScale;
                var initialYaw = rig.transform.eulerAngles.y;
                Assert.That(alignment.TrySnapTurn(1), Is.True);
                Assert.That(Mathf.DeltaAngle(initialYaw, rig.transform.eulerAngles.y),
                    Is.EqualTo(30f).Within(0.01f));
                Assert.That(Vector3.Distance(camera.position, originalEye),
                    Is.LessThan(1e-4f), "A snap turn pivots around the head, never teleports it.");
                Assert.That(tool.transform.position, Is.EqualTo(originalToolPosition));
                Assert.That(tool.transform.localScale, Is.EqualTo(originalToolScale));
                Assert.That(alignment.TrySnapTurn(-1), Is.True);
                Assert.That(Mathf.DeltaAngle(initialYaw, rig.transform.eulerAngles.y),
                    Is.EqualTo(0f).Within(0.01f));
                Assert.That(alignment.TrySnapTurn(0), Is.False);
            }
            finally { rig.SetActive(originalActive); }
        }

        [Test]
        public void P07_03_ReturnToolToDockRequiresPhysicalProximityAndFreeGrab()
        {
            var tool = FindInActiveScene("DesktopResearchTool");
            var dock = tool.GetComponent<ToolMount>();
            var rigidbody = tool.GetComponent<Rigidbody>();
            var grab = tool.GetComponent<XRGrabInteractable>();
            Assert.That(dock, Is.Not.Null);
            Assert.That(dock.IsMounted, Is.False);
            var original = tool.transform.position;
            var oldKinematic = rigidbody.isKinematic;
            try
            {
                tool.transform.position = original + Vector3.right * 0.6f;
                Assert.That(dock.TryMount(), Is.False,
                    "A free tool must never teleport across the room into the dock.");
                Assert.That(tool.transform.position.x, Is.EqualTo(original.x + 0.6f).Within(1e-5f));
                Assert.That(rigidbody.isKinematic, Is.EqualTo(oldKinematic));
                Assert.That(grab.enabled, Is.True);
                tool.transform.position = original + Vector3.right * 0.06f;
                Assert.That(dock.TryMount(), Is.True);
                Assert.That(dock.IsMounted, Is.True);
                Assert.That(tool.transform.position, Is.EqualTo(dock.DockAnchor.position));
                Assert.That(grab.enabled, Is.False);
                Assert.That(dock.ReleaseFromDock(), Is.True);
            }
            finally { tool.transform.position = original; }
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
