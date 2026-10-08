using System.Linq;
using NUnit.Framework;
using PharmaLabVR.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
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
        }

        [Test]
        public void B01_ResearchToolPrefabIsAnXriGrabInteractable()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PharmaLabVR/Prefabs/Tools/ResearchBurette.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<XRGrabInteractable>(), Is.Not.Null);
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
