using NUnit.Framework;
using PharmaLabVR.Input;
using UnityEngine;

namespace PharmaLabVR.Tests.EditMode
{
    // Authored for the final Unity campaign; the same actual production pure
    // policy is compiled/executed by the headless .NET contract suite now.
    public sealed class DesktopControlPolicyTests
    {
        [Test]
        public void P10_01_PlacingToolMustHitVisibleBenchWithoutTeleportingThroughReceiver()
        {
            Assert.That(DesktopPlacementAdmission.CanPlace(0.06, 1d, true, false), Is.True);
            Assert.That(DesktopPlacementAdmission.CanPlace(0.32, 1d, true, false), Is.False);
            Assert.That(DesktopPlacementAdmission.CanPlace(0.06, 1d, false, false), Is.False);
            Assert.That(DesktopPlacementAdmission.CanPlace(0.06, 0.2d, true, false), Is.False);
            Assert.That(DesktopPlacementAdmission.CanPlace(0.06, 1d, true, true), Is.False);
        }

        [Test]
        public void P10_01_DragOnWorldPlaneHasExclusiveInputNotValveOwnership()
        {
            Assert.That(DesktopPointerRouting.Resolve(true, true, true, false, true),
                Is.EqualTo(DesktopPointerAction.ToolDrag));
            Assert.That(DesktopPointerRouting.Resolve(true, false, true, false, true),
                Is.EqualTo(DesktopPointerAction.Valve));
            Assert.That(DesktopPointerRouting.Resolve(false, true, true, false, true),
                Is.EqualTo(DesktopPointerAction.None));
            Assert.That(DesktopPointerRouting.Resolve(true, true, true, true, true),
                Is.EqualTo(DesktopPointerAction.None));
        }

        [Test]
        public void M2_DesktopReceiverClickInspectsButFAlonePicks()
        {
            Assert.That(DesktopReceiverRouting.Resolve(true, true, false),
                Is.EqualTo(DesktopReceiverAction.Inspect));
            Assert.That(DesktopReceiverRouting.Resolve(true, false, true),
                Is.EqualTo(DesktopReceiverAction.Pick));
            Assert.That(DesktopReceiverRouting.Resolve(false, false, true),
                Is.EqualTo(DesktopReceiverAction.None));
            Assert.That(DesktopReceiverRouting.Resolve(true, true, true),
                Is.EqualTo(DesktopReceiverAction.None));
            Assert.That(DesktopReceiverRouting.Resolve(true, false, false),
                Is.EqualTo(DesktopReceiverAction.None));
        }

        [Test]
        public void M2_ReceiverFootNotCenterMustClearPhysicalBenchAtFPlace()
        {
            Assert.That(DesktopPlacementAdmission.CanPlaceReceiver(
                0.045, 0.04, 1.0, true, false), Is.True);
            Assert.That(DesktopPlacementAdmission.CanPlaceReceiver(
                0.035, 0.04, 1.0, true, false), Is.False);
            Assert.That(DesktopPlacementAdmission.CanPlaceReceiver(
                0.35, 0.04, 1.0, true, false), Is.False);
            Assert.That(DesktopPlacementAdmission.CanPlaceReceiver(
                0.045, 0.04, 0.1, true, false), Is.False);
            Assert.That(DesktopPlacementAdmission.CanPlaceReceiver(
                0.045, double.NaN, 1.0, true, false), Is.False);
        }

        [Test]
        public void M2_PhysicalStockBottleMouthRinseRejectsDoubleDestinationAndDryWater()
        {
            ResearchAuxiliaryDeliveryIntent Resolve(bool dipped, bool receiver = false,
                bool inlet = false, bool prepared = true, bool ready = true,
                bool held = true) =>
                ResearchPhysicalBottleRinseSelection.Resolve(
                    "research-stock-bottle-visual", held, true, receiver, inlet,
                    dipped, prepared, ready);
            Assert.That(Resolve(true), Is.EqualTo(ResearchAuxiliaryDeliveryIntent.BottleRinse));
            Assert.That(Resolve(false), Is.EqualTo(ResearchAuxiliaryDeliveryIntent.None));
            Assert.That(Resolve(true, receiver: true), Is.EqualTo(ResearchAuxiliaryDeliveryIntent.None));
            Assert.That(Resolve(true, inlet: true), Is.EqualTo(ResearchAuxiliaryDeliveryIntent.None));
            Assert.That(Resolve(true, prepared: false), Is.EqualTo(ResearchAuxiliaryDeliveryIntent.None));
            Assert.That(Resolve(true, ready: false), Is.EqualTo(ResearchAuxiliaryDeliveryIntent.None));
            Assert.That(Resolve(true, held: false), Is.EqualTo(ResearchAuxiliaryDeliveryIntent.None));
        }

