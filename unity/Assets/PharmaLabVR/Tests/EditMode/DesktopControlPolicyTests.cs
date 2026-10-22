using NUnit.Framework;
using PharmaLabVR.Input;

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
