using System.Linq;
using NUnit.Framework;
using PharmaLabVR.Tools;
using PharmaLabVR.Input;
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
        public void M2_GeneratedStockBottleExposesSourceImmersionToBothPhysicalInputRoutes()
        {
            var bottle = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchStockBottle.prefab");
            Assert.That(bottle, Is.Not.Null);
            Assert.That(bottle.GetComponent<ResearchStockAspirateTarget>(), Is.Not.Null);
            Assert.That(bottle.transform.Find("ParkedResearchCap"), Is.Not.Null);
            Assert.That(bottle.transform.Find("ClosedResearchCap"), Is.Null,
                "A closed bottle cap must not cover the immersion opening.");
            var scene = EditorSceneManager.OpenScene("Assets/PharmaLabVR/Scenes/Lab.unity",
                OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var desktop = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DesktopInputAdapter>(true)).Single();
                var xr = roots.SelectMany(root =>
                    root.GetComponentsInChildren<XRInputAdapter>(true)).Single();
                Assert.That(desktop.HasStockAspirateTarget, Is.True);
                Assert.That(xr.HasStockAspirateTarget, Is.True);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void M2_ReserveStockHasNativeBoundVisibleImmersionAndPhysicalInputConnections()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchReserveStockBottle.prefab");
            Assert.That(prefab, Is.Not.Null);
            var target = prefab.GetComponent<ResearchStockAspirateTarget>();
            Assert.That(target, Is.Not.Null);
            Assert.That(target.NativeInventoryId, Is.EqualTo("reserve-stock"));
            Assert.That(prefab.GetComponent<NativeMeteredInstrumentVisual>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<BottlePourActuator>(), Is.Null,
                "Reserve is a prepared pipette intake station, not a second Native pouring tool.");
            Assert.That(prefab.GetComponent<XRGrabInteractable>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("ParkedResearchCap"), Is.Not.Null);
            var scene = EditorSceneManager.OpenScene("Assets/PharmaLabVR/Scenes/Lab.unity",
                OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var reserve = roots.SelectMany(root =>
                    root.GetComponentsInChildren<ResearchStockAspirateTarget>(true))
                    .Single(item => item.NativeInventoryId == "reserve-stock");
                Assert.That(reserve.name, Is.EqualTo("ResearchReserveStockBottle"));
                var desktop = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DesktopInputAdapter>(true)).Single();
                var xr = roots.SelectMany(root =>
                    root.GetComponentsInChildren<XRInputAdapter>(true)).Single();
                Assert.That(desktop.HasReserveStockAspirateTarget, Is.True);
                Assert.That(xr.HasReserveStockAspirateTarget, Is.True);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void M2_PhysicalNativeRinseStationHasWaterBoundGeometryAndSceneRoutes()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Vessels/ResearchRinseStation.prefab");
            var pipettePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchPipette.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(pipettePrefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<NativeMeteredInstrumentVisual>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<ResearchRinseWaterTarget>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("Opening"), Is.Not.Null);
            var station = Object.Instantiate(prefab);
            var pipette = Object.Instantiate(pipettePrefab);
            try
            {
                var target = station.GetComponent<ResearchRinseWaterTarget>();
                var tip = pipette.transform.Find("OutletExit");
                pipette.transform.position = station.transform.Find("Opening").position -
                    tip.localPosition + Vector3.down * 0.038f;
                Assert.That(target.IsImmersed(pipette.transform), Is.False,
                    "Unprepared Native water is not an authorized rinse.");
                var visual = station.GetComponent<NativeMeteredInstrumentVisual>();
                visual.ApplySnapshot(
                    "{\"vessels\":[{\"id\":\"rinse-water\",\"inventory\":{\"researchAdditiveVolumeM3\":0.000001}}]}");
                Assert.That(target.IsImmersed(pipette.transform), Is.True,
                    "Aligned pipette must reach real represented Native water.");
                pipette.transform.position += Vector3.right * 0.1f;
                Assert.That(target.IsImmersed(pipette.transform), Is.False,
                    "A tip outside the open station must be refused.");
                visual.ApplySnapshot("{\"vessels\":[]}");
                Assert.That(target.IsImmersed(pipette.transform), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(pipette);
                Object.DestroyImmediate(station);
            }

            var scene = EditorSceneManager.OpenScene("Assets/PharmaLabVR/Scenes/Lab.unity",
                OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.SelectMany(root =>
                    root.GetComponentsInChildren<ResearchRinseWaterTarget>(true)).Count(),
                    Is.EqualTo(1));
                Assert.That(roots.SelectMany(root =>
                    root.GetComponentsInChildren<DesktopInputAdapter>(true)).Single()
                    .HasPhysicalRinseWaterTarget, Is.True);
                Assert.That(roots.SelectMany(root =>
                    root.GetComponentsInChildren<XRInputAdapter>(true)).Single()
                    .HasPhysicalRinseWaterTarget, Is.True);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void M2_PhysicalNativeRinseIntentRejectsCompetingTargetsAndHolds()
        {
            ResearchAuxiliaryDeliveryIntent Resolve(bool water, bool receiver = false,
                bool source = false, bool prepared = true, bool ready = true) =>
                ResearchPhysicalRinseSelection.Resolve("research-pipette-visual",
                    true, true, receiver, source, false, water, prepared, ready);
            Assert.That(Resolve(true), Is.EqualTo(ResearchAuxiliaryDeliveryIntent.PipetteRinse));
            Assert.That(Resolve(false), Is.EqualTo(ResearchAuxiliaryDeliveryIntent.None));
            Assert.That(Resolve(true, receiver: true), Is.EqualTo(ResearchAuxiliaryDeliveryIntent.None));
            Assert.That(Resolve(true, source: true), Is.EqualTo(ResearchAuxiliaryDeliveryIntent.None));
            Assert.That(Resolve(true, prepared: false), Is.EqualTo(ResearchAuxiliaryDeliveryIntent.None));
            Assert.That(Resolve(true, ready: false), Is.EqualTo(ResearchAuxiliaryDeliveryIntent.None));
        }

        [Test]
        public void M2_GeneratedBuretteUpperInletTracksRealGlassBodyMesh()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchBurette.prefab");
            Assert.That(prefab, Is.Not.Null);
            var inlet = prefab.GetComponent<ResearchBuretteInletTarget>();
            var marker = prefab.transform.Find("InletEntry");
            var body = prefab.transform.Find("InspectableGlassBody");
            Assert.That(inlet, Is.Not.Null, "The Native burette needs a distinct physical intake.");
            Assert.That(marker, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            var upper = body.localPosition.y +
                body.GetComponent<MeshFilter>().sharedMesh.bounds.max.y * body.localScale.y;
            Assert.That(marker.localPosition.y, Is.EqualTo(upper).Within(1e-5f));
            Assert.That(prefab.GetComponent<ValveActuator>(), Is.Not.Null);
        }

        [Test]
        public void P03_03_InvalidSidewaysDockReturnPreservesActualUnityBodyState()
        {
            var root = new GameObject("dock-return-body");
            var dock = new GameObject("dock-pose");
            try
            {
                root.transform.position = new Vector3(0.04f, 0f, 0f);
                root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = false;
                var grab = root.AddComponent<XRGrabInteractable>();
                var mount = root.AddComponent<ToolMount>();
                mount.Configure(dock.transform, rigidbody, grab);
                var oldPosition = root.transform.position;
                var oldRotation = root.transform.rotation;
                Assert.That(mount.TryMount(), Is.False,
                    "A horizontal but nearby instrument must not snap through the rack.");
                Assert.That(root.transform.position, Is.EqualTo(oldPosition));
                Assert.That(root.transform.rotation, Is.EqualTo(oldRotation));
                Assert.That(rigidbody.isKinematic, Is.False);
                Assert.That(grab.enabled, Is.True);
                Assert.That(mount.IsMounted, Is.False);
                root.transform.rotation = dock.transform.rotation;
                Assert.That(mount.TryMount(), Is.True);
                Assert.That(mount.IsMounted, Is.True);
                Assert.That(grab.enabled, Is.False);
                Assert.That(rigidbody.isKinematic, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(dock);
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
        public void P10_01_GeneratedDesktopCameraSharesPhysicalToolOwnershipWithInputAdapter()
        {
            var scene = EditorSceneManager.OpenScene("Assets/PharmaLabVR/Scenes/Lab.unity",
                OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var adapter = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DesktopInputAdapter>(true)).Single();
                var camera = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DesktopCameraController>(true)).Single();
                Assert.That(camera.ToolInputAdapter, Is.SameAs(adapter),
                    "The Desktop Camera was created before parenting; its wheel must share the real held tool.");
                Assert.That(camera.PitchDegrees,
                    Is.EqualTo(Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x)).Within(0.01f),
                    "The first right-drag must preserve authored 30 degree camera pitch.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void P03_03_DesktopBuretteOutletMarkerCapturesReceiverAtAuthoredResearchScenePose()
        {
            var scene = EditorSceneManager.OpenScene("Assets/PharmaLabVR/Scenes/Lab.unity",
                OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var objects = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                var tool = objects.Single(item => item.name == "DesktopResearchTool");
                var cylinder = tool.Find("CalibratedBody");
                var outlet = tool.Find("OutletExit");
                Assert.That(cylinder, Is.Not.Null);
                Assert.That(outlet, Is.Not.Null, "Native valve has no physical outlet in generated Lab.");
                var expectedBottomY = ResearchOutletGeometry.BottomLocalY(
                    cylinder.localPosition.y,
                    cylinder.GetComponent<MeshFilter>().sharedMesh.bounds.min.y,
                    cylinder.localScale.y);
                Assert.That(outlet.localPosition.y, Is.EqualTo(expectedBottomY).Within(1e-5),
                    "Burette output origin must follow actual mesh transform.");
                var receiver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<LabCaptureTarget>(true)).Single();
                var hit = receiver.Estimate(tool, Vector3.zero);
                Assert.That(hit.Length, Is.EqualTo(1),
                    "The authored initial 50mL burette tip must geometrically reach its receiver.");
                Assert.That(hit[0].destinationInventoryId, Is.EqualTo("receiver"));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
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