        [Test]
        public void M2_PipettePhysicalSourceSelectionRequiresRealImmersionAndFreshOwnership()
        {
            Assert.That(ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "source", true, true, true, true, false), Is.True);
            Assert.That(ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "reserve-stock", true, true, true, true, false), Is.True);
            Assert.That(ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "rinse-water", true, true, true, true, false), Is.False);
            Assert.That(ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "reserve-stock", false, true, true, true, false), Is.False);
            Assert.That(ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "source", true, false, true, true, false), Is.False);
            Assert.That(ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "source", true, true, false, true, false), Is.False);
            Assert.That(ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "source", true, true, true, false, false), Is.False);
            Assert.That(ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "source", true, true, true, true, true), Is.False);
        }

        [Test]
        public void M3_PhysicalSecondReceiverContactRecordsOnlyValidCurrentNativePH()
        {
            Assert.That(ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "receiver", true, true, true, true, true, true), Is.True);
            Assert.That(ResearchPhysicalMeasurementRecording.CanRecord(
                "Lab", "receiver", true, true, true, true, true, true), Is.False);
            Assert.That(ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "receiver", false, true, true, true, true, true), Is.False);
            Assert.That(ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "receiver", true, false, true, true, true, true), Is.False);
            Assert.That(ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "source", true, true, true, true, true, true), Is.False);
            Assert.That(ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "receiver", true, true, false, true, true, true), Is.False);
            Assert.That(ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "receiver", true, true, true, false, true, true), Is.False);
            Assert.That(ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "receiver", true, true, true, true, false, true), Is.False);
            Assert.That(ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "receiver", true, true, true, true, true, false), Is.False);
        }

        [Test]
        public void M2_PhysicalNativeStockInspectionDemandsExclusiveFreshGesture()
        {
            Assert.That(ResearchPhysicalInspectionSelection.DesktopBottleInventory(
                "source", true, true, true, true), Is.EqualTo("source"));
            Assert.That(ResearchPhysicalInspectionSelection.DesktopBottleInventory(
                "reserve-stock", true, true, true, true), Is.EqualTo("reserve-stock"));
            Assert.That(ResearchPhysicalInspectionSelection.DesktopBottleInventory(
                "source", false, true, true, true), Is.Null);
            Assert.That(ResearchPhysicalInspectionSelection.DesktopBottleInventory(
                "source", true, false, true, true), Is.Null);
            Assert.That(ResearchPhysicalInspectionSelection.DesktopBottleInventory(
                "receiver", true, true, true, true), Is.Null);
            Assert.That(ResearchPhysicalInspectionSelection.XrUniqueInventory(
                true, true, true, false, .03, .2, .2), Is.EqualTo("receiver"));
            Assert.That(ResearchPhysicalInspectionSelection.XrUniqueInventory(
                true, true, true, false, .2, .03, .2), Is.EqualTo("source"));
            Assert.That(ResearchPhysicalInspectionSelection.XrUniqueInventory(
                true, true, true, false, .2, .2, .03), Is.EqualTo("reserve-stock"));
            Assert.That(ResearchPhysicalInspectionSelection.XrUniqueInventory(
                true, true, true, false, .03, .03, .2), Is.Null);
            Assert.That(ResearchPhysicalInspectionSelection.XrUniqueInventory(
                true, true, false, false, .2, .03, .2), Is.Null);
            Assert.That(ResearchPhysicalInspectionSelection.XrUniqueInventory(
                true, true, true, true, .2, .03, .2), Is.Null);
        }

        [Test]
        public void P10_01_ClickedColliderRatherThanViewCenterIsPicked()
        {
            var cameraOwner = new GameObject("ClickSelectionCamera");
            var clicked = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var centered = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                var camera = cameraOwner.AddComponent<Camera>();
                camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
                camera.transform.position = Vector3.zero;
                camera.transform.rotation = Quaternion.identity;
                clicked.transform.position = new Vector3(1.5f, 0f, 4f);
                centered.transform.position = new Vector3(0f, 0f, 4f);
                clicked.transform.localScale = Vector3.one * 0.35f;
                centered.transform.localScale = Vector3.one * 0.35f;
                Physics.SyncTransforms();
                var screenPoint = camera.WorldToScreenPoint(clicked.transform.position);
                var clickRay = DesktopInputAdapter.SelectionRay(camera,
                    new Vector2(screenPoint.x, screenPoint.y), true);
                Assert.That(Physics.Raycast(clickRay, out var clickHit, 5f), Is.True);
                Assert.That(clickHit.collider.gameObject, Is.SameAs(clicked));
                var fRay = DesktopInputAdapter.SelectionRay(camera, Vector2.zero, false);
                Assert.That(Physics.Raycast(fRay, out var centerHit, 5f), Is.True);
                Assert.That(centerHit.collider.gameObject, Is.SameAs(centered));
            }
            finally
            {
                Object.DestroyImmediate(clicked);
                Object.DestroyImmediate(centered);
                Object.DestroyImmediate(cameraOwner);
            }
        }

        [Test]
        public void P10_01_SelectionClickCannotAlsoActuateOrDragTheNativeTool()
        {
            Assert.That(DesktopPointerRouting.ResolveForHeldInstrument(
                true, true, false, true, false, true, true),
                Is.EqualTo(DesktopPointerAction.None));
            Assert.That(DesktopPointerRouting.ResolveForHeldInstrument(
                true, false, true, true, false, true, true),
                Is.EqualTo(DesktopPointerAction.None));
            Assert.That(DesktopPointerRouting.ResolveForHeldInstrument(
                true, true, false, true, false, true, false),
                Is.EqualTo(DesktopPointerAction.Valve));
        }

        [Test]
        public void M2_RestoredNativeSessionNeedsRealReleasedToolsNeutralControlsAndTracking()
        {
            var gate = new ResearchRestorationGestureGate();
            Assert.That(gate.IsArmed, Is.True);
            gate.RequireFreshNeutralInput();
            Assert.That(gate.IsArmed, Is.False);
            Assert.That(gate.ObserveNeutral(false, true, true), Is.False);
            Assert.That(gate.ObserveNeutral(true, false, true), Is.False);
            Assert.That(gate.ObserveNeutral(true, true, false), Is.False);
            Assert.That(gate.ObserveNeutral(true, true, true), Is.True);
            gate.RequireFreshNeutralInput();
            Assert.That(gate.IsArmed, Is.False);
        }

        [Test]
        public void M2_XRPhysicalReceiverTouchRequiresFreshTrackedEmptyHandAndNoUi()
        {
            Assert.That(ResearchXRReceiverInspectionAdmission.MayInspect(
                true, true, true, false, 0.02), Is.True);
            Assert.That(ResearchXRReceiverInspectionAdmission.MayInspect(
                true, false, true, false, 0.02), Is.False);
            Assert.That(ResearchXRReceiverInspectionAdmission.MayInspect(
                false, true, true, false, 0.02), Is.False);
            Assert.That(ResearchXRReceiverInspectionAdmission.MayInspect(
                true, true, false, false, 0.02), Is.False);
            Assert.That(ResearchXRReceiverInspectionAdmission.MayInspect(
                true, true, true, true, 0.02), Is.False);
            Assert.That(ResearchXRReceiverInspectionAdmission.MayInspect(
                true, true, true, false, double.NaN), Is.False);
            Assert.That(ResearchXRReceiverInspectionAdmission.MayInspect(
                true, true, true, false, 0.2), Is.False);
        }

        [Test]
        public void P10_01_WheelCannotMoveHeldInstrumentAndCameraTogether()
        {
            Assert.That(DesktopWheelRouting.Resolve(true, false, 120d),
                Is.EqualTo(DesktopWheelTarget.HeldToolHeight));
            Assert.That(DesktopWheelRouting.Resolve(false, false, 120d),
                Is.EqualTo(DesktopWheelTarget.CameraZoom));
            Assert.That(DesktopWheelRouting.Resolve(true, true, 120d),
                Is.EqualTo(DesktopWheelTarget.None));
            Assert.That(DesktopWheelRouting.HeightDeltaMetres(120d),
                Is.EqualTo(0.02d).Within(1e-10));
            Assert.That(DesktopWheelRouting.ZoomFieldOfViewDegrees(60d, 120d),
                Is.EqualTo(55d).Within(1e-10));
            Assert.That(DesktopWheelRouting.ZoomFieldOfViewDegrees(85d, -120d),
                Is.EqualTo(85d).Within(1e-10));
        }
    }
}
