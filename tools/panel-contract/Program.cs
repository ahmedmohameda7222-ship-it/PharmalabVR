// Headless behavioral coverage for actual production routing classes.
// This is intentionally NOT Unity scene/input, native chemistry, or headset evidence.
using System;
using System.Linq;
using PharmaLabVR.Core;
using PharmaLabVR.Input;
using PharmaLabVR.Session;
using PharmaLabVR.UI;
using PharmaLabVR.Tools;

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
        // P03.02: exercise the actual pure production payload builder through
        // the independent .NET compiler. Unity/Native runtime execution is separate.
        var placedBottle = ResearchInstrumentPayloads.Place("bottle-1", "source", "spill",
            ResearchInstrumentPayloads.BottleProfile);
        Check(placedBottle.Contains("\"geometryProfileHash\":\"bottle-100ml-research-v1\""),
            "Bottle command lost its distinct Native geometry profile");
        var measured = ResearchInstrumentPayloads.DispenseOrPour("bottle-1", "receiver", 2e-8, "4");
        Check(measured.Contains("\"quantityM3\":2E-08") &&
              measured.Contains("\"expectedInventoryRevision\":\"4\""),
            "Measured bottle transport lost captured quantity or revision");
        var aspirated = ResearchInstrumentPayloads.Aspirate("pipette", 5e-8, "2");
        Check(aspirated.Contains("\"quantityM3\":5E-08") &&
              !aspirated.Contains("destinationInventoryId"),
            "Aspirate accidentally encoded a delivery destination");
        var rinsed = ResearchInstrumentPayloads.Rinse("bottle-1", "rinse-water", "waste", 1e-8, "5");
        Check(rinsed.Contains("\"rinseSourceInventoryId\":\"rinse-water\"") &&
              rinsed.Contains("\"basis\":\"LiquidVolumeM3\""),
            "Rinse must debit a real source using Native volume-basis payload");
        var invalidVolumeRejected = false;
        try { ResearchInstrumentPayloads.Aspirate("pipette", double.NaN, "2"); }
        catch (ArgumentOutOfRangeException) { invalidVolumeRejected = true; }
        Check(invalidVolumeRejected, "Non-finite aspiration quantity reached Native JSON builder");
        var invalidProfileRejected = false;
        try { ResearchInstrumentPayloads.Place("bottle-1", "source", "spill", "burette-50ml-research-v1"); }
        catch (ArgumentException) { invalidProfileRejected = true; }
        Check(invalidProfileRejected, "Metered action accepted burette valve profile");
        var invalidRevisionRejected = false;
        try { ResearchInstrumentPayloads.SwitchPipetteSource("pipette", "base", "bad-revision"); }
        catch (ArgumentException) { invalidRevisionRejected = true; }
        Check(invalidRevisionRejected, "Source switch accepted malformed tool revision");
        var reserveRefill = ResearchInstrumentPayloads.RefillFromPreparedStock(
            "reserve-stock", "source", "spill", 2e-8);
        Check(reserveRefill.Contains("\"sourceInventoryId\":\"reserve-stock\"") &&
              reserveRefill.Contains("\"destinationInventoryId\":\"source\"") &&
              reserveRefill.Contains("\"HomogeneousAqueousLiquid\"") &&
              reserveRefill.Contains("\"fraction\":1.0"),
            "Bottle refill must use existing prepared stock via a real Native fixed transfer");
        var sameStockRejected = false;
        try { ResearchInstrumentPayloads.RefillFromPreparedStock("source", "source", "spill", 2e-8); }
        catch (ArgumentException) { sameStockRejected = true; }
        Check(sameStockRejected, "A bottle cannot refill itself through a fictitious stock transfer");
        // P03.03: moving receivers are not a valid static-aperture capture.
        Check(CaptureAdmission.MayCapture(1.0, 1.0, 0.08, 0.001, 0.05, 0.15, 0.0),
            "Stationary upright receiver with valid outlet should capture");
        Check(!CaptureAdmission.MayCapture(1.0, 1.0, 0.08, 0.001, 0.05, 0.15, 0.04),
            "Receiver moving at 4 cm/s must reject static capture");
        Check(!CaptureAdmission.MayCapture(1.0, 1.0, 0.08, 0.001, 0.05, 0.15, double.NaN),
            "Unknown receiver motion cannot become a valid committed capture");
        Check(!CaptureAdmission.MayCapture(1.0, 0.5, 0.08, 0.001, 0.05, 0.15, 0.0),
            "Sideways outlet must spill rather than hit the aperture");
        Check(!CaptureAdmission.MayCapture(1.0, 1.0, 0.08, 0.004, 0.05, 0.15, 0.0),
            "A tool outside the receiver rim must not capture");
        Check(!CaptureAdmission.MayCapture(1.0, 1.0, 0.20, 0.001, 0.05, 0.15, 0.0),
            "An outlet above the researched maximum fall distance must not capture");
        // P03.02: a 0.05 mL aspiration cannot dispense 0.05 mL
        // while the Native research pipette retains 0.10 mL in its tip.
        Check(!ResearchInstrumentPayloads.CanDispensePipette(5e-8, 5e-8),
            "A single 0.05 mL aspiration must not imply deliverable material");
        Check(!ResearchInstrumentPayloads.CanDispensePipette(1.49e-7, 5e-8),
            "Aliquot above represented tip hold-up is still insufficient");
        Check(ResearchInstrumentPayloads.CanDispensePipette(1.5e-7, 5e-8),
            "Accumulated 0.15 mL must permit measured 0.05 mL dispense");
        Check(!ResearchInstrumentPayloads.CanDispensePipette(double.NaN, 5e-8),
            "Invalid Native tip snapshot cannot enable dispense");
        Check(!ResearchInstrumentPayloads.CanDispensePipette(1e-4, double.PositiveInfinity),
            "Unbounded requested delivery cannot enable dispense");
        // P03.03: kinematic/XR-held receiver motion has no reliable
        // Rigidbody linearVelocity; sample real world transform displacement.
        var motion = new ReceiverMotionTracker();
        Check(motion.Observe(20, 2.0, 0.0, 0.0, 0.0) == 0.0,
            "Initial receiver motion observation must be stationary");
        Check(motion.Observe(21, 2.1, 0.0, 0.0, 0.0) == 0.0,
            "Unmoved receiver must remain a valid capture target");
        var moving = motion.Observe(22, 2.2, 0.01, 0.0, 0.0);
        Check(moving > CaptureAdmission.MaximumReceiverSpeedMetresPerSecond,
            "Translated receiver must be rejected even without a Rigidbody");
        Check(motion.Observe(22, 2.21, 0.02, 0.0, 0.0) == moving,
            "Repeated per-frame interactor sampling must preserve the moving state");
        Check(motion.Observe(23, 2.3, 0.01, 0.0, 0.0) == 0.0,
            "Stationary receiver must recover on a fresh frame");
        Check(double.IsPositiveInfinity(motion.Observe(24, double.NaN, 0.01, 0.0, 0.0)),
            "Non-finite capture timestamps must fail closed");
        // P06.02: VR stock setup must allow every supported research
        // concentration and keep water explicitly at zero mol/L.
        var matrix = new[] { 0.01, 0.05, 0.10 };
        Check(ResearchConcentrationSelection.Resolve(matrix, 0, "hcl") == 0.01,
            "XR stock choice ignored lower research concentration");
        Check(ResearchConcentrationSelection.Resolve(matrix, 1, "nacl") == 0.05,
            "XR stock choice ignored middle research concentration");
        Check(ResearchConcentrationSelection.Resolve(matrix, 2, "water") == 0.0,
            "Water must not inherit an analyte molarity");
        var invalidMatrixIndexRejected = false;
        try { ResearchConcentrationSelection.Resolve(matrix, 3, "hcl"); }
        catch (ArgumentOutOfRangeException) { invalidMatrixIndexRejected = true; }
        Check(invalidMatrixIndexRejected, "Unlisted research molarity was accepted");
        // P03.03: two geometrically eligible receiving rims are ambiguous.
        // Do not accidentally credit the first collider to Native material.
        Check(CaptureTargetSelection.ResolveUnique(new[] { "receiver" }) == "receiver",
            "Single eligible Native receiver must remain a valid capture.");
        Check(CaptureTargetSelection.ResolveUnique(new[] { "receiver", "beaker-b" }) == null,
            "Two valid receivers must NOT pick an arbitrary first candidate.");
        Check(CaptureTargetSelection.ResolveUnique(new[] { "receiver", "receiver" }) == null,
            "Two physical targets sharing an inventory id remain ambiguous.");
        Check(CaptureTargetSelection.ResolveUnique(Array.Empty<string>()) == null,
            "No aperture hit must preserve spill rather than invent capture.");
        Check(CaptureTargetSelection.ResolveUnique(new[] { "" }) == null,
            "Blank inventory destinations cannot be admitted.");
        Check(CaptureTargetSelection.ResolveUnique(new[] { "beaker-b", "receiver" }) == null,
            "Reversing candidate enumeration must not alter ambiguous rejection.");
        // P03.03/P07.03: return-to-dock requires a real nearby pose,
        // not just a nearby but horizontal/invalid body.
        Check(ToolMountAdmission.CanMount(0.05, 8.0, false),
            "Near, aligned released instrument should mount.");
        Check(!ToolMountAdmission.CanMount(0.05, 90.0, false),
            "Sideways research tool must not snap through bench into dock.");
        Check(!ToolMountAdmission.CanMount(0.18, 0.0, false),
            "Out-of-reach tool may not teleport to dock.");
        Check(!ToolMountAdmission.CanMount(0.05, 0.0, true),
            "Tool gripped by either hand cannot be silently mounted.");
        Check(!ToolMountAdmission.CanMount(double.NaN, 0.0, false),
            "Invalid world position cannot enter dock.");
        Check(!ToolMountAdmission.CanMount(0.02, double.NaN, false),
            "Invalid orientation cannot enter dock.");
        var initial = SetupPreferencesSelection.Current;
        try
        {
            // P03.01: the rendered pending drop is a view of the Native parcel,
            // never a source of material. A matching committed landing receipt
            // clears it exactly once, even if an older snapshot arrives later.
            var parcel = new DropParcelState();
            Check(!parcel.ApplySnapshot("", 5e-8),
                "Anonymous positive-volume drop must not become visible");
            Check(parcel.ActiveParcelId == "" && parcel.CommittedVolumeM3 == 0.0,
                "Anonymous input leaked a pending material visual");
            Check(parcel.ApplySnapshot("branch-1/8/burette-1", 5e-8),
                "Valid committed drop did not enter pending presentation");
            Check(parcel.ActiveParcelId == "branch-1/8/burette-1" &&
                parcel.CommittedVolumeM3 == 5e-8,
                "Presenter parcel identity or volume differed from Native");
            Check(!parcel.Landed("branch-1/7/burette-1"),
                "Unrelated landing receipt cleared another pending parcel");
            Check(parcel.ActiveParcelId == "branch-1/8/burette-1",
                "Unrelated landing changed the pending drop");
            Check(parcel.Landed("branch-1/8/burette-1"),
                "The Native landing for this parcel was not observed");
            Check(!parcel.Landed("branch-1/8/burette-1"),
                "Duplicate landing receipt was accepted twice");
            Check(parcel.ActiveParcelId == "" && parcel.CommittedVolumeM3 == 0.0,
                "Landed parcel remained visible as a hanging drop");
            Check(!parcel.ApplySnapshot("branch-1/8/burette-1", 5e-8),
                "Stale Native snapshot resurrected an already landed drop");
            // Explicit Native SessionLoad is a new replay boundary: a saved
            // pending parcel may legitimately reuse the pre-landing ID.
            parcel.ResetSession();
            Check(parcel.ApplySnapshot("branch-1/8/burette-1", 5e-8),
                "A legitimately restored Native SessionLoad drop stayed invisible");
            Check(parcel.Landed("branch-1/8/burette-1"),
                "Restored parcel must accept its own later landing exactly once");
            // P03.01 repeated held snapshots are NOT repeated Native restores.
            // A newer explicit restore of an older pre-landing checkpoint is.
            var repeated = new DropParcelState();
            Check(repeated.ApplyNativeSnapshot("branch-1/12/burette-1", 5e-8, "SessionLoad"),
                "Initial Native SessionLoad did not surface its pending parcel");
            Check(repeated.Landed("branch-1/12/burette-1"),
                "Landed receipt for restored parcel was ignored");
            Check(!repeated.ApplyNativeSnapshot("branch-1/12/burette-1", 5e-8, "SessionLoad"),
                "Repeated SessionLoad snapshot erased landed-parcel suppression");
            Check(!repeated.ApplyNativeSnapshot("branch-1/12/burette-1", 5e-8, "SessionLoad"),
                "Further held snapshots kept resurrecting an already landed parcel");
            repeated.ResetSession(); // accepted, explicit second import while still held
            Check(repeated.ApplyNativeSnapshot("branch-1/12/burette-1", 5e-8, "SessionLoad"),
                "Explicit same-reason Native session replacement lost saved pending parcel");
            Check(repeated.Landed("branch-1/12/burette-1"),
                "Second imported parcel did not acknowledge its actual landing");
            Check(!repeated.ApplyNativeSnapshot("branch-1/12/burette-1", 5e-8, "SessionLoad"),
                "A post-replacement held snapshot incorrectly reset receipt ownership");
            Check(repeated.ApplyNativeSnapshot("branch-1/12/burette-1", 5e-8, "CheckpointRestart"),
                "New Native checkpoint restore did not admit legitimately rewound parcel");
            Check(repeated.Landed("branch-1/12/burette-1"),
                "Checkpoint-restored pending drop did not land");
            Check(!repeated.ApplyNativeSnapshot("branch-1/12/burette-1", 5e-8, "CheckpointRestart"),
                "Repeated CheckpointRestart snapshot resurrected an already landed drop");
            Check(!repeated.ApplyNativeSnapshot("branch-1/12/burette-1", 5e-8, ""),
                "Leaving a hold should not itself reset the parcel ledger view");
            Check(repeated.ApplyNativeSnapshot("branch-1/12/burette-1", 5e-8, "SessionLoad"),
                "New SessionLoad epoch after a running sample should reset the view");

            Check(parcel.ApplySnapshot("branch-1/9/burette-1", 5e-8),
                "Next newly committed parcel was suppressed");
            Check(!parcel.ApplySnapshot("branch-1/9/burette-1", double.NaN),
                "Invalid pending volume must never be presented");
            Check(!parcel.ApplySnapshot("branch-1/9/burette-1", 0.0),
                "Zero-volume flight must hide the visual");
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

            // P07.03: the absence of an active grip cannot stand in for
            // real tracked head and controller devices during Native recovery.
            Check(!XRTrackingAdmission.HasFreshTracking(false, true, true),
                "Missing XR head falsely admitted a neutral recovery baseline");
            Check(!XRTrackingAdmission.HasFreshTracking(true, false, true),
                "Lost left hand falsely admitted native recovery");
            Check(!XRTrackingAdmission.HasFreshTracking(true, true, false),
                "Lost right hand falsely admitted native recovery");
            Check(!XRTrackingAdmission.HasFreshTracking(false, false, false),
                "Untracked VR devices falsely admitted native recovery");
            Check(XRTrackingAdmission.HasFreshTracking(true, true, true),
                "Fully tracked headset and both controllers should be eligible");

            // P07.03: a real Native import is held by SessionLoad; keep its
            // restored mode isolated from the currently active Unity rig and
            // refuse active/free-hand XR replacement before disposing old core.
            Check(SessionLoadAdmission.MayReplace("Desktop", "Desktop", false, false),
                "Desktop-to-Desktop import should be permitted without an XR dock");
            Check(!SessionLoadAdmission.MayReplace("Desktop", "VR", true, true),
                "Desktop rig accepted a VR session without BeginModeChange");
            Check(!SessionLoadAdmission.MayReplace("VR", "Desktop", true, true),
                "VR rig accepted a Desktop session without BeginModeChange");
            Check(!SessionLoadAdmission.MayReplace("VR", "VR", false, false),
                "VR import lacked the registered tool dock");
            Check(!SessionLoadAdmission.MayReplace("VR", "VR", true, false),
                "VR import while tool undocked falsely admitted");
            Check(SessionLoadAdmission.MayReplace("VR", "VR", true, true),
                "Parked VR tool with consistent import mode should be admissible");
            Check(!SessionLoadAdmission.MayReplace("Unsupported", "VR", true, true),
                "Unknown active mode falsely admitted imported session");
            Check(!SessionLoadAdmission.MayReplace("VR", null, true, true),
                "Missing imported Native mode falsely admitted");

            var driver = new CoreDriver();
            var router = new ScreenRouter();
            router.Configure(driver);
            Check(ReferenceEquals(router.Current.Setup, selected), "Scene-level selection did not survive");
            var session = new SessionController();
            var panels = new LabPanelController();
            panels.Configure(router, driver, session);
            Check(router.Current.SupportedActions.Contains("PipetteAspirate"),
                "Lab did not publish the revision-bound instrument route");
            Check(panels.Request(PanelAction.PrepareRinseWater),
                "VR/Desktop shared panel refused explicit setup water preparation");
            Check(panels.Request(PanelAction.PrepareResearchStock, "nacl"),
                "Shared Native stock preparation action refused");
            Check(session.PreparedStockId == "nacl", "Prepared stock identity was not preserved");
            Check(panels.Request(PanelAction.PipetteAspirate) &&
                  panels.Request(PanelAction.PipetteDispense) &&
                  panels.Request(PanelAction.PipetteRinse),
                "Pipette command panel routing did not call the actual SessionController methods");
            Check(panels.Request(PanelAction.BottlePour) && panels.Request(PanelAction.BottleRinse),
                "Bottle command panel routing did not reach measured pour and rinse");
            Check(session.MeteredCalls == 7,
                "A tool command was silently swallowed or replayed more than once");
            var meteredCount = session.MeteredCalls;
            session.DispensePermitted = false;
            Check(!panels.Request(PanelAction.PipetteDispense),
                "Shared panel allowed a pipette dispense below Native retained-tip volume");
            Check(session.MeteredCalls == meteredCount,
                "Rejected pipette dispense was forwarded to Native.");
            session.DispensePermitted = true;
            Check(panels.Request(PanelAction.PrepareReserveStock, "naoh"),
                "Panel cannot prepare real reserve stock during setup");
            Check(panels.Request(PanelAction.PipetteSwitchReserve),
                "Panel cannot switch pipette to preprepared reserve");
            Check(panels.Request(PanelAction.BottleRefill),
                "Panel cannot refill bottle from bounded prepared inventory");
            Check(session.MeteredCalls == 10,
                "Reserve/refill commands bypassed their one-time shared route");
            Check(session.SaveCalls == 0 && session.ReviewOpens == 0,
                "Lab instrument actions silently opened Review or created a Save");
            var concentrationRouter = new ScreenRouter();
            var concentrationDriver = new CoreDriver();
            concentrationRouter.Configure(concentrationDriver);
            var concentrationSession = new SessionController();
            var concentrationPanels = new LabPanelController();
            concentrationPanels.Configure(concentrationRouter, concentrationDriver, concentrationSession);
            Check(concentrationPanels.Request(PanelAction.PrepareResearchStock, "hcl", 0.05),
                "VR selected 0.05 mol/L source cannot route through shared panel");
            Check(concentrationSession.PreparedConcentration == 0.05,
                "Native stock setup lost its selected molarity");
            Check(concentrationPanels.Request(PanelAction.PrepareReserveStock, "water", 0.0),
                "Research water reserve must accept explicit zero molarity");
            Check(concentrationSession.PreparedConcentration == 0.0,
                "Water reserve illegally inherited last acid concentration");
            Check(panels.Request(PanelAction.Inspect, "receiver"), "Inspect receiver action refused");
            Check(router.Current.ScreenId == ScreenId.Inspect, "Inspect route missing");
            Check(router.Current.ModelIdentity == "IPhreeqc-research", "Native-derived model metadata lost");
            Check(panels.Request(PanelAction.Measure, "receiver"), "Measure action refused");
            Check(router.Current.ScreenId == ScreenId.Measure, "Measure route missing");
            Check(panels.Request(PanelAction.Pause), "Pause action refused");
            Check(router.Current.ScreenId == ScreenId.Pause && driver.IsNativePaused, "Pause not held");
            driver.CurrentMode = "VR";
            router.Configure(driver);
            Check(router.Current.SupportedActions.Contains("Load"),
                "VR Pause must expose an explicit saved-session Load action");
            session.LoadPermitted = false;
            Check(!panels.Request(PanelAction.Load),
                "Unsupported/missing saved file falsely returned Load success");
            Check(session.LoadCalls == 0, "Rejected Load touched a Native replacement");
            session.LoadPermitted = true;
            var beforeLoadRevision = driver.CurrentLabView.EventSequence;
            Check(panels.Request(PanelAction.Load),
                "Available Native-backed VR Save could not be loaded");
            Check(session.LoadCalls == 1, "Load was dispatched more than once");
            Check(driver.CurrentLabView.EventSequence == beforeLoadRevision,
                "UI Load action independently fabricated a Native material revision");
            Check(session.SaveCalls == 0, "Load silently wrote a new Save");
            driver.CurrentMode = "Desktop";
            router.Configure(driver);
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
            driver.CurrentMode = "VR";
            router.Configure(driver);
            Check(router.Current.SupportedActions.Contains("ReleaseTool"),
                "VR Lab omitted the dock-release action");
            Check(panels.Request(PanelAction.ReleaseTool),
                "An explicitly docked VR body cannot be released through the shared panel");
            Check(session.ReleaseCalls == 1, "Dock release must dispatch exactly once");
            Check(!panels.Request(PanelAction.ReleaseTool),
                "Releasing an already released tool falsely succeeded");
            Check(router.Current.SupportedActions.Contains("ParkTool"),
                "Released VR tool cannot be parked from the shared panel");
            session.ParkPermitted = false;
            Check(!panels.Request(PanelAction.ParkTool),
                "Out-of-range tool falsely reported a successful park");
            Check(session.ParkCalls == 0, "Rejected park mutated the tool mount");
            session.ParkPermitted = true;
            Check(panels.Request(PanelAction.ParkTool),
                "Valid nearby tool could not return to the rack");
            Check(session.ParkCalls == 1,
                "Valid parking must dispatch exactly once");
            Check(!panels.Request(PanelAction.ParkTool),
                "Double-park falsely succeeded");
            Check(session.SaveCalls == 0 && driver.CurrentLabView.EventSequence == "7",
                "Parking a physical tool must not Save or change Native revision");
            driver.CurrentMode = "Desktop";
            router.Configure(driver);
            // P07.03: recentering is presentation only, requires a parked
            // tool and actual head tracking; it must never auto-Continue.
            Check(!RecoveryAlignmentPolicy.CanRecenter(false, true, false, true, true),
                "Desktop incorrectly admitted XR recenter");
            Check(!RecoveryAlignmentPolicy.CanRecenter(true, false, false, true, true),
                "Running Native session incorrectly admitted recenter");
            Check(!RecoveryAlignmentPolicy.CanRecenter(true, true, false, false, true),
                "Untracked head admitted recenter");
            Check(!RecoveryAlignmentPolicy.CanRecenter(true, true, false, true, false),
                "Undocked tool admitted head teleport");
            Check(RecoveryAlignmentPolicy.CanRecenter(true, true, false, true, true),
                "Paused, parked VR should admit explicit recenter");
            Check(RecoveryAlignmentPolicy.CanRecenter(true, false, true, true, true),
                "Native hold with parked tool should admit recenter");

            driver.CurrentMode = "VR";
            router.Configure(driver);
            Check(!panels.Request(PanelAction.Recenter),
                "Running VR recenter falsely returned success");
            Check(!panels.Request(PanelAction.SnapLeft),
                "Unpaused VR falsely admitted a comfort snap turn");
            Check(RecoveryAlignmentPolicy.SnapDegrees(-1) == -30 &&
                RecoveryAlignmentPolicy.SnapDegrees(1) == 30,
                "P07.03 snap turn must be exactly 30 degrees");
            var rejectedInvalidSnap = false;
            try { RecoveryAlignmentPolicy.SnapDegrees(0); }
            catch (ArgumentOutOfRangeException) { rejectedInvalidSnap = true; }
            Check(rejectedInvalidSnap, "Invalid snap input silently rotates the rig");
            Check(panels.Request(PanelAction.Pause), "Pause required before XR recenter");
            Check(router.Current.SupportedActions.Contains("Recenter"), "Paused VR missing Recenter");
            Check(router.Current.SupportedActions.Contains("SnapLeft") &&
                router.Current.SupportedActions.Contains("SnapRight"),
                "Paused XR is missing explicit 30 degree turning controls");
            session.SnapPermitted = false;
            Check(!panels.Request(PanelAction.SnapRight),
                "Refused snap turn falsely returned success");
            session.SnapPermitted = true;
            Check(panels.Request(PanelAction.SnapLeft) &&
                panels.Request(PanelAction.SnapRight), "Valid snap turn rejected");
            Check(session.SnapCalls == 2 && session.LastSnapDirection == 1,
                "Snap turns must dispatch left -1 and right +1 exactly once");
            Check(driver.IsNativePaused && driver.ContinueCalls == 1,
                "Snap turns must not issue native Continue");
            session.RecenterPermitted = false;
            Check(!panels.Request(PanelAction.Recenter),
                "Failed XR recenter falsely returned success");
            session.RecenterPermitted = true;
            var beforeRecenterRevision = driver.CurrentLabView.EventSequence;
            var beforeRecenterContinue = driver.ContinueCalls;
            Check(panels.Request(PanelAction.Recenter), "Parked, paused XR recenter denied");
            Check(session.RecenterCalls == 1, "Recenter action was not dispatched exactly once");
            Check(driver.CurrentLabView.EventSequence == beforeRecenterRevision,
                "Recenter invented a material or observation revision");
            Check(driver.ContinueCalls == beforeRecenterContinue && driver.IsNativePaused,
                "Recenter silently resumed the Native Core");
            Check(panels.Request(PanelAction.Lab), "Cannot return to Lab after paused recenter");
            driver.CurrentMode = "Desktop";
            router.Configure(driver);
            Check(!panels.Request(PanelAction.Recenter), "Desktop admitted XR recenter");

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
