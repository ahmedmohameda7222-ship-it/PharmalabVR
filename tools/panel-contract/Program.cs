// Headless behavioral coverage for actual production routing classes.
// This is intentionally NOT Unity scene/input, native chemistry, or headset evidence.
using System;
using PharmaLabVR.Core;
using PharmaLabVR.Input;
using PharmaLabVR.Session;
using PharmaLabVR.UI;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Main()
    {
        var initial = SetupPreferencesSelection.Current;
        try
        {
            var bootRouter = new ScreenRouter();
            bootRouter.Configure(null);
            bootRouter.Show(ScreenId.Home);
            var boot = new BootMenu { Router = bootRouter };
            var bootActions = new LabPanelController();
            bootActions.ConfigureBoot(bootRouter, boot);
            Check(bootActions.Request(PanelAction.EnterVR), "Home → VR Setup action refused");
            Check(bootRouter.Current.Mode == "VR", "Boot mode lost");
            Check(bootRouter.Current.ScreenId == ScreenId.Setup, "Setup route missing");
            var selected = new LabSetupPreferences("Seated", "Left", 0.74f, 1.2f, true, true, true, 0.5f);
            bootRouter.UpdateSetupPreferences(selected);
            Check(ReferenceEquals(bootRouter.Current.Setup, selected), "Immutable preference snapshot missing");
            Check(bootActions.Request(PanelAction.Home), "Setup → Home action refused");
            Check(bootActions.Request(PanelAction.EnterDesktop), "Home → Desktop Setup action refused");
            Check(bootRouter.Current.Mode == "Desktop", "Desktop mode lost");
            Check(ReferenceEquals(bootRouter.Current.Setup, selected), "Back navigation lost preferences");
            boot.CanConfirm = false;
            Check(!bootActions.Request(PanelAction.ConfirmSetup), "Unavailable Boot falsely returned success");
            Check(!boot.Confirmed, "Rejected Boot loaded the Lab anyway");
            boot.CanConfirm = true;
            Check(bootActions.Request(PanelAction.ConfirmSetup) && boot.Confirmed, "Setup confirmation refused");
            Check(!bootActions.Request(PanelAction.Pause), "Unsupported Boot action falsely succeeded");
            Check(selected.With(textScale: 1.3f).TextScale > selected.TextScale,
                "Immutable preference replacement did not apply");
            var invalidRejected = false;
            try { new LabSetupPreferences("Invalid", "Left", 0.74f, 1f, false, false, false); }
            catch (ArgumentException) { invalidRejected = true; }
            Check(invalidRejected, "Invalid posture was admitted");


            // P07.01: startup refuses a VR journey when XR is absent or the scene
            // cannot load, but Desktop remains available without a headset.
            var desktopAvailable = new BootLaunchAvailability(false, false, false, true);
            Check(desktopAvailable.CanEnter(ApplicationMode.Desktop), "Headset absence blocked Desktop");
            Check(!desktopAvailable.CanEnter(ApplicationMode.VirtualReality),
                "Missing XR loader / headset falsely admitted VR");
            var loaderOnly = new BootLaunchAvailability(true, true, false, true);
            Check(!loaderOnly.CanEnter(ApplicationMode.VirtualReality),
                "A loaded XR runtime without a present headset falsely admitted VR");
            var headsetReady = new BootLaunchAvailability(true, true, true, true);
            Check(headsetReady.CanEnter(ApplicationMode.VirtualReality), "Ready XR denied");
            Check(!new BootLaunchAvailability(true, true, true, false)
                    .CanEnter(ApplicationMode.VirtualReality), "Missing Lab scene falsely admitted VR");
            Check(!new BootLaunchAvailability(false, false, false, false)
                    .CanEnter(ApplicationMode.Desktop), "Missing Lab scene falsely admitted Desktop");
            Check(loaderOnly.UnavailableReason(ApplicationMode.VirtualReality).Contains("headset"),
                "Boot must explain why XR entry is unavailable");
            Check(!desktopAvailable.UnavailableReason(ApplicationMode.Desktop).Contains("unavailable"),
                "Desktop ready status was misleading");

            // P07.02: owning the tool body is not enough if an XR UI click
            // is being routed on the same trigger or ownership came from a ray.
            Check(!InteractionOwnership.MayActuateValve(false, true, false),
                "Ray selection must not actuate the gripped body valve");
            Check(!InteractionOwnership.MayActuateValve(true, false, false),
                "Lost controller tracking must not actuate the valve");
            Check(!InteractionOwnership.MayActuateValve(true, true, true),
                "The world-panel UI owns the trigger; valve must stay closed");
            Check(InteractionOwnership.MayActuateValve(true, true, false),
                "Valid direct grip should permit the held valve input");
            Check(!InteractionOwnership.MayActuateValve(false, false, true),
                "Unowned tool cannot be actuated");

            var driver = new CoreDriver();
            var router = new ScreenRouter();
            router.Configure(driver);
            Check(ReferenceEquals(router.Current.Setup, selected), "Scene-level selection did not survive");
            var session = new SessionController();
            var panels = new LabPanelController();
            panels.Configure(router, driver, session);
            Check(panels.Request(PanelAction.Inspect, "receiver"), "Inspect receiver action refused");
            Check(router.Current.ScreenId == ScreenId.Inspect, "Inspect route missing");
            Check(router.Current.ModelIdentity == "IPhreeqc-research", "Native-derived model metadata lost");
            Check(panels.Request(PanelAction.Measure, "receiver"), "Measure action refused");
            Check(router.Current.ScreenId == ScreenId.Measure, "Measure route missing");
            Check(panels.Request(PanelAction.Pause), "Pause action refused");
            Check(router.Current.ScreenId == ScreenId.Pause && driver.IsNativePaused, "Pause not held");
            Check(panels.Request(PanelAction.Settings), "Pause → Settings action refused");
            Check(router.Current.ScreenId == ScreenId.Settings, "Settings not visible");
            Check(panels.Request(PanelAction.Back), "Settings Back refused");
            Check(router.Current.ScreenId == ScreenId.Pause && driver.IsNativePaused,
                "Settings Back unpaused the operating authority");
            Check(panels.Request(PanelAction.Review), "Paused Review refused");
            Check(router.Current.ScreenId == ScreenId.Review, "Review route missing");
            Check(panels.Request(PanelAction.Lab), "Recorded Review return refused");
            Check(router.Current.ScreenId == ScreenId.Lab && !driver.IsNativePaused,
                "Review return failed to resume the paused authority");
            Check(driver.ContinueCalls == 1, "Review path did not submit exactly one Continue");
            Check(session.SaveCalls == 0, "Review implicitly saved");
            Check(session.RecordedReview == null, "Review close failed");
            Check(driver.CurrentLabView.EventSequence == "7", "Navigation altered native revision metadata");
            driver.SetHold(true);
            Check(router.Current.ScreenId == ScreenId.Hold, "Native hold not routed");
            Check(!panels.Request(PanelAction.Continue), "Unavailable held Continue falsely succeeded");
            Check(!panels.Request(PanelAction.Measure, "receiver"), "Held Measure falsely succeeded");
            Check(panels.Request(PanelAction.Review), "Recorded Review blocked during Hold");
            Check(!panels.Request(PanelAction.Lab), "Held Review bypassed native hold");
            Check(router.Current.ScreenId == ScreenId.Review, "Denied held return hid review");
            Check(session.SaveCalls == 0, "Hold/Review caused implicit Save");
            Console.WriteLine($"P06.02 headless routing checks: {checks} passed (NOT Unity, Player or Native Core)");
        }
        finally
        {
            SetupPreferencesSelection.Select(initial);
        }
    }
}
