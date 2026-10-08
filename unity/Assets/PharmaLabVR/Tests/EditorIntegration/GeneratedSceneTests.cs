using System.Linq;
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
