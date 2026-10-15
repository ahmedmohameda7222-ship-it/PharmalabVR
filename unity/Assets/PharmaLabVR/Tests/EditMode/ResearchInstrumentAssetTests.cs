using System.Linq;
using NUnit.Framework;
using PharmaLabVR.Tools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PharmaLabVR.Tests
{
    // Requires BuildPipelineEntry.AllAssets before the deferred Editor campaign.
    // These tests are authored only: NOT_RUN at the source SHA.
    public sealed class ResearchInstrumentAssetTests
    {
        [Test]
        public void P03_02_ResearchBottleAndPipetteAreDistinctPersistentAssets()
        {
            var pipette = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchPipette.prefab");
            var bottle = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchStockBottle.prefab");
            Assert.That(pipette, Is.Not.Null, "AllAssets must generate a separate pipette.");
            Assert.That(bottle, Is.Not.Null, "AllAssets must generate a separate stock bottle.");
            Assert.That(pipette.GetComponent<PipetteActuator>(), Is.Not.Null);
            Assert.That(bottle.GetComponent<BottlePourActuator>(), Is.Not.Null);
            Assert.That(pipette.GetComponent<ValveActuator>(), Is.Null,
                "Research pipette must not masquerade as burette valve.");
            Assert.That(pipette.GetComponent<NativeMeteredInstrumentVisual>(), Is.Not.Null);
            Assert.That(bottle.GetComponent<NativeMeteredInstrumentVisual>(), Is.Not.Null);
            Assert.That(pipette.transform.Find("NativePipetteLiquid"), Is.Not.Null);
            Assert.That(bottle.transform.Find("NativeBottleLiquid"), Is.Not.Null);
            Assert.That(bottle.GetComponent<ValveActuator>(), Is.Null,
                "Stock bottle measured pour cannot be actuated with the burette valve.");
            Assert.That(pipette.transform.Find("GraduatedGlassTube"), Is.Not.Null);
            Assert.That(pipette.transform.Find("MeteredOutletTip"), Is.Not.Null);
            Assert.That(bottle.transform.Find("StockBottleBody"), Is.Not.Null);
            Assert.That(bottle.transform.Find("100mL_Research_NotCalibrated"), Is.Not.Null);
            Assert.That(pipette.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThan(20));
            Assert.That(bottle.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void P07_02_ResearchToolPrefabsAllowPhysicalGripWithoutBecomingBuretteValves()
        {
            var paths = new[]
            {
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchPipette.prefab",
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchStockBottle.prefab"
            };
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null);
                var body = prefab.GetComponent<Rigidbody>();
                Assert.That(body, Is.Not.Null, path);
                Assert.That(body.isKinematic, Is.True,
                    "Parked research tool must not fall before explicit grip.");
                Assert.That(prefab.GetComponent<XRGrabInteractable>(), Is.Not.Null);
                Assert.That(prefab.GetComponentsInChildren<Collider>().Any(c => c.enabled), Is.True,
                    "Physical XR grip requires an actual pickable collider.");
                Assert.That(prefab.GetComponent<ValveActuator>(), Is.Null,
                    "Grip may not silently grant burette hydraulic actuation.");
            }
        }

        [Test]
        public void P03_03_ResearchOutletMarkersMatchActualPipetteTipAndBottleNeck()
        {
            var pipette = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchPipette.prefab");
            var bottle = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchStockBottle.prefab");
            Assert.That(pipette, Is.Not.Null);
            Assert.That(bottle, Is.Not.Null);
            var tip = pipette.transform.Find("MeteredOutletTip");
            var pipetteOutlet = pipette.transform.Find("OutletExit");
            var bottleNeck = bottle.transform.Find("NarrowNeck");
            var bottleOutlet = bottle.transform.Find("OutletExit");
            Assert.That(pipetteOutlet, Is.Not.Null, "Pipette must expose a metered outlet.");
            Assert.That(bottleOutlet, Is.Not.Null, "Research stock bottle needs its own neck outlet.");
            var pipetteBottom = tip.localPosition.y +
                tip.GetComponent<MeshFilter>().sharedMesh.bounds.min.y * tip.localScale.y;
            var bottleTop = bottleNeck.localPosition.y +
                bottleNeck.GetComponent<MeshFilter>().sharedMesh.bounds.max.y * bottleNeck.localScale.y;
            Assert.That(pipetteOutlet.localPosition.y, Is.EqualTo(pipetteBottom).Within(1e-5));
            Assert.That(bottleOutlet.localPosition.y, Is.EqualTo(bottleTop).Within(1e-5));
            Assert.That(Vector3.Dot(pipetteOutlet.TransformDirection(Vector3.down), Vector3.down),
                Is.GreaterThan(0.99f));
            Assert.That(Vector3.Dot(bottleOutlet.TransformDirection(Vector3.down), Vector3.down),
                Is.LessThan(-0.99f), "Upright closed stock bottle cannot masquerade as a downward spout.");
        }

        [Test]
        public void P03_02_GeneratedLabPlacesOneOfEachResearchInstrumentWithoutReplacingBurette()
        {
            var scene = EditorSceneManager.OpenScene("Assets/PharmaLabVR/Scenes/Lab.unity",
                OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var pipettes = roots.SelectMany(root => root.GetComponentsInChildren<PipetteActuator>(true)).ToArray();
                var bottles = roots.SelectMany(root => root.GetComponentsInChildren<BottlePourActuator>(true)).ToArray();
                Assert.That(pipettes.Length, Is.EqualTo(1));
                Assert.That(bottles.Length, Is.EqualTo(1));
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Any(item => item.name == "DesktopResearchTool"), Is.True,
                    "Existing 50 mL burette must retain its own scene route.");
                Assert.That(pipettes[0].transform.position, Is.Not.EqualTo(bottles[0].transform.position));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
