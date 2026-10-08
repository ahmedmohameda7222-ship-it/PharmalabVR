using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PharmaLabVR.Editor;
using PharmaLabVR.Input;
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
            Assert.That(xrRig.GetComponentInChildren<XRInteractionManager>(true), Is.Not.Null);
            Assert.That(xrRig.GetComponentsInChildren<ActionBasedController>(true).Length, Is.EqualTo(2));
            Assert.That(xrRig.GetComponentsInChildren<XRDirectInteractor>(true).Length, Is.EqualTo(2));
            Assert.That(xrRig.GetComponentsInChildren<XRRayInteractor>(true).Length, Is.EqualTo(2));

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
