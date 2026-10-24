// Headless behavioral coverage for actual production routing classes.
// This is intentionally NOT Unity scene/input, native chemistry, or headset evidence.
using System;
using System.Linq;
using PharmaLabVR.Core;
using PharmaLabVR.Lab;
using PharmaLabVR.Protocols;
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
        // P10.03 product directive: English-only, without any runtime locale toggle.
        var englishUi = new LocalizationService();
        englishUi.Load("en", new UnityEngine.TextAsset(
            System.IO.File.ReadAllText("unity/Assets/PharmaLabVR/Content/Localization/en.json")));
        Check(englishUi.Locale == "en" && englishUi.Get("offline") == "Offline",
            "Installed English product text was not loaded.");
        // Keep unwanted localization assets out of the shipping app and
        // prevent accidental reintroduction during later implementation.
        Check(!System.IO.File.Exists("unity/Assets/PharmaLabVR/Content/Localization/ar.json") &&
              !System.IO.File.Exists("unity/Assets/PharmaLabVR/Content/Fonts/NotoSansArabic-Regular.ttf") &&
              !System.IO.Directory.Exists("unity/Assets/ThirdParty/RTLTMPro") &&
              !System.IO.File.Exists("unity/Assets/PharmaLabVR/Runtime/UI/LanguageSelection.cs") &&
              !System.IO.File.Exists("unity/Assets/PharmaLabVR/Runtime/UI/LocalizedLabel.cs"),
            "English-only app contains a prohibited bilingual/RTL application asset.");
        var deniedOtherLocale = false;
        try { englishUi.Load("ar", new UnityEngine.TextAsset("{\"offline\":\"unavailable\"}")); }
        catch (ArgumentException) { deniedOtherLocale = true; }
        Check(deniedOtherLocale && englishUi.Locale == "en" && englishUi.Get("offline") == "Offline",
            "A second runtime language displaced the English UI.");
        var badCatalogDenied = false;
        try { englishUi.Load("en", new UnityEngine.TextAsset("{\"offline\":\"one\",\"offline\":\"two\"}")); }
        catch (FormatException) { badCatalogDenied = true; }
        Check(badCatalogDenied && englishUi.Get("offline") == "Offline",
            "A corrupted English table overwrote live product labels.");
        var otherScriptDenied = false;
        try { englishUi.Load("en", new UnityEngine.TextAsset(
            "{\"offline\":\"\u063A\u064A\u0631 \u0645\u062A\u0635\u0644\"}")); }
        catch (FormatException) { otherScriptDenied = true; }
        Check(otherScriptDenied && englishUi.Get("offline") == "Offline",
            "Non-English script was accepted in the shipping English text catalog.");
        var trailingCommaDenied = false;
        try { englishUi.Load("en", new UnityEngine.TextAsset("{\"offline\":\"Offline\",}")); }
        catch (FormatException) { trailingCommaDenied = true; }
        Check(trailingCommaDenied && englishUi.Get("offline") == "Offline",
            "A malformed product copy table was accepted.");

        // P09.02: a Native Current pH is displayable only when the model
        // reports Supported + Ready + Current and a finite numeric value.
        Check(ScientificReadoutPolicy.Resolve("Supported", "Ready", "Current", 7.13, true) ==
              MeasurementAvailability.Current,
            "Valid finite current Native pH was suppressed.");
        Check(ScientificReadoutPolicy.Resolve("Supported", "Ready", "Current", 0.0, false) ==
              MeasurementAvailability.Failed,
            "Missing Native pH value became a fabricated valid 0.00 after Unity deserialization.");
        Check(ScientificReadoutPolicy.Resolve("Supported", "Ready", "Current", 0.0, true) ==
              MeasurementAvailability.Current,
            "Valid numeric pH zero was confused with an omitted Native value.");
        Check(ScientificReadoutPolicy.Resolve("Supported", "Ready", "Current", double.NaN, true) ==
              MeasurementAvailability.Failed,
            "NaN pH was promoted as a current scientific measurement.");
        Check(ScientificReadoutPolicy.Resolve("Supported", "Ready", "Current", double.PositiveInfinity, true) ==
              MeasurementAvailability.Failed,
            "Infinite pH was promoted as a current scientific measurement.");
        Check(ScientificReadoutPolicy.Resolve("Supported", "Failed", "Current", 7.13, true) ==
              MeasurementAvailability.Failed,
            "Native failed computation displayed a current result.");
        Check(ScientificReadoutPolicy.Resolve("Supported", "Ready", "Stale", 7.13, true) ==
              MeasurementAvailability.Stale,
            "Native stale observation lost its freshness label.");
        Check(ScientificReadoutPolicy.Resolve("Unsupported", "Ready", "Current", 7.13, true) ==
              MeasurementAvailability.Unsupported,
            "Unsupported chemistry produced a readout.");
        Check(ScientificReadoutPolicy.Resolve("Supported", "Pending", "Absent", 0, false) ==
              MeasurementAvailability.Pending,
            "Pending real scientific result was fabricated.");
        var truthView = new MeasurementView();
        truthView.Present(MeasurementAvailability.Failed, "7.13", "Native pH failure");
        Check(truthView.DisplayValue == "--",
            "Failed scientific readout displayed a previous numeric value.");
        var staleXrLabel = ScientificReadoutPolicy.Describe("receiver",
            MeasurementAvailability.Stale, "7.13", "Supported", "Stale",
            "Research", "IPhreeqc-research");
        Check(staleXrLabel.Contains("Stale") && staleXrLabel.Contains("--") &&
              !staleXrLabel.Contains("7.13") && staleXrLabel.Contains("IPhreeqc-research"),
            "XR status label implied a current value for stale Native chemistry.");

        // M1 onboarding: the initial Native Pause is a controlled stock-setup
        // phase with real preparation actions, not a dead Pause menu.
        var onboardingNative = new CoreDriver { SourceNeedsPreparation = true };
        Check(onboardingNative.PauseSession(), "Initial Native setup pause fixture failed.");
        var onboardingRouter = new ScreenRouter();
        onboardingRouter.Configure(onboardingNative);
        Check(onboardingRouter.Current.ScreenId == ScreenId.Lab &&
              onboardingRouter.Current.SupportedActions.Contains("PrepareResearchStock") &&
              onboardingRouter.Current.SupportedActions.Contains("SelectResearchLesson") &&
              !onboardingRouter.Current.SupportedActions.Contains("PipetteDispense") &&
              !onboardingRouter.Current.SupportedActions.Contains("FillBurette") &&
              !onboardingRouter.Current.SupportedActions.Contains("Load"),
            "First-run setup must expose stock/lesson choices but no material transfer or load.");
        onboardingRouter.Show(ScreenId.Settings);
        Check(onboardingRouter.ReturnFromSettings() &&
              onboardingRouter.Current.ScreenId == ScreenId.Lab,
            "Initial stock-setup Settings cannot return to its Native-held workspace.");
        onboardingNative.SetHold(true);
        Check(onboardingRouter.Current.ScreenId == ScreenId.Hold,
            "Real input loss must supersede the initial stock-setup workspace.");
        onboardingNative.SetHold(false);
        Check(onboardingRouter.Current.ScreenId == ScreenId.Lab,
            "Recovered startup input hold incorrectly stranded stock preparation.");
        onboardingNative.SourceNeedsPreparation = false;
        onboardingNative.UpdateView();
        Check(onboardingRouter.Current.ScreenId == ScreenId.Pause &&
              !onboardingRouter.Current.SupportedActions.Contains("PrepareResearchStock"),
            "Ordinary Native Pause must not retain startup-only stock actions.");
        onboardingRouter.Show(ScreenId.Lab);
        Check(onboardingRouter.Current.ScreenId == ScreenId.Pause,
            "Normal paused Native authority admitted live Lab navigation.");

        // A notebook reading is not typed by the user or inferred from
        // a lesson. It is bound to a Current Native pH and matching vessel revision.
        var noteView = new LabViewModel();
        noteView.Vessels[1].MaterialRevision = "3";
        noteView.Vessels[1].CommittedVolumeM3 = 1e-8;
        noteView.Observations[0].Support = "Supported";
        noteView.Observations[0].Freshness = "Current";
        noteView.Observations[0].AsOfMaterialRevision = "3";
        noteView.Observations[0].DatabaseIdentity = "sha256:research-db";
        noteView.Observations[0].Value = 7.13;
        noteView.Observations[0].ValueAvailable = true;
        noteView.Observations[0].ComputationState = "Ready";
        var nativeNote = NativeMeasurementNote.Build(noteView, "receiver",
            MeasurementAvailability.Current, "7.13");
        var predictionMatch = ResearchLessonJourney.CompareRecordedPrediction(
            "at-least-7", noteView, "receiver", MeasurementAvailability.Current, "7.13");
        var predictionMismatch = ResearchLessonJourney.CompareRecordedPrediction(
            "below-7", noteView, "receiver", MeasurementAvailability.Current, "7.13");
        Check(predictionMatch.Contains("criterion matched") &&
              predictionMismatch.Contains("criterion not matched") &&
              predictionMismatch.Contains("Research reflection only") &&
              !predictionMatch.Contains("certified"),
            "Learner reflection must compare actual Native pH to a selected threshold without grading.");
        Check(nativeNote.Contains("pH 7.13") && nativeNote.Contains("revision 3") &&
              nativeNote.Contains("event 7") && nativeNote.Contains("sha256:research-db") &&
              nativeNote.Contains("IPhreeqc-research"),
            "Recorded pH note must preserve actual Native event, material and model identity.");
        // P12 student explanation is a deliberate bounded reflection on
        // previously supported Native data, never manufactured chemistry.
        var explained = ResearchLessonJourney.BuildRecordedExplanation(
            "strong-acid-titration-research", "model-limitations",
            noteView, "receiver", MeasurementAvailability.Current, "7.13");
        Check(explained.Contains("Student explanation for strong-acid-titration-research") &&
              explained.Contains("event 7") &&
              explained.Contains("model") &&
              explained.Contains("not Native scientific evidence"),
            "Learner explanation must retain the actual measured Native event and remain ungraded.");
        var unsupportedExplanationDenied = false;
        try {
            ResearchLessonJourney.BuildRecordedExplanation(
                "strong-acid-titration-research", "made-up-scoring",
                noteView, "receiver", MeasurementAvailability.Current, "7.13");
        }
        catch (InvalidOperationException) { unsupportedExplanationDenied = true; }
        Check(unsupportedExplanationDenied,
            "Arbitrary or grade-like explanations were admitted.");
        var invalidMeasurementExplanationDenied = false;
        try {
            ResearchLessonJourney.BuildRecordedExplanation(
                "strong-acid-titration-research", "method-uncertainty",
                noteView, "receiver", MeasurementAvailability.Stale, "7.13");
        }
        catch (InvalidOperationException) { invalidMeasurementExplanationDenied = true; }
        Check(invalidMeasurementExplanationDenied,
            "A stale unsupported observation yielded a student explanation.");
        // Rounded pH 7.00 MUST NOT decide a less-than-7 hypothesis.
        // Native raw 6.999 and raw 7.001 legitimately have the SAME
        // 2-decimal UI label, but opposite scientific threshold outcomes.
        noteView.Observations[0].ValueAvailable = true;
        noteView.Observations[0].ComputationState = "Ready";
        noteView.Observations[0].Value = 6.999;
        var rawBelow = ResearchLessonJourney.CompareRecordedPrediction(
            "below-7", noteView, "receiver", MeasurementAvailability.Current, "7.00");
        Check(rawBelow.Contains("criterion matched") && rawBelow.Contains("6.999"),
            "Raw Native pH below 7 was incorrectly classified by rounded UI pH 7.00.");
        noteView.Observations[0].Value = 7.001;
        var rawAbove = ResearchLessonJourney.CompareRecordedPrediction(
            "at-least-7", noteView, "receiver", MeasurementAvailability.Current, "7.00");
        Check(rawAbove.Contains("criterion matched") && rawAbove.Contains("7.001"),
            "Raw Native pH above 7 was incorrectly classified by rounded UI pH 7.00.");
        var mismatchDenied = false;
        try { NativeMeasurementNote.Build(noteView, "receiver",
            MeasurementAvailability.Current, "6.35"); }
        catch (InvalidOperationException) { mismatchDenied = true; }
        Check(mismatchDenied, "UI pH inconsistent with Native observation was saved as evidence.");
        noteView.Observations[0].Value = 7.13;
        noteView.Observations[0].ValueAvailable = false;
        var absentRawDenied = false;
        try { NativeMeasurementNote.Build(noteView, "receiver",
            MeasurementAvailability.Current, "7.13"); }
        catch (InvalidOperationException) { absentRawDenied = true; }
        Check(absentRawDenied, "An unavailable raw Native observation was accepted from display text.");
        noteView.Observations[0].ValueAvailable = true;
        var staleDenied = false;
        noteView.Observations[0].AsOfMaterialRevision = "2";
        try { NativeMeasurementNote.Build(noteView, "receiver", MeasurementAvailability.Current, "7.13"); }
        catch (InvalidOperationException) { staleDenied = true; }
        Check(staleDenied, "Old measurement revision was allowed into the notebook.");
        var oldReflectionDenied = false;
        try { ResearchLessonJourney.CompareRecordedPrediction("below-7",
            noteView, "receiver", MeasurementAvailability.Current, "7.13"); }
        catch (InvalidOperationException) { oldReflectionDenied = true; }
        Check(oldReflectionDenied,
            "An outdated Native observation was admitted into a student prediction comparison.");
        noteView.Observations[0].AsOfMaterialRevision = "3";
        var unsupportedDenied = false;
        try { NativeMeasurementNote.Build(noteView, "receiver", MeasurementAvailability.Stale, "7.13"); }
        catch (InvalidOperationException) { unsupportedDenied = true; }
        Check(unsupportedDenied, "A stale display was recorded as a current pH result.");
        var nonfiniteDenied = false;
        try { NativeMeasurementNote.Build(noteView, "receiver", MeasurementAvailability.Current, "NaN"); }
        catch (InvalidOperationException) { nonfiniteDenied = true; }
        Check(nonfiniteDenied, "A non-finite pH value was admitted into the notebook.");
        var missingModelDenied = false;
        noteView.Observations[0].DatabaseIdentity = "";
        try { NativeMeasurementNote.Build(noteView, "receiver", MeasurementAvailability.Current, "7.13"); }
        catch (InvalidOperationException) { missingModelDenied = true; }
        Check(missingModelDenied, "A reading without the actual Native model package was recorded.");
        noteView.Observations[0].DatabaseIdentity = "sha256:research-db";
        var noteRouter = new ScreenRouter();
        noteRouter.Configure(new CoreDriver());
        noteRouter.Show(ScreenId.Measure, "receiver");
        var notePanels = new LabPanelController();
        var noteSession = new SessionController();
        notePanels.Configure(noteRouter, new CoreDriver(), noteSession);
        Check(noteRouter.Current.SupportedActions.Contains("RecordCurrentPH"),
            "Native pH record action missing from the shared measurement page.");
        Check(notePanels.Request(PanelAction.RecordCurrentPH) &&
              noteSession.NotebookAppendCalls == 1,
            "Measurement recording does not route through the shared Desktop/XR panel.");
        noteSession.CanRecordCurrentNativePH = false;
        Check(!notePanels.Request(PanelAction.RecordCurrentPH) &&
              noteSession.NotebookAppendCalls == 1,
            "Rejected measurement attempt appended an unverified note.");
        noteRouter.Show(ScreenId.Notebook);
        Check(noteRouter.Current.SupportedActions.Contains("RecordResearchExplanation") &&
              notePanels.Request(PanelAction.RecordResearchExplanation, "model-limitations") &&
              noteSession.NotebookAppendCalls == 2 &&
              !notePanels.Request(PanelAction.RecordResearchExplanation, "model-limitations"),
            "Shared Notebook must commit only one learner-selected explanation for a recorded pH.");
        Check(noteRouter.Current.SupportedActions.Contains("Save") &&
              noteRouter.Current.SupportedActions.Contains("Review") &&
              notePanels.Request(PanelAction.Save) &&
              notePanels.Request(PanelAction.Review) &&
              noteRouter.Current.ScreenId == ScreenId.Review,
            "Notebook-to-durable-save-to-recorded-review journey is not routable.");

        // P12.03 concept inspection must describe existing Native material
        // and exact supported observation, never a simulated plot/result.
        noteView.Observations[0].Value = 7.13d;
        noteView.Observations[0].ValueAvailable = true;
        noteView.Observations[0].ComputationState = "Ready";
        var concept = ResearchConceptReadout.Describe(noteView, "receiver");
        Check(concept.Contains("Native event 7") &&
              concept.Contains("Native material revision 3") &&
              concept.Contains("0.010 mL") &&
              concept.Contains("pH 7.130") &&
              concept.Contains("sha256:research-db"),
            "Concept pane failed to display provenance from the real Native snapshot.");
        noteView.Observations[0].AsOfMaterialRevision = "2";
        Check(ResearchConceptReadout.Describe(noteView, "receiver")
                .Contains("pH unavailable") &&
              !ResearchConceptReadout.Describe(noteView, "receiver")
                .Contains("pH 7.130"),
            "Outdated material revision was presented as a supported current pH concept.");
        noteView.Observations[0].AsOfMaterialRevision = "3";
        noteView.Observations[0].ValueAvailable = false;
        Check(ResearchConceptReadout.Describe(noteView, "receiver")
                .Contains("pH unavailable"),
            "A missing Native computed value was manufactured by a concept panel.");
        noteView.Observations[0].ValueAvailable = true;
        Check(ResearchConceptReadout.Describe(noteView, "not-a-native-vessel")
                .Contains("unavailable"),
            "Unknown catalog vessel should not yield a fabricated concept explanation.");
        var conceptRouter = new ScreenRouter();
        conceptRouter.Configure(new CoreDriver());
        var conceptPanel = new LabPanelController();
        conceptPanel.Configure(conceptRouter, new CoreDriver(), new SessionController());
        Check(conceptPanel.Request(PanelAction.Learn, "receiver") &&
              conceptRouter.Current.ScreenId == ScreenId.Learn &&
              conceptRouter.Current.SupportedActions.Contains("Lab"),
            "Shared Desktop/XR concept action did not open the real learn view.");
        conceptPanel.Request(PanelAction.Lab);

        // P12.02 student-prediction admission is scoped to the Native branch
        // and an actually empty receiver; hypothesis never mutates material.
        var predictiveView = new LabViewModel();
        var predictiveDefinition = new ProtocolDefinition {
            schemaVersion = 1, id = "strong-acid-titration-research",
            maturity = "Research", certifiedScoring = false,
            steps = new[] { "Prepare", "Transfer", "Inspect" }
        };
        var predictionJourney = new ResearchLessonJourney(predictiveDefinition, "predict-branch");
        Check(predictionJourney.CanPredict("predict-branch", predictiveView, false, null),
            "Learner cannot predict before starting the physical experiment.");
        Check(!predictionJourney.CanPredict("wrong-branch", predictiveView, false, null) &&
              !predictionJourney.CanPredict("predict-branch", predictiveView, true, null) &&
              !predictionJourney.CanPredict("predict-branch", predictiveView, false, "below-7"),
            "Prediction must not be duplicated across branch/hold/prior selection.");
        predictiveView.Vessels[1].CommittedVolumeM3 = 1e-8;
        Check(!predictionJourney.CanPredict("predict-branch", predictiveView, false, null),
            "Prediction must precede, not follow, Native receiver material transfer.");
        // Guided steps must reflect real Native and local Notebook evidence,
        // never a fabricated score or assumed completion.
        var blankSteps = predictionJourney.EvidenceChecklist(
            "predict-branch", new LabViewModel(), Array.Empty<NotebookEntry>());
        Check(blankSteps.Contains("Pending: prepare Native stock") &&
              blankSteps.Contains("Pending: student hypothesis") &&
              blankSteps.Contains("Pending: recorded Native pH") &&
              !blankSteps.Contains("certified complete"),
            "New lab incorrectly claims Native or learner progress.");
        var changedSteps = predictionJourney.EvidenceChecklist(
            "different-branch", predictiveView, Array.Empty<NotebookEntry>());
        Check(changedSteps.Contains("different Native branch") &&
              !changedSteps.Contains("Recorded:"),
            "Research checklist inherited progress across Native branch boundaries.");
        predictiveView.Vessels[0].CommittedVolumeM3 = 1e-7;
        predictiveView.Vessels[0].MaterialRevision = "1";
        predictiveView.Vessels[1].MaterialRevision = "3";
        predictiveView.Observations[0].Support = "Supported";
        predictiveView.Observations[0].Freshness = "Current";
        predictiveView.Observations[0].ComputationState = "Ready";
        predictiveView.Observations[0].AsOfMaterialRevision = "3";
        predictiveView.Observations[0].DatabaseIdentity = "sha256:research-db";
        predictiveView.Observations[0].Value = 7.13;
        predictiveView.Observations[0].ValueAvailable = true;
        var hypothesis = new NotebookEntry("predict-branch", "5", "receiver",
            "Student hypothesis for strong-acid-titration-research: receiver pH predicted below 7. " +
            "This is a learner prediction, not a Native result or certified endpoint.",
            DateTime.UtcNow.ToString("O"));
        var progressMeasurement = new NotebookEntry("predict-branch", "7", "receiver", nativeNote,
            DateTime.UtcNow.ToString("O"));
        var progressExplanation = new NotebookEntry("predict-branch", "7", "receiver",
            "Student explanation for strong-acid-titration-research at Native event 7 · model-limitations: " +
            "Selected by learner; not Native scientific evidence.",
            DateTime.UtcNow.ToString("O"));
        // A restored Native checkpoint must never inherit a student
        // prediction written later on the same branch. Durable notebook
        // annotations remain tied to their original Native event sequence.
        var futureHypothesis = new NotebookEntry("predict-branch", "9", "receiver",
            "Student hypothesis for strong-acid-titration-research: receiver pH predicted at least 7. " +
            "This is a learner prediction, not a Native result or certified endpoint.",
            DateTime.UtcNow.ToString("O"));
        Check(ResearchLessonJourney.RecoverSavedPrediction(
                new[] { futureHypothesis }, predictiveDefinition.id,
                "predict-branch", predictiveView.EventSequence) == null,
            "Restored event 7 inherited a prediction recorded in future event 9.");
        Check(ResearchLessonJourney.RecoverSavedPrediction(
                new[] { hypothesis }, predictiveDefinition.id, "predict-branch", "4") == null &&
              ResearchLessonJourney.RecoverSavedPrediction(
                new[] { hypothesis }, predictiveDefinition.id, "predict-branch", "5") == "below-7",
            "Predictions must become visible at their exact authored Native event.");
        // On SessionLoad, only one authenticated, branch-matched, event-
        // bounded lesson hypothesis can automatically restore guidance.
        Check(ResearchLessonJourney.RecoverSavedLessonId(
                new[] { hypothesis, futureHypothesis }, "predict-branch", "7") ==
                "strong-acid-titration-research",
            "Saved Research lesson was not recovered from its original durable hypothesis.");
        Check(ResearchLessonJourney.RecoverSavedLessonId(
                new[] { futureHypothesis }, "predict-branch", "7") == null &&
              ResearchLessonJourney.RecoverSavedLessonId(
                new[] { hypothesis }, "other-branch", "7") == null &&
              ResearchLessonJourney.RecoverSavedLessonId(
                new[] { hypothesis }, "predict-branch", "4") == null,
            "A future, foreign or not-yet-recorded prediction must not restore a lesson.");
        var contradictoryPrediction = new NotebookEntry(
            "predict-branch", "6", "receiver",
            "Student hypothesis for strong-acid-titration-research: receiver pH predicted at least 7. " +
            "This is a learner prediction, not a Native result or certified endpoint.",
            DateTime.UtcNow.ToString("O"));
        Check(ResearchLessonJourney.HasAmbiguousSavedPredictions(
                new[] { hypothesis, contradictoryPrediction }, "predict-branch", "7") &&
              !ResearchLessonJourney.HasAmbiguousSavedPredictions(
                new[] { hypothesis, futureHypothesis }, "predict-branch", "7") &&
              !ResearchLessonJourney.HasAmbiguousSavedPredictions(
                new[] { hypothesis, hypothesis }, "predict-branch", "7"),
            "Conflicting on-branch learner guidance needs an explicit visible warning.");
        Check(ResearchLessonJourney.RecoverSavedPrediction(
                new[] { hypothesis, contradictoryPrediction },
                predictiveDefinition.id, "predict-branch", "7") == null &&
              ResearchLessonJourney.RecoverSavedLessonId(
                new[] { hypothesis, contradictoryPrediction }, "predict-branch", "7") == null,
            "Contradictory student hypotheses for the SAME lesson must not auto-restore.");
        Check(ResearchLessonJourney.RecoverSavedLessonId(
                new[] { hypothesis, contradictoryPrediction,
                    new NotebookEntry("predict-branch", "6", "receiver",
                        "Student hypothesis for mixed-acid-titration-research: receiver pH predicted below 7. " +
                        "This is a learner prediction, not a Native result or certified endpoint.",
                        DateTime.UtcNow.ToString("O")) },
                "predict-branch", "7") == null,
            "A contradictory lesson must not be ignored to auto-select another lesson.");
        Check(ResearchLessonJourney.RecoverSavedPrediction(
                new[] { hypothesis, contradictoryPrediction },
                predictiveDefinition.id, "predict-branch", "5") == "below-7" &&
              ResearchLessonJourney.RecoverSavedPrediction(
                new[] { hypothesis, hypothesis }, predictiveDefinition.id,
                "predict-branch", "7") == "below-7",
            "Future conflicting notes must remain outside the restored event ceiling; exact duplicates are stable.");
        var conflictingLesson = new NotebookEntry("predict-branch", "6", "receiver",
            "Student hypothesis for mixed-acid-titration-research: receiver pH predicted at least 7. " +
            "This is a learner prediction, not a Native result or certified endpoint.",
            DateTime.UtcNow.ToString("O"));
        Check(ResearchLessonJourney.RecoverSavedLessonId(
                new[] { hypothesis, conflictingLesson }, "predict-branch", "7") == null,
            "Two conflicting student lessons cannot be silently auto-selected.");

        var futureChecklist = predictionJourney.EvidenceChecklist(
            "predict-branch", predictiveView, new[] { futureHypothesis });
        Check(futureChecklist.Contains("Pending: student hypothesis") &&
              !futureChecklist.Contains("Recorded: student hypothesis"),
            "Research checklist credited work from a future Native event.");
        var steps = predictionJourney.EvidenceChecklist("predict-branch",
            predictiveView, new[] { hypothesis, progressMeasurement, progressExplanation });
        Check(steps.Contains("Recorded: student hypothesis") &&
              steps.Contains("Observed: Native receiver transfer") &&
              steps.Contains("Recorded: Native pH") &&
              steps.Contains("Recorded: learner reflection") &&
              steps.Contains("No certified grade"),
            "Evidence checklist failed to connect real Native events and durable student notes.");
        // M3 guidance must never claim pH is Supported/Current based on
        // its display flags alone. Computation readiness, actual Native
        // numeric presence and receiver material revision still govern.
        predictiveView.Observations[0].ComputationState = "Pending";
        Check(!predictionJourney.Summary("predict-branch", predictiveView, false, false)
                .Contains("Native pH reports Supported/Current"),
            "A pending solver observation was announced as current research pH.");
        predictiveView.Observations[0].ComputationState = "Ready";
        predictiveView.Observations[0].ValueAvailable = false;
        Check(!predictionJourney.Summary("predict-branch", predictiveView, false, false)
                .Contains("Native pH reports Supported/Current"),
            "An unavailable Native numeric value was announced as current pH.");
        predictiveView.Observations[0].ValueAvailable = true;
        predictiveView.Observations[0].AsOfMaterialRevision = "2";
        Check(!predictionJourney.Summary("predict-branch", predictiveView, false, false)
                .Contains("Native pH reports Supported/Current"),
            "An observation for an older receiver revision was labeled current.");
        predictiveView.Observations[0].AsOfMaterialRevision = "3";
        var verifiedGuidance = predictionJourney.Summary(
            "predict-branch", predictiveView, false, false);
        Check(verifiedGuidance.Contains("Native pH reports Supported/Current"),
            "A complete matching Native observation was not presented as current.");
        var unrecorded = predictionJourney.EvidenceChecklist("predict-branch",
            predictiveView, new[] { hypothesis });
        Check(unrecorded.Contains("Pending: recorded Native pH") &&
              unrecorded.Contains("Pending: learner reflection"),
            "Current Native pH without a saved Notebook observation was presented as recorded work.");
        var conciseNext = predictionJourney.CompactEvidenceStatus(
            "predict-branch", new LabViewModel(), Array.Empty<NotebookEntry>());
        Check(conciseNext.Contains("Next: prepare Native stock") && conciseNext.Length < 180,
            "XR guided status must fit the panel and identify the actual next pending action.");
        // XR must prioritize safety recovery over a chemistry "Next" prompt.
        // A genuine Native Pause is not first-run stock preparation; a Hold
        // always wins and cannot imply a permitted student transfer.
        var heldGuidance = predictionJourney.CompactEvidenceStatus(
            "predict-branch", predictiveView, Array.Empty<NotebookEntry>(),
            paused: false, held: true);
        var pausedGuidance = predictionJourney.CompactEvidenceStatus(
            "predict-branch", predictiveView, Array.Empty<NotebookEntry>(),
            paused: true, held: false);
        Check(heldGuidance.StartsWith("Hold:", StringComparison.Ordinal) &&
              !heldGuidance.Contains("Next:") && heldGuidance.Length < 180 &&
              pausedGuidance.StartsWith("Pause:", StringComparison.Ordinal) &&
              !pausedGuidance.Contains("Next:") && pausedGuidance.Length < 180,
            "XR lesson guide suggested chemistry commands during Native Hold/Pause.");
        Check(predictionJourney.CompactEvidenceStatus(
                "predict-branch", predictiveView, Array.Empty<NotebookEntry>(),
                paused: true, held: true).StartsWith("Hold:", StringComparison.Ordinal),
            "Native Hold must take priority over normal Pause guidance.");
        var conciseRecorded = predictionJourney.CompactEvidenceStatus(
            "predict-branch", predictiveView, new[] { hypothesis, progressMeasurement, progressExplanation });
        Check(conciseRecorded.Contains("Study steps documented") &&
              conciseRecorded.Contains("not graded") && conciseRecorded.Length < 180,
            "Recorded learner progress should be compact, factual and never a grade.");

        Check(ResearchLessonJourney.IsSupportedPrediction("below-7") &&
              ResearchLessonJourney.IsSupportedPrediction("at-least-7") &&
              !ResearchLessonJourney.IsSupportedPrediction("made-up-endpoint"),
            "Prediction permits only the two explicitly student-authored hypotheses.");
        var predictorDriver = new CoreDriver { SourceNeedsPreparation = true };
        predictorDriver.PauseSession();
        var predictionRouter = new ScreenRouter();
        predictionRouter.Configure(predictorDriver);
        var predictionPanels = new LabPanelController();
        var predictionSession = new SessionController();
        predictionPanels.Configure(predictionRouter, predictorDriver, predictionSession);
        Check(predictionRouter.Current.SupportedActions.Contains("RecordResearchPrediction") &&
              predictionPanels.Request(PanelAction.RecordResearchPrediction, "below-7") &&
              predictionSession.NotebookAppendCalls == 1 &&
              !predictionPanels.Request(PanelAction.RecordResearchPrediction, "below-7"),
            "Shared first-run Desktop/XR prediction did not durably request a single annotation.");

        // Instructor review must expose Native precision only if the
        // recorded observation itself is Supported/Current and agrees with
        // the visible 2-decimal projection. No derived student grades.
        var recordedBelow = InstructorObservationLabel.Render(
            "receiver", "7.00", 6.999, true, "3");
        var recordedAbove = InstructorObservationLabel.Render(
            "receiver", "7.00", 7.001, true, "3");
        Check(recordedBelow.Contains("raw 6.999") &&
              recordedAbove.Contains("raw 7.001") &&
              recordedBelow.Contains("display 7.00") &&
              !recordedBelow.Contains("grade"),
            "Instructor preview discarded Native precision near the pH-7 learner criterion.");
        Check(!InstructorObservationLabel.Render(
                "receiver", "--", 6.999, false, "3").Contains("6.999") &&
              !InstructorObservationLabel.Render(
                "receiver", "7.00", double.NaN, true, "3").Contains("NaN") &&
              !InstructorObservationLabel.Render(
                "receiver", "6.80", 6.999, true, "3").Contains("6.999"),
            "Offline teaching preview exposed absent, nonfinite or inconsistent raw Native science.");

        // M3 learner review RED: a saved Native session's review must show
        // this branch's durable prediction and observation annotations,
        // including after a lesson controller is reconstructed.
        var researchNotes = new NotebookService();
        var learningDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plv-learning-review-" + Guid.NewGuid().ToString("N"));
        var learningPath = System.IO.Path.Combine(learningDirectory, "study.plv-notes");
        try
        {
            researchNotes.Append(learningPath, "student-branch", "7", "receiver",
                "Student hypothesis: receiver pH below 7.");
            researchNotes.Append(learningPath, "student-branch", "11", "receiver",
                "Native pH 6.20 · material revision 3 · event 11 · database pinned-db.");
            researchNotes.Append(learningPath, "different-tenant-branch", "14", "source",
                "Private unrelated student annotation.");
            // A Native checkpoint at event 7 must not display/export local
            // annotations from event 11 merely because the branch ID agrees.
            // Real recorded review must load only branch+event-bounded journal
            // evidence. The same persisted path must never expose a future
            // measurement, and corrupt bytes must not crash the Review screen.
            var boundedReview = ResearchLearningReview.ReadRecordedJournal(
                researchNotes, learningPath, "student-branch",
                "student-branch", "7", false);
            Check(boundedReview.Contains("Student hypothesis", StringComparison.Ordinal) &&
                  !boundedReview.Contains("Native pH 6.20", StringComparison.Ordinal) &&
                  !boundedReview.Contains("Private unrelated", StringComparison.Ordinal),
                "Restored event-7 Review exposed future or cross-branch Notebook evidence.");
            var restoredReview = ResearchLearningReview.ReadRecordedJournal(
                researchNotes, learningPath, "student-branch",
                "student-branch", "11", false);
            Check(restoredReview.Contains("Native pH 6.20", StringComparison.Ordinal) &&
                  restoredReview.Contains("Student hypothesis", StringComparison.Ordinal) &&
                  !restoredReview.Contains("Private unrelated", StringComparison.Ordinal),
                "Valid saved Native review lost current durable student observations.");
            var foreignReview = ResearchLearningReview.ReadRecordedJournal(
                researchNotes, learningPath, "other-live-branch",
                "student-branch", "11", false);
            Check(!foreignReview.Contains("Student hypothesis", StringComparison.Ordinal) &&
                  !foreignReview.Contains("Native pH 6.20", StringComparison.Ordinal),
                "Review displayed recorded notes from another live Native branch.");
            var damagedJournalPath = System.IO.Path.Combine(
                learningDirectory, "damaged.plv-notes");
            researchNotes.Append(damagedJournalPath, "student-branch", "7",
                "receiver", "Private unverified damaged journal content.");
            System.IO.File.WriteAllText(damagedJournalPath,
                "PLV-NOTEBOOK-V1\\ninvalid-record\\n");
            var damagedReview = ResearchLearningReview.ReadRecordedJournal(
                researchNotes, damagedJournalPath, "student-branch",
                "student-branch", "7", false);
            var activeNotebookAtSeven = ResearchLearningReview.ReadActiveNotebookSummary(
                researchNotes, learningPath, "student-branch", "7");
            Check(activeNotebookAtSeven.Contains("Latest · event 7", StringComparison.Ordinal) &&
                  activeNotebookAtSeven.Contains("Student hypothesis", StringComparison.Ordinal) &&
                  !activeNotebookAtSeven.Contains("Native pH 6.20", StringComparison.Ordinal) &&
                  !activeNotebookAtSeven.Contains("Private unrelated", StringComparison.Ordinal),
                "Notebook screen displayed future or foreign annotations after checkpoint restore.");
            var emptyNotebook = ResearchLearningReview.ReadActiveNotebookSummary(
                researchNotes, System.IO.Path.Combine(learningDirectory, "no-notes.plv-notes"),
                "student-branch", "7");
            Check(emptyNotebook.Contains("no recorded annotations", StringComparison.Ordinal),
                "A genuinely empty notebook must not be misreported as corrupted.");
            // A previously committed .bak may contain OLD valid evidence.
            // When the current primary is visibly corrupt it must NOT be
            // silently promoted to the student's current Notebook or Review.
            var backedJournal = System.IO.Path.Combine(
                learningDirectory, "backed-journal.plv-notes");
            researchNotes.Append(backedJournal, "student-branch", "7",
                "receiver", "Old backup-only learner hypothesis.");
            researchNotes.Append(backedJournal, "student-branch", "11",
                "receiver", "Latest current learner measurement.");
            Check(System.IO.File.Exists(backedJournal + ".bak"),
                "Regression fixture lacks an actually committed prior journal backup.");
            System.IO.File.WriteAllText(backedJournal,
                "PLV-NOTEBOOK-V1\\nCORRUPTED-CURRENT-PRIMARY\\n");
            var corruptBackedReview = ResearchLearningReview.ReadRecordedJournal(
                researchNotes, backedJournal, "student-branch",
                "student-branch", "11", false);
            var corruptBackedNotebook = ResearchLearningReview.ReadActiveNotebookSummary(
                researchNotes, backedJournal, "student-branch", "11");
            Check(corruptBackedReview.Contains("Local notebook unavailable",
                    StringComparison.Ordinal) &&
                  corruptBackedNotebook.Contains("Research notebook unavailable",
                    StringComparison.Ordinal) &&
                  !corruptBackedReview.Contains("Old backup-only", StringComparison.Ordinal) &&
                  !corruptBackedNotebook.Contains("Old backup-only", StringComparison.Ordinal),
                "Corrupt current Notebook silently surfaced a stale .bak as current learner evidence.");
                        var corruptNotebook = ResearchLearningReview.ReadActiveNotebookSummary(
                researchNotes, damagedJournalPath, "student-branch", "7");
            Check(corruptNotebook.Contains("Research notebook unavailable", StringComparison.Ordinal) &&
                  !corruptNotebook.Contains("no recorded annotations", StringComparison.Ordinal) &&
                  !corruptNotebook.Contains("Private unverified", StringComparison.Ordinal),
                "Corrupted offline Notebook misleadingly appeared empty or leaked unverified notes.");
            Check(damagedReview.Contains("Local notebook unavailable", StringComparison.Ordinal) &&
                  !damagedReview.Contains("Private unverified", StringComparison.Ordinal),
                "Damaged Notebook interrupted offline Review or leaked unverified notes.");
                        // When the pinned scientific model is unavailable, the confirmed
            // saved Native review still permits bounded, explicitly UNVERIFIED
            // learner annotations from the matching offline sidecar.
            var recordedNativePath = System.IO.Path.Combine(
                learningDirectory, "study.plv.json");
            Check(ResearchLearningReview.SelectNotebookScope(
                "student-branch", "student-branch", true, true) ==
                    RecordedNotebookScope.RecordedOnly &&
                  ResearchLearningReview.SelectNotebookScope(
                "student-branch", "other-live-branch", true, true) ==
                    RecordedNotebookScope.RecordedOnly &&
                  ResearchLearningReview.SelectNotebookScope(
                "student-branch", "student-branch", true, false) ==
                    RecordedNotebookScope.Live &&
                  ResearchLearningReview.SelectNotebookScope(
                "student-branch", "other-live-branch", true, false) ==
                    RecordedNotebookScope.Unavailable &&
                  ResearchLearningReview.SelectNotebookScope(
                "student-branch", null, false, false) ==
                    RecordedNotebookScope.Unavailable,
                "A confirmed recorded-only source must win even when active and saved Native branches share an ID.");
                        // Saved Review may have a different filename from the currently
            // active UI saveName. The authoritative sidecar must be resolved
            // from the file that actually supplied RecordedSessionReview.
            Check(ResearchLearningReview.SidecarForSavedNative(recordedNativePath) ==
                    learningPath &&
                  ResearchLearningReview.ReadRecordedOnlyJournal(
                    researchNotes, recordedNativePath,
                    ResearchLearningReview.SidecarForSavedNative(recordedNativePath),
                    "student-branch", "7", false)
                    .Contains("Student hypothesis", StringComparison.Ordinal),
                "Model-unavailable Review did not resolve its annotations from the saved file.");
            var invalidSavedReviewPathRejected = false;
            try
            {
                ResearchLearningReview.SidecarForSavedNative(
                    System.IO.Path.Combine(learningDirectory, "active-other-session.plv-notes"));
            }
            catch (System.IO.InvalidDataException) { invalidSavedReviewPathRejected = true; }
            Check(invalidSavedReviewPathRejected,
                "A non-Native filename became a recorded-only learner sidecar.");
                        // Production Unity adapters are not compiled by the headless
            // suite. Pin their saved-source routing in the contract so a
            // future edit cannot silently revert to the active saveName.
            var recordedOnlyController = System.IO.File.ReadAllText(
                "unity/Assets/PharmaLabVR/Runtime/Session/SessionController.cs");
            Check(recordedOnlyController.Contains(
                    "ReadRecordedOnlySavedAnnotations(", StringComparison.Ordinal) &&
                  recordedOnlyController.Split(
                    "ResearchLearningReview.SidecarForSavedNative(recordedOnlyReviewSavePath)")
                    .Length >= 3,
                "Unity recorded-only Review/CSV route drifted back to an unrelated active notebook.");
                        var recordedOnlyAtSeven = ResearchLearningReview.ReadRecordedOnlyJournal(
                researchNotes, recordedNativePath, learningPath,
                "student-branch", "7", false);
            Check(recordedOnlyAtSeven.Contains("Recorded-only", StringComparison.Ordinal) &&
                  recordedOnlyAtSeven.Contains("Student hypothesis", StringComparison.Ordinal) &&
                  !recordedOnlyAtSeven.Contains("Native pH 6.20", StringComparison.Ordinal) &&
                  !recordedOnlyAtSeven.Contains("Private unrelated", StringComparison.Ordinal),
                "Saved model-unavailable Review lost bounded student notes or leaked future evidence.");
            // Recorded-only Review must also export the same constrained
            // student annotation CSV without a live Native model. Scientific
            // and student file identities remain distinct; no future notes.
            var recordedOnlyEntries = ResearchLearningReview.ReadRecordedOnlyEntries(
                researchNotes, recordedNativePath, learningPath,
                "student-branch", "7");
            var recordedOnlyCsv = ResearchLearningReview.RenderCsv(
                recordedOnlyEntries, "student-branch");
            Check(recordedOnlyEntries.Count == 1 &&
                  recordedOnlyCsv.Contains("studentAnnotation", StringComparison.Ordinal) &&
                  recordedOnlyCsv.Contains("Student hypothesis", StringComparison.Ordinal) &&
                  !recordedOnlyCsv.Contains("Native pH 6.20", StringComparison.Ordinal) &&
                  !recordedOnlyCsv.Contains("Private unrelated", StringComparison.Ordinal),
                "Saved model-unavailable Review could not export only recorded, event-bounded learner annotations.");
            var wrongExportSidecarRejected = false;
            try
            {
                ResearchLearningReview.ReadRecordedOnlyEntries(researchNotes,
                    recordedNativePath,
                    System.IO.Path.Combine(learningDirectory, "other.plv-notes"),
                    "student-branch", "11");
            }
            catch (System.IO.InvalidDataException) { wrongExportSidecarRejected = true; }
            Check(wrongExportSidecarRejected,
                "Student CSV export paired a different sidecar with saved Native evidence.");
                        var mismatchedSidecar = ResearchLearningReview.ReadRecordedOnlyJournal(
                researchNotes, recordedNativePath,
                System.IO.Path.Combine(learningDirectory, "other.plv-notes"),
                "student-branch", "11", false);
            Check(mismatchedSidecar.Contains("unavailable", StringComparison.Ordinal) &&
                  !mismatchedSidecar.Contains("Native pH 6.20", StringComparison.Ordinal),
                "Recorded-only fallback attached a different student's notebook path.");
            var invalidRecordedCeiling = ResearchLearningReview.ReadRecordedOnlyJournal(
                researchNotes, recordedNativePath, learningPath,
                "student-branch", "011", false);
            Check(invalidRecordedCeiling.Contains("unavailable", StringComparison.Ordinal),
                "Recorded-only fallback accepted an invalid saved Native event identity.");
                        var historical = researchNotes.Read(learningPath, "student-branch", "7");
            Check(historical.Count == 1 && historical[0].EventSequence == "7" &&
                  !historical[0].Text.Contains("Native pH 6.20"),
                "Recorded checkpoint at event 7 exposed later event-11 learner evidence.");
            Check(researchNotes.Read(learningPath, "student-branch", "11").Count == 2 &&
                  researchNotes.Read(learningPath, "different-tenant-branch", "7").Count == 0,
                "Bounded Notebook read dropped eligible notes or included the wrong branch.");
            var malformedReadDenied = false;
            try { researchNotes.Read(learningPath, "student-branch", "07"); }
            catch (ArgumentException) { malformedReadDenied = true; }
            Check(malformedReadDenied,
                "Noncanonical Native event ceiling was accepted for saved learner evidence.");
            var notes = researchNotes.Read(learningPath, "student-branch");
            // A portable learner archive is untrusted offline evidence, not
            // a server attestation. Teaching preview must independently parse
            // the exact checksummed journal, reject other branch or future
            // event records, and never silently recover corrupt snapshots.
            var portableNotes = researchNotes.ExportBranch(
                learningPath, "student-branch", "11");
            var parsedNotes = NotebookService.ReadPortableBranch(
                portableNotes, "student-branch", "11");
            Check(parsedNotes.Count == 2 &&
                  parsedNotes.All(entry => entry.BranchId == "student-branch"),
                "Instructor offline preview did not preserve the exported student event journal.");
            var crossBranchDenied = false;
            try {
                NotebookService.ReadPortableBranch(
                    portableNotes, "different-tenant-branch", "11");
            } catch (System.IO.InvalidDataException) { crossBranchDenied = true; }
            Check(crossBranchDenied, "Portable student notes crossed the recorded Native branch boundary.");
            var futureEventDenied = false;
            try {
                NotebookService.ReadPortableBranch(portableNotes, "student-branch", "7");
            } catch (System.IO.InvalidDataException) { futureEventDenied = true; }
            Check(futureEventDenied, "Instructor imported student notes after the purported Native snapshot.");
            var damagedDenied = false;
            try {
                NotebookService.ReadPortableBranch(
                    portableNotes.Replace("PLV-NOTEBOOK-V1", "PLV-NOTEBOOK-V2"),
                    "student-branch", "11");
            } catch (System.IO.InvalidDataException) { damagedDenied = true; }
            Check(damagedDenied, "Instructor imported a corrupted portable notebook header.");
            // Reopening Measure without a new Native event must not append
            // the same receiver pH evidence again. New events and unrelated
            // branches remain independently recordable.
            researchNotes.Append(learningPath, "idempotent-branch", "7", "receiver",
                nativeNote);
            var onceRecorded = researchNotes.Read(learningPath, "idempotent-branch");
            Check(NativeMeasurementNote.WasRecorded(onceRecorded,
                    "idempotent-branch", "7", "receiver", "3") &&
                  !NativeMeasurementNote.WasRecorded(onceRecorded,
                    "other-branch", "7", "receiver", "3") &&
                  !NativeMeasurementNote.WasRecorded(onceRecorded,
                    "idempotent-branch", "8", "receiver", "3") &&
                  !NativeMeasurementNote.WasRecorded(onceRecorded,
                    "idempotent-branch", "7", "receiver", "4") &&
                  !NativeMeasurementNote.WasRecorded(onceRecorded,
                    "idempotent-branch", "7", "source", "3"),
                "Previously recorded Native pH must block only its matching branch/event/vessel/revision.");
            Check(!NativeMeasurementNote.WasRecorded(notes, "student-branch", "7",
                    "receiver", "3"),
                "A student-authored prediction is not a genuine previously recorded Native observation.");
            // A learner's pH history contains only actual logged Native
            // observation notes. Never manufacture intermediate curve points.
            var historyNotes = researchNotes.Read(learningPath, "idempotent-branch");
            Check(ResearchNotebookPHHistory.Summarize(
                    historyNotes, "idempotent-branch", "receiver", 4)
                    .Contains("event 7") &&
                  ResearchNotebookPHHistory.Summarize(
                    historyNotes, "idempotent-branch", "receiver", 4)
                    .Contains("pH 7.13"),
                "Existing raw Native pH observation was absent from local event history.");
            var nextNote = nativeNote.Replace("Native pH 7.13", "Native pH 6.99")
                .Replace(" · event 7 ·", " · event 8 ·");
            researchNotes.Append(learningPath, "idempotent-branch", "8", "receiver", nextNote);
            researchNotes.Append(learningPath, "idempotent-branch", "9", "receiver",
                nextNote.Replace(" · event 8 ·", " · event 9 ·")
                    .Replace("raw Native pH 6.99", "raw Native pH NaN"));
            var eventTrend = ResearchNotebookPHHistory.Summarize(
                researchNotes.Read(learningPath, "idempotent-branch"),
                "idempotent-branch", "receiver", 4);
            Check(eventTrend.Contains("event 7") && eventTrend.Contains("event 8") &&
                  eventTrend.Contains("pH 6.99") && !eventTrend.Contains("event 9") &&
                  eventTrend.IndexOf("event 7", StringComparison.Ordinal) <
                  eventTrend.IndexOf("event 8", StringComparison.Ordinal),
                "Native pH trend fabricated a point, lost event order or accepted invalid raw data.");
            var compactTrend = ResearchNotebookPHHistory.Summarize(
                researchNotes.Read(learningPath, "idempotent-branch"),
                "idempotent-branch", "receiver", 1);
            Check(!compactTrend.Contains("event 7") && compactTrend.Contains("event 8"),
                "XR compact pH history must show the latest recorded Native event.");
            Check(ResearchNotebookPHHistory.Summarize(
                notes, "student-branch", "receiver", 4).Contains("No recorded Native pH"),
                "Learner predictions must not be confused with raw Native measurements.");
            var recordedTrendReview = ResearchLearningReview.Render(
                researchNotes.Read(learningPath, "idempotent-branch"),
                "idempotent-branch", false);
            Check(recordedTrendReview.Contains("Local recorded Native pH by event") &&
                  recordedTrendReview.Contains("event 8") &&
                  recordedTrendReview.Contains("not a certified titration curve"),
                "Recorded Desktop Review must surface the actual stored Native pH event history.");
            var result = ResearchLearningReview.Render(notes, "student-branch", false);
            Check(result.Contains("2 student annotations") &&
                  result.Contains("Student hypothesis") && result.Contains("Native pH 6.20") &&
                  result.Contains("event 7") && result.Contains("event 11") &&
                  !result.Contains("unrelated"),
                "Recorded review must render durable branch-linked student findings without cross-branch disclosure.");
            var compact = ResearchLearningReview.Render(notes, "student-branch", true);
            Check(compact.Length < 320 && compact.Contains("6.20") &&
                  compact.Contains("Student hypothesis"),
                "XR recorded review must expose readable learner findings.");
            var studentPage0 = ResearchLearningReview.RenderPage(
                notes, "student-branch", 0);
            var studentPage1 = ResearchLearningReview.RenderPage(
                notes, "student-branch", 1);
            Check(studentPage0.Contains("1/2") && studentPage0.Contains("Student hypothesis") &&
                  !studentPage0.Contains("Native pH 6.20") &&
                  studentPage1.Contains("2/2") && studentPage1.Contains("Native pH 6.20") &&
                  !studentPage1.Contains("Student hypothesis"),
                "XR teacher cannot inspect each actual imported student finding individually.");
            Check(ResearchLearningReview.RenderPage(notes, "student-branch", 5)
                    == studentPage1 &&
                  ResearchLearningReview.RenderPage(notes, "student-branch", -1)
                    == studentPage0,
                "Out-of-range annotation paging did not remain bounded.");
            Check(!ResearchLearningReview.RenderPage(notes, "different-tenant-branch", 0)
                    .Contains("Native pH 6.20") &&
                  !ResearchLearningReview.RenderPage(notes, "different-tenant-branch", 0)
                    .Contains("Student hypothesis") &&
                  studentPage0.Length < 180 && studentPage1.Length < 180,
                "XR instructor note paging crossed Native branches or overflowed its panel.");
            var isolated = ResearchLearningReview.Render(notes, "different-tenant-branch", false);
            Check(!isolated.Contains("6.20") && !isolated.Contains("hypothesis"),
                "Other Native branch review must not inherit this student's annotations.");
            Check(ResearchLearningReview.Render(Array.Empty<NotebookEntry>(),
                "student-branch", false).Contains("No student annotations"),
                "Empty notebook must not imply measured or completed work.");
            var studentCsv = ResearchLearningReview.RenderCsv(notes, "student-branch");
            Check(studentCsv.Contains("studentAnnotation") &&
                  studentCsv.Contains("Native pH 6.20") &&
                  studentCsv.Contains("Student hypothesis") &&
                  !studentCsv.Contains("different-tenant-branch"),
                "Exported learner CSV leaked another branch or lost actual notebook results.");
            var csvPath = System.IO.Path.Combine(learningDirectory, "learner-results.csv");
            ResearchLearningReview.WriteCsv(csvPath, notes, "student-branch");
            Check(System.IO.File.ReadAllText(csvPath) == studentCsv,
                "Learner results CSV did not persist to offline disk.");
            researchNotes.Append(learningPath, "formula-branch", "15", "receiver",
                "=HYPERLINK(\"https://invalid.example\",\"x\")");
            var safeCsv = ResearchLearningReview.RenderCsv(
                researchNotes.Read(learningPath, "formula-branch"), "formula-branch");
            Check(safeCsv.Contains("\"'=HYPERLINK") && !safeCsv.Contains("student-branch"),
                "Untrusted student annotation was exported as a spreadsheet formula.");
            // Two local Notebook writers must never silently replace one
            // another's saved learner evidence, and a stale predictable
            // .tmp file from a crash must not be truncated by a new append.
            var oldJournal = System.IO.File.ReadAllText(learningPath);
            var writerConflictRefused = false;
            using (var externalLease = new System.IO.FileStream(
                learningPath + ".lock", System.IO.FileMode.OpenOrCreate,
                System.IO.FileAccess.ReadWrite, System.IO.FileShare.None))
            {
                try {
                    researchNotes.Append(learningPath, "student-branch", "12", "receiver",
                        "This write must be rejected while another process holds the journal lease.");
                }
                catch (System.IO.InvalidDataException) { writerConflictRefused = true; }
            }
            Check(writerConflictRefused &&
                  System.IO.File.ReadAllText(learningPath) == oldJournal,
                "A competing Notebook writer replaced another writer's durable journal.");
            var staleStage = learningPath + ".tmp";
            System.IO.File.WriteAllText(staleStage, "PREVIOUS-CRASH-ARTIFACT");
            researchNotes.Append(learningPath, "staging-branch", "19", "receiver",
                "Actual new student annotation committed via a fresh stage.");
            Check(System.IO.File.ReadAllText(staleStage) == "PREVIOUS-CRASH-ARTIFACT" &&
                  researchNotes.Read(learningPath, "staging-branch").Count == 1,
                "New Notebook append clobbered a crash artifact or lost the committed annotation.");
        }
        finally
        {
            if (System.IO.Directory.Exists(learningDirectory))
                System.IO.Directory.Delete(learningDirectory, true);
        }

        // M3 reopen: an existing branch's durable first hypothesis must
        // reappear on lesson reselection, without permitting a second answer.
        var reopenedNotebook = new NotebookService();
        var reopenedFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plv-hypothesis-reopen-" + Guid.NewGuid().ToString("N"));
        var reopenedPath = System.IO.Path.Combine(reopenedFolder, "study.plv-notes");
        try
        {
            reopenedNotebook.Append(reopenedPath, "local-branch-a", "5", "receiver",
                "Student hypothesis for strong-acid-titration-research: receiver pH predicted below 7. " +
                "This is a learner prediction, not a Native result or certified endpoint.");
            reopenedNotebook.Append(reopenedPath, "local-branch-b", "6", "receiver",
                "Student hypothesis for strong-acid-titration-research: receiver pH predicted at least 7. " +
                "This is a learner prediction, not a Native result or certified endpoint.");
            var recorded = reopenedNotebook.Read(reopenedPath, "local-branch-a");
            Check(ResearchLessonJourney.RecoverSavedPrediction(
                    recorded, "strong-acid-titration-research", "local-branch-a", "7") == "below-7",
                "Lesson reselection did not recover its previously durable learner prediction.");
            Check(ResearchLessonJourney.RecoverSavedPrediction(
                    recorded, "mixed-acid-titration-research", "local-branch-a", "7") == null &&
                  ResearchLessonJourney.RecoverSavedPrediction(
                    recorded, "strong-acid-titration-research", "local-branch-b", "7") == null &&
                  ResearchLessonJourney.RecoverSavedPrediction(
                    Array.Empty<NotebookEntry>(), "strong-acid-titration-research", "local-branch-a", "7") == null,
                "A different lesson, branch or empty notebook inherited a false prediction.");
        }
        finally
        {
            if (System.IO.Directory.Exists(reopenedFolder))
                System.IO.Directory.Delete(reopenedFolder, true);
        }

        // M2/M3: all active XR menu buttons need distinct ray-hit rows.
        // Previously the reserve-stock action overlapped Strong acid lesson,
        // and concentration overlapped Mixed acid lesson on the same page.
        foreach (var preparation in new[] { true, false })
        foreach (var reserveReady in new[] { true, false })
        foreach (var canPredict in new[] { true, false })
        foreach (var page in new[] { 1, 2, 3, 4 })
        {
            var pageItems = ResearchInstrumentMenuLayout.Visible(
                page, preparation, !preparation, reserveReady,
                !preparation, canPredict, !preparation, reserveReady);
            Check(pageItems.Select(item => item.Row).Distinct().Count() == pageItems.Count,
                "XR tool/menu hit targets overlap for page " + page);
            Check(pageItems.All(item => item.Row >= 0 && item.Row <= 6),
                "XR instrument navigation placed a target outside the bounded panel.");
        }
        Check(ResearchInstrumentMenuLayout.ShowGlobalSettings(0) &&
              !ResearchInstrumentMenuLayout.ShowGlobalSettings(1) &&
              !ResearchInstrumentMenuLayout.ShowGlobalSettings(2) &&
              !ResearchInstrumentMenuLayout.ShowGlobalSettings(4),
            "XR setup Settings must not overlap the instrument navigation row.");
        var firstSetup = ResearchInstrumentMenuLayout.Visible(
            2, true, false, false, false, false, false, false);
        Check(firstSetup.Any(item => item.Name == "Choose concentration") &&
              firstSetup.Any(item => item.Name == "More tools and lessons") &&
              !firstSetup.Any(item => item.Name == "Strong acid lesson"),
            "Stock preparation must have its own uncluttered XR page.");
        var firstExtra = ResearchInstrumentMenuLayout.Visible(
            4, true, false, false, false, true, false, false);
        Check(firstExtra.Any(item => item.Name == "Prepare reserve stock") &&
              firstExtra.Any(item => item.Name == "Strong acid lesson") &&
              firstExtra.Any(item => item.Name == "Mixed acid lesson"),
            "Prepared reserve and both lesson choices must be reachable without overlap.");
        var readyExtra = ResearchInstrumentMenuLayout.Visible(
            4, false, true, true, true, false, true, true);
        Check(readyExtra.Any(item => item.Name == "Switch pipette to reserve") &&
              readyExtra.Any(item => item.Name == "Refill bottle 0.01 ml") &&
              readyExtra.Any(item => item.Name == "Prime burette 0.05 ml") &&
              !readyExtra.Any(item => item.Name == "Prepare reserve stock"),
            "Native-ready reserve/burette operations must not conflict with setup-only actions.");
        var returnToPrimary = ResearchInstrumentMenuLayout.Visible(
            4, false, true, true, true, false, true, true, false, true);
        Check(returnToPrimary.Any(item => item.Name == "Switch pipette to source" && item.Row == 0) &&
              !returnToPrimary.Any(item => item.Name == "Switch pipette to reserve") &&
              returnToPrimary.Select(item => item.Row).Distinct().Count() == returnToPrimary.Count,
            "XR must offer accessible Native source return after reserve without overlapping page controls.");
        var physicalReturnSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Session/SessionController.cs");
        Check(physicalReturnSource.Contains("ActivePipetteStockInventoryId") &&
              physicalReturnSource.Contains("TryPipetteSwitchPrimary"),
            "Desktop and XR must expose current Native pipette source and an explicit way back.");

        var notebookChoice = ResearchInstrumentMenuLayout.Visible(
            3, false, true, false, false, true, false, false, true);
        Check(notebookChoice.Any(item => item.Name == "Open notebook" && item.Row == 2) &&
              notebookChoice.Select(item => item.Row).Distinct().Count() == notebookChoice.Count,
            "VR student lesson view must open the actual saved Notebook without overlapping predictions.");
        var unavailableNotebook = ResearchInstrumentMenuLayout.Visible(
            3, true, false, false, false, true, false, false, false);
        Check(!unavailableNotebook.Any(item => item.Name == "Open notebook"),
            "Startup scene cannot open a notebook before a supported Native lab state.");
        var choice = ResearchInstrumentMenuLayout.Visible(
            3, false, true, false, false, true, false, false);
        Check(choice.Any(item => item.Name == "Predict pH below 7") &&
              choice.Any(item => item.Name == "Predict pH at least 7"),
            "Lesson selection must lead to both pre-transfer prediction options.");

        // Offline teaching preview is a read-only REVIEW action. It is
        // neither a Native command nor permission to grade or upload.
        var teachingRouter = new ScreenRouter();
        teachingRouter.Configure(new CoreDriver());
        teachingRouter.Show(ScreenId.Review);
        var teachingSessions = new SessionController();
        var teachingPanels = new LabPanelController();
        teachingPanels.Configure(teachingRouter, new CoreDriver(), teachingSessions);
        Check(teachingRouter.Current.SupportedActions.Contains("PreviewPortableArchive") &&
              teachingRouter.Current.SupportedActions.Contains("ChoosePortableArchive") &&
              teachingRouter.Current.SupportedActions.Contains("NextInstructorAnnotation") &&
              teachingPanels.Request(PanelAction.NextInstructorAnnotation) &&
              teachingSessions.NextImportedNoteCalls == 1 &&
              teachingPanels.Request(PanelAction.ChoosePortableArchive) &&
              teachingSessions.PortableChooseCalls == 1 &&
              teachingPanels.Request(PanelAction.PreviewPortableArchive, "submitted.plvpack") &&
              teachingSessions.PortablePreviewCalls == 1,
            "Recorded Review does not route to the offline teaching preview.");
        teachingRouter.Show(ScreenId.Lab);
        Check(!teachingPanels.Request(PanelAction.PreviewPortableArchive, "submitted.plvpack") &&
              !teachingPanels.Request(PanelAction.ChoosePortableArchive) &&
              teachingSessions.PortablePreviewCalls == 1 &&
              teachingSessions.PortableChooseCalls == 1 &&
              !teachingPanels.Request(PanelAction.NextInstructorAnnotation) &&
              teachingSessions.NextImportedNoteCalls == 1,
            "Live Lab controls admitted a portable teaching preview action.");

        // P23: imported local study packs must be selectable from XR without
        // exposing parent directories or unrelated files.
        var packFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pharmalabvr-safe-study-packs");
        var choices = OfflineArchivePicker.Names(packFolder, new[] {
            System.IO.Path.Combine(packFolder, "B.plvpack"),
            System.IO.Path.Combine(packFolder, "A.plvpack"),
            System.IO.Path.Combine(packFolder, "A.plvpack"),
            System.IO.Path.Combine(packFolder, ".hidden.plvpack"),
            System.IO.Path.Combine(packFolder, "unrelated.txt"),
            System.IO.Path.Combine(packFolder, "..", "foreign.plvpack")
        });
        Check(choices.Count == 2 && choices[0] == "A.plvpack" && choices[1] == "B.plvpack",
            "Offline teaching picker did not restrict itself to distinct canonical session filenames.");
        // Each student export must be an independently selectable,
        // bounded archive. Exporting new Notebook evidence at the same Native
        // material event cannot silently replace the original student pack.
        // Newly exported evidence must remain selectable even after the
        // app has accumulated more research bundles than its 64-card picker
        // limit; sort by local file recency, never arbitrary directory order.
        var recentFolder = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plv-recent-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(recentFolder);
        try
        {
            var oldPack = System.IO.Path.Combine(recentFolder, "older.plvpack");
            var midPack = System.IO.Path.Combine(recentFolder, "middle.plvpack");
            var newPack = System.IO.Path.Combine(recentFolder, "newest.plvpack");
            foreach (var file in new[] { oldPack, midPack, newPack })
                System.IO.File.WriteAllText(file, "test-pack");
            var t = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            System.IO.File.SetLastWriteTimeUtc(oldPack, t.AddMinutes(-5));
            System.IO.File.SetLastWriteTimeUtc(midPack, t.AddMinutes(-2));
            System.IO.File.SetLastWriteTimeUtc(newPack, t);
            var result = OfflineArchivePicker.NamesNewest(recentFolder,
                new[] { oldPack, newPack, midPack, oldPack,
                    System.IO.Path.Combine(recentFolder, "..", "outside.plvpack") }, 2);
            Check(result.Count == 2 && result[0] == "newest.plvpack" &&
                  result[1] == "middle.plvpack",
                "Recent exported Native/Notebook packs are hidden behind older alphabetical bundles.");
        }
        finally { System.IO.Directory.Delete(recentFolder, recursive: true); }
        var firstName = OfflineArchivePicker.NewStudentExportName(
            "student-research", "8", "0123456789abcdef0123456789abcdef");
        var secondName = OfflineArchivePicker.NewStudentExportName(
            "student-research", "8", "fedcba9876543210fedcba9876543210");
        Check(firstName != secondName &&
              OfflineArchivePicker.ValidName(firstName) &&
              OfflineArchivePicker.ValidName(secondName) &&
              firstName.Contains("-e8-", StringComparison.Ordinal) &&
              !firstName.Contains("/", StringComparison.Ordinal),
            "Portable student export may overwrite previous event-bounded evidence.");
        Check(OfflineArchivePicker.ValidName(
            OfflineArchivePicker.NewStudentExportName(
                "overlong-study-" + new string('x', 120), "18446744073709551615",
                "0123456789abcdef0123456789abcdef")),
            "Long student labels must never produce an invalid/non-discoverable archive.");
        var unsafeRevision = false;
        try { OfflineArchivePicker.NewStudentExportName(
            "study", "../8", "0123456789abcdef0123456789abcdef"); }
        catch (ArgumentException) { unsafeRevision = true; }
        Check(unsafeRevision, "Unsafe Native event identities must be rejected for portable export.");

        Check(OfflineArchivePicker.NextName(choices, "A.plvpack") == "B.plvpack" &&
              OfflineArchivePicker.NextName(choices, "B.plvpack") == "A.plvpack" &&
              OfflineArchivePicker.NextName(choices, "missing.plvpack") == "A.plvpack",
            "XR selection does not cycle deterministically through bounded safe teaching archives.");

        // M3: genuine authored Research lesson content is delivered to a
        // branch-scoped, non-grading observer of real Native material state.
        var strongJson = System.Text.Json.JsonDocument.Parse(
            System.IO.File.ReadAllText("unity/Assets/PharmaLabVR/Content/Protocols/strong-acid-titration.json"));
        var mixedJson = System.Text.Json.JsonDocument.Parse(
            System.IO.File.ReadAllText("unity/Assets/PharmaLabVR/Content/Protocols/mixed-acid-titration.json"));
        Check(strongJson.RootElement.GetProperty("steps").GetArrayLength() >= 4 &&
              mixedJson.RootElement.GetProperty("steps").GetArrayLength() >= 4 &&
              !strongJson.RootElement.GetProperty("certifiedScoring").GetBoolean() &&
              !mixedJson.RootElement.GetProperty("certifiedScoring").GetBoolean(),
            "Two real ungraded lesson procedures are required for the student Lab.");
        var definition = new ProtocolDefinition
        {
            schemaVersion = 1, id = "strong-acid-titration-research",
            maturity = "Research", certifiedScoring = false,
            steps = new[] { "Prepare supported stock", "Deliver measured aliquot", "Inspect and review" }
        };
        var lesson = new ResearchLessonJourney(definition, "native-branch-1");
        var emptyLessonView = new LabViewModel();
        Check(lesson.Summary("native-branch-1", emptyLessonView, true, false).Contains("Pause"),
            "A paused Native lesson cannot present a completed or transferable procedure.");
        Check(lesson.Summary("other-branch", emptyLessonView, false, false).Contains("branch"),
            "A restored or forked Native session must invalidate previous lesson context.");
        emptyLessonView.Vessels[0].CommittedVolumeM3 = 2.5e-5;
        emptyLessonView.Vessels[1].CommittedVolumeM3 = 1e-8;
        Check(lesson.Summary("native-branch-1", emptyLessonView, false, false).Contains("pH"),
            "A real receiver material increment must direct learner to measured Native pH.");
        Check(lesson.Instructions.Contains("Inspect and review") &&
              lesson.Instructions.Contains("Prepare supported stock"),
            "Installed course instruction text is not shown to learners.");
        Check(!lesson.Summary("native-branch-1", emptyLessonView, false, false)
                .Contains("Certified"),
            "Research-only course incorrectly claimed certified performance.");

        // M1/M2 first-use student bench: this text is derived only from the
        // Native vessel view and cannot turn a missing or held state into success.
        var bench = new LabViewModel();
        bench.Vessels[0].CommittedVolumeM3 = 2.5e-5;
        bench.Vessels[0].MaterialRevision = "1";
        bench.Vessels[1].CommittedVolumeM3 = 0;
        bench.Vessels[1].MaterialRevision = "0";
        var readout = LabBenchReadout.Inventory(bench);
        Check(readout.Contains("Stock bottle: 25.000 mL (Native rev 1)") &&
              readout.Contains("Receiver: 0.000 mL (Native rev 0)"),
            "The first-use bench failed to display real Native material volumes.");
        Check(LabBenchReadout.NextAction(bench, false, false, false).Contains("Transfer"),
            "The empty receiver must offer a supported Native transfer.");
        bench.Vessels[0].CommittedVolumeM3 -= 1e-8;
        bench.Vessels[1].CommittedVolumeM3 += 1e-8;
        Check(LabBenchReadout.Inventory(bench).Contains("Receiver: 0.010 mL"),
            "Accepted transfers must change the live Native receiver readout.");
        Check(LabBenchReadout.NextAction(bench, false, false, false).Contains("Inspect"),
            "Receiver inventory must offer actual inspection, not a fabricated scientific result.");
        Check(LabBenchReadout.NextAction(bench, false, false, true).Contains("Hold") &&
              LabBenchReadout.NextAction(bench, false, true, false).Contains("Pause"),
            "An interrupted or paused lab cannot recommend transferring material.");
        Check(LabBenchReadout.NextAction(bench, true, false, false).Contains("Prepare"),
            "Empty Native setup should direct the learner to stock preparation.");
        Check(LabBenchReadout.Inventory(null).Contains("unavailable"),
            "A missing Native session was represented as a real filled vessel.");
        bench.Vessels[1].CommittedVolumeM3 = double.NaN;
        Check(LabBenchReadout.Inventory(bench).Contains("Receiver: unavailable"),
            "Invalid Native quantity must never display as a real receiver measurement.");

        // Priming a burette is a bounded Native TransferFixed between real vessels.
        var burettePrime = ResearchInstrumentPayloads.RefillFromPreparedStock(
            "source", "burette", "spill", 5e-8);
        Check(burettePrime.Contains("\"sourceInventoryId\":\"source\"") &&
              burettePrime.Contains("\"destinationInventoryId\":\"burette\"") &&
              burettePrime.Contains("\"value\":5E-08"),
            "Native burette prime must debit source and credit a distinct vessel.");

        bench.Vessels[1].CommittedVolumeM3 = 0;
        bench.Vessels.Add(new VesselView { Id = "burette", MaterialRevision = "1",
            CapacityM3 = 5e-5, CommittedVolumeM3 = 5e-8 });
        Check(LabBenchReadout.NextAction(bench, false, false, false).Contains("Burette primed"),
            "The live guided procedure did not surface the primed physical stopcock path.");
        Check(LabBenchReadout.Inventory(bench).Contains("Burette: 0.050 mL"),
            "The primed burette is not visible in the Native inventory readout.");

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
        // P10.01 Desktop exact-control contract: scroll over the lab
        // manipulates ONLY the held research tool, otherwise camera zoom.
        Check(DesktopWheelRouting.Resolve(true, false, 120.0) == DesktopWheelTarget.HeldToolHeight,
            "Held tool scroll must alter tool height instead of camera zoom");
        Check(DesktopWheelRouting.Resolve(false, false, 120.0) == DesktopWheelTarget.CameraZoom,
            "Empty-hand scroll must zoom without moving any vessel");
        Check(DesktopWheelRouting.Resolve(true, true, 120.0) == DesktopWheelTarget.None &&
              DesktopWheelRouting.Resolve(false, true, 120.0) == DesktopWheelTarget.None,
            "Scroll over a UI ray target must change neither tool nor camera");
        Check(DesktopWheelRouting.Resolve(true, false, double.NaN) == DesktopWheelTarget.None &&
              DesktopWheelRouting.Resolve(false, false, 0.0) == DesktopWheelTarget.None,
            "Invalid/zero wheel delta must be inert");
        Check(Math.Abs(DesktopWheelRouting.HeightDeltaMetres(120.0) - 0.02) < 1e-10 &&
              Math.Abs(DesktopWheelRouting.HeightDeltaMetres(-120.0) + 0.02) < 1e-10,
            "Held scroll must change physical height by a bounded 2 cm notch");
        Check(Math.Abs(DesktopWheelRouting.ZoomFieldOfViewDegrees(60.0, 120.0) - 55.0) < 1e-10,
            "Empty wheel forward must zoom camera by changing FOV, never physics");
        Check(DesktopWheelRouting.ZoomFieldOfViewDegrees(35.0, 120.0) == 35.0 &&
              DesktopWheelRouting.ZoomFieldOfViewDegrees(85.0, -120.0) == 85.0,
            "Camera FOV must be bounded 35 to 85 degrees");
        Check(DesktopWheelRouting.HeightDeltaMetres(100000.0) == 0.02 &&
              DesktopWheelRouting.HeightDeltaMetres(double.PositiveInfinity) == 0.0,
            "Spurious/malformed scroll spikes must not teleport a held instrument");
        // P10.01 valid Place: never release a held tool in midair, through
        // another object, outside the bench or dangerously tilted.
        Check(DesktopPlacementAdmission.CanPlace(0.04, 1.0, true, false),
            "Upright tool near the real bench should admit explicit F Place");
        Check(!DesktopPlacementAdmission.CanPlace(0.28, 1.0, true, false),
            "Midair F Place must keep the physical tool held");
        Check(!DesktopPlacementAdmission.CanPlace(0.04, 1.0, false, false),
            "Ray blocked by a vessel or non-bench surface must not place");
        Check(!DesktopPlacementAdmission.CanPlace(0.04, 0.3, true, false),
            "Horizontal tool must not snap into the bench");
        Check(!DesktopPlacementAdmission.CanPlace(0.04, 1.0, true, true),
            "Input focus on an overlay cannot release a held object");
        Check(!DesktopPlacementAdmission.CanPlace(double.NaN, 1.0, true, false) &&
              !DesktopPlacementAdmission.CanPlace(0.04, double.PositiveInfinity, true, false),
            "Invalid physical pose must fail closed before any Rigidbody release");
        // M2 Desktop receiver control: left click still only inspects Native pH;
        // F explicitly picks the physical receiver, and F releases it on bench.
        Check(DesktopReceiverRouting.Resolve(true, true, false) ==
              DesktopReceiverAction.Inspect &&
              DesktopReceiverRouting.Resolve(true, false, true) ==
              DesktopReceiverAction.Pick,
            "Click-to-inspect and F-to-reposition must not share receiver ownership.");
        Check(DesktopReceiverRouting.Resolve(false, false, true) ==
              DesktopReceiverAction.None &&
              DesktopReceiverRouting.Resolve(true, true, true) ==
              DesktopReceiverAction.None,
            "An unsupported or simultaneous receiver gesture must not pick it up.");
        Check(DesktopPlacementAdmission.CanPlaceReceiver(
                  0.045, 0.04, 1.0, true, false) &&
              !DesktopPlacementAdmission.CanPlaceReceiver(
                  0.035, 0.04, 1.0, true, false) &&
              !DesktopPlacementAdmission.CanPlaceReceiver(
                  0.35, 0.04, 1.0, true, false) &&
              !DesktopPlacementAdmission.CanPlaceReceiver(
                  0.045, 0.04, 0.1, true, false),
            "Receiver placement must use its physical bottom, not embed its center in the bench.");
                // P10.01 held-tool mouse plane drag has priority over left-drag valve;
        // both routes share exclusive ownership and obey HUD focus.
        // A momentary input event may occur between Native simulation ticks.
        // Capture the actual Unity-frame press, then consume exactly once on
        // the next admissible Native sample, never after interruption.
        var bufferedPress = new ResearchPressBuffer();
        Check(!bufferedPress.TryConsume(12, "research-stock-bottle-visual", true),
            "An unpressed physical instrument issued a Native action.");
        bufferedPress.Capture("research-stock-bottle-visual", 14, true);
        Check(bufferedPress.TryConsume(18, "research-stock-bottle-visual", true) &&
              !bufferedPress.TryConsume(18, "research-stock-bottle-visual", true),
            "A between-tick press was lost or replayed more than once.");
        bufferedPress.Capture("research-pipette-visual", 20, true);
        Check(!bufferedPress.TryConsume(22, "research-stock-bottle-visual", true) &&
              !bufferedPress.TryConsume(23, "research-pipette-visual", true),
            "Changed tool ownership replayed a prior press.");
        bufferedPress.Capture("research-pipette-visual", 30, true);
        Check(!bufferedPress.TryConsume(31, "research-pipette-visual", false) &&
              !bufferedPress.TryConsume(32, "research-pipette-visual", true),
            "Native Hold/Pause left a queued transfer command armed.");
        bufferedPress.Capture("research-pipette-visual", 40, true);
        Check(!bufferedPress.TryConsume(60, "research-pipette-visual", true),
            "An old gesture survived beyond its finite input freshness window.");
        bufferedPress.Capture("research-stock-bottle-visual", 70, false);
        Check(!bufferedPress.TryConsume(71, "research-stock-bottle-visual", true),
            "Unadmitted input was stored for later transfer.");
        bufferedPress.Capture("research-stock-bottle-visual", 80, true);
        Check(!bufferedPress.TryConsume(81, "research-stock-bottle-visual", false) &&
              !bufferedPress.TryConsume(82, "research-stock-bottle-visual", true),
            "A UI-owned or blocked queued press was incorrectly revived later.");
        bufferedPress.Capture("research-stock-bottle-visual", 85, true);
        bufferedPress.Clear();
        Check(!bufferedPress.TryConsume(81, "research-stock-bottle-visual", true),
            "Focus loss or mode change failed to clear the pending physical gesture.");

        // Physical delivery must require a deliberate gesture over exactly
        // one receiver. It never substitutes tool pose for a Native receipt.
        Check(ResearchAuxiliaryDelivery.Resolve(
                "research-stock-bottle-visual", true, true, true, true)
                == ResearchAuxiliaryDeliveryIntent.BottlePour &&
              ResearchAuxiliaryDelivery.Resolve(
                "research-pipette-visual", true, true, true, true)
                == ResearchAuxiliaryDeliveryIntent.PipetteDispense,
            "Actual bottle and pipette over the receiver must request their Native transfer actions.");
        Check(ResearchAuxiliaryDelivery.Resolve(
                "research-stock-bottle-visual", true, false, true, true)
                == ResearchAuxiliaryDeliveryIntent.None &&
              ResearchAuxiliaryDelivery.Resolve(
                "research-pipette-visual", true, true, false, true)
                == ResearchAuxiliaryDeliveryIntent.None &&
              ResearchAuxiliaryDelivery.Resolve(
                "research-pipette-visual", true, true, true, false)
                == ResearchAuxiliaryDeliveryIntent.None &&
              ResearchAuxiliaryDelivery.Resolve(
                "research-tool", true, true, true, true)
                == ResearchAuxiliaryDeliveryIntent.None &&
              ResearchAuxiliaryDelivery.Resolve(
                "research-pipette-visual", false, true, true, true)
                == ResearchAuxiliaryDeliveryIntent.None,
            "No press, missing geometry, paused session, wrong tool or no grip must never deliver.");

        // M3: crash recovery must be wired to Unity's session lifecycle
        // as well as the next export, not exist only as a detached helper.
        var exportControllerSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Session/SessionController.cs");
        Check(exportControllerSource.Contains(
                "private bool RecoverInterruptedCsvForCurrentSession(", StringComparison.Ordinal) &&
              exportControllerSource.Contains(
                "RecordedCsvPairWriter.RecoverInterruptedPair(", StringComparison.Ordinal) &&
              exportControllerSource.Split(
                "RecoverInterruptedCsvForCurrentSession();").Length >= 3 &&
              exportControllerSource.Contains(
                "if (!RecoverInterruptedCsvForCurrentSession())",
                StringComparison.Ordinal),
            "Unity session initialization and CSV export do not reconcile interrupted paired outputs.");
                // Verify the gesture is actually routed by both input adapters,
        // not merely accepted by a detached presentation-only helper.
        var noteDesktopSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Input/DesktopInputAdapter.cs");
        var noteXrSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Input/XRInputAdapter.cs");
        Check(noteDesktopSource.Contains("TryRecordPhysicalReceiverPH()", StringComparison.Ordinal) &&
              noteDesktopSource.Contains("measurementGestureGate.ObserveNeutral(", StringComparison.Ordinal) &&
              noteDesktopSource.Contains("PanelAction.RecordCurrentPH", StringComparison.Ordinal) &&
              noteXrSource.Contains("TryRecordPhysicalReceiverPH()", StringComparison.Ordinal) &&
              noteXrSource.Contains("measurementGestureGate.ObserveNeutral(", StringComparison.Ordinal) &&
              noteXrSource.Contains("PanelAction.RecordCurrentPH", StringComparison.Ordinal),
            "Desktop/XR must use fresh physical receiver gestures for the real Notebook command.");

        // The same live physical grip authority must also guard an
        // otherwise geometrically valid Native pipette rinse.
        var physicalRinseSessionSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Session/SessionController.cs");
        var physicalSessionRinseBodyStart = physicalRinseSessionSource.IndexOf(
            "public bool TryPhysicalRinsePipette(", StringComparison.Ordinal);
        var physicalSessionRinseBodyEnd = physicalRinseSessionSource.IndexOf(
            "public bool TryPipetteWaterRinse()", StringComparison.Ordinal);
        Check(physicalSessionRinseBodyStart >= 0 &&
              physicalSessionRinseBodyEnd > physicalSessionRinseBodyStart &&
              physicalRinseSessionSource.Substring(physicalSessionRinseBodyStart,
                physicalSessionRinseBodyEnd - physicalSessionRinseBodyStart)
                .Contains("!PhysicallyOwnsPipette(physicalPipette)", StringComparison.Ordinal),
            "A public rinse endpoint must not consume Native water without physical pipette ownership.");

        // A held stock-bottle mouth dipped into real Native rinse water
        // requests cleaning only when no receiver/inlet owns its outlet.
        Check(ResearchPhysicalBottleRinseSelection.Resolve(
              "research-stock-bottle-visual", true, true, false, false,
              true, true, true) == ResearchAuxiliaryDeliveryIntent.BottleRinse,
            "Physical bottle-mouth immersion did not admit the Native rinse intent.");
        Check(ResearchPhysicalBottleRinseSelection.Resolve(
              "research-reserve-stock-bottle-visual", true, true, false, false,
              true, true, true) == ResearchAuxiliaryDeliveryIntent.None &&
              ResearchPhysicalBottleRinseSelection.Resolve(
              "research-stock-bottle-visual", false, true, false, false,
              true, true, true) == ResearchAuxiliaryDeliveryIntent.None &&
              ResearchPhysicalBottleRinseSelection.Resolve(
              "research-stock-bottle-visual", true, false, false, false,
              true, true, true) == ResearchAuxiliaryDeliveryIntent.None &&
              ResearchPhysicalBottleRinseSelection.Resolve(
              "research-stock-bottle-visual", true, true, true, false,
              true, true, true) == ResearchAuxiliaryDeliveryIntent.None &&
              ResearchPhysicalBottleRinseSelection.Resolve(
              "research-stock-bottle-visual", true, true, false, true,
              true, true, true) == ResearchAuxiliaryDeliveryIntent.None &&
              ResearchPhysicalBottleRinseSelection.Resolve(
              "research-stock-bottle-visual", true, true, false, false,
              false, true, true) == ResearchAuxiliaryDeliveryIntent.None &&
              ResearchPhysicalBottleRinseSelection.Resolve(
              "research-stock-bottle-visual", true, true, false, false,
              true, false, true) == ResearchAuxiliaryDeliveryIntent.None &&
              ResearchPhysicalBottleRinseSelection.Resolve(
              "research-stock-bottle-visual", true, true, false, false,
              true, true, false) == ResearchAuxiliaryDeliveryIntent.None,
            "Stock-bottle cleaning must refuse ambiguous, unprepared or unattended physical gestures.");

        // M2 first-use cleaning: RinseTool requires a Native tool
        // registration. Neither instrument may depend on a prior pour
        // or aspiration to establish its actual PlaceTool revision.
        foreach (var instrumentFile in new[] {
            "unity/Assets/PharmaLabVR/Runtime/Tools/PipetteActuator.cs",
            "unity/Assets/PharmaLabVR/Runtime/Tools/BottlePourActuator.cs"
        })
        {
            var instrumentSource = System.IO.File.ReadAllText(instrumentFile);
            var rinseAt = instrumentSource.LastIndexOf(
                "public LabCommandReceipt Rinse(double quantityM3)", StringComparison.Ordinal);
            Check(rinseAt >= 0 &&
                  instrumentSource.Substring(rinseAt).Contains(
                    "native.EnsurePlaced(toolId, sourceId, sinkId,", StringComparison.Ordinal) &&
                  instrumentSource.Substring(rinseAt).Contains(
                    "return native.Rinse(", StringComparison.Ordinal),
                "Cold-start Native rinse must register the physical instrument before RinseTool: " +
                instrumentFile);
        }

                // M2 source-only physical pipette selection is invoked from both
        // Desktop/XR adapters. The public SessionController must really
        // define it (not just a stub) and validate physical ownership again.
        var physicalPipetteSessionSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Session/SessionController.cs");
        Check(physicalPipetteSessionSource.Contains(
                "public bool TryPhysicalSelectPipetteSource(", StringComparison.Ordinal) &&
              physicalPipetteSessionSource.Contains(
                "ResearchPhysicalPipetteSelectionAdmission.MaySelect(", StringComparison.Ordinal) &&
              physicalPipetteSessionSource.Contains(
                "IsPipetteDirectlyGripped(", StringComparison.Ordinal) &&
              physicalPipetteSessionSource.Contains(
                "TryPipetteSwitchReserve()", StringComparison.Ordinal) &&
              physicalPipetteSessionSource.Contains(
                "TryPipetteSwitchPrimary()", StringComparison.Ordinal),
            "Native source-switch gesture is called by Desktop/XR but lacks its actual SessionController implementation.");
        Check(ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "reserve-stock", true, true, true, true, false) &&
              ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "source", true, true, true, true, false) &&
              !ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "rinse-water", true, true, true, true, false) &&
              !ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "source", false, true, true, true, false) &&
              !ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "source", true, false, true, true, false) &&
              !ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "source", true, true, false, true, false) &&
              !ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "source", true, true, true, false, false) &&
              !ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                "source", true, true, true, true, true),
            "Physical pipette source selection must refuse unsupported, unprepared, unheld, paused, dry or already selected stock.");

                // M3: a SECOND deliberate physical receiver contact on Measure must
        // persist only the current Native observation, never replay the first
        // Lab inspection gesture or create synthetic notebook evidence.
        Check(ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "receiver", true, true, true, true, true, true),
            "A fresh physically repeated receiver gesture must offer current Native pH recording.");
        Check(!ResearchPhysicalMeasurementRecording.CanRecord(
                "Lab", "receiver", true, true, true, true, true, true) &&
              !ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "source", true, true, true, true, true, true) &&
              !ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "receiver", false, true, true, true, true, true) &&
              !ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "receiver", true, false, true, true, true, true) &&
              !ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "receiver", true, true, false, true, true, true) &&
              !ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "receiver", true, true, true, false, true, true) &&
              !ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "receiver", true, true, true, true, false, true) &&
              !ResearchPhysicalMeasurementRecording.CanRecord(
                "Measure", "receiver", true, true, true, true, true, false),
            "No stale, duplicate, source, held-hand, UI-owned, paused or first-touch pH may be recorded.");

                // M2 physical bottle inspection: no invented pH or source switching.
        // Two close candidates cannot be resolved by collider traversal order.
        Check(ResearchPhysicalInspectionSelection.XrUniqueInventory(
                true, true, true, false, .025, double.PositiveInfinity, double.PositiveInfinity) == "receiver" &&
              ResearchPhysicalInspectionSelection.XrUniqueInventory(
                true, true, true, false, .20, .025, double.PositiveInfinity) == "source" &&
              ResearchPhysicalInspectionSelection.XrUniqueInventory(
                true, true, true, false, .20, .20, .025) == "reserve-stock",
            "XR empty-hand touch must select the exact Native vessel represented by physical geometry.");
        Check(ResearchPhysicalInspectionSelection.XrUniqueInventory(
                true, true, true, false, .025, .025, double.PositiveInfinity) == null &&
              ResearchPhysicalInspectionSelection.XrUniqueInventory(
                true, true, true, false, double.NaN, .025, .025) == null &&
              ResearchPhysicalInspectionSelection.XrUniqueInventory(
                true, true, true, false, .20, .20, .20) == null &&
              ResearchPhysicalInspectionSelection.XrUniqueInventory(
                true, true, false, false, .20, .025, .20) == null &&
              ResearchPhysicalInspectionSelection.XrUniqueInventory(
                false, true, true, false, .20, .025, .20) == null &&
              ResearchPhysicalInspectionSelection.XrUniqueInventory(
                true, false, true, false, .20, .025, .20) == null &&
              ResearchPhysicalInspectionSelection.XrUniqueInventory(
                true, true, true, true, .20, .025, .20) == null,
            "Overlapping, unknown, untracked, paused, held or UI-owned XR touches must fail closed.");
        Check(ResearchPhysicalInspectionSelection.DesktopBottleInventory(
                "source", true, true, true, true) == "source" &&
              ResearchPhysicalInspectionSelection.DesktopBottleInventory(
                "reserve-stock", true, true, true, true) == "reserve-stock" &&
              ResearchPhysicalInspectionSelection.DesktopBottleInventory(
                "rinse-water", true, true, true, true) == null &&
              ResearchPhysicalInspectionSelection.DesktopBottleInventory(
                "source", true, false, true, true) == null &&
              ResearchPhysicalInspectionSelection.DesktopBottleInventory(
                "source", false, true, true, true) == null &&
              ResearchPhysicalInspectionSelection.DesktopBottleInventory(
                "source", true, true, false, true) == null &&
              ResearchPhysicalInspectionSelection.DesktopBottleInventory(
                "source", true, true, true, false) == null,
            "Alt-click must inspect only a prepared real bottle, never pick or transfer on stale gestures.");

                // This headless suite cannot launch Unity input, but keeps both
        // actual scene routes wired to the policy exercised above.
        var stockInspectDesktop = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Input/DesktopInputAdapter.cs");
        var stockInspectXr = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Input/XRInputAdapter.cs");
        Check(stockInspectDesktop.Contains("pointerBottleInspectionAtPress", StringComparison.Ordinal) &&
              stockInspectDesktop.Contains("TryInspectStockBottle(", StringComparison.Ordinal) &&
              stockInspectDesktop.Contains("PanelAction.Inspect, inventoryId", StringComparison.Ordinal) &&
              stockInspectXr.Contains("TryInspectPhysicalVesselByTouch(", StringComparison.Ordinal) &&
              stockInspectXr.Contains("ResearchPhysicalInspectionSelection.XrUniqueInventory(", StringComparison.Ordinal) &&
              stockInspectXr.Contains("receiverGrip == null || !receiverGrip.isSelected", StringComparison.Ordinal) &&
              stockInspectXr.Contains("PanelAction.Measure : PanelAction.Inspect", StringComparison.Ordinal),
            "Physical Desktop/XR inspection must reach shared Native stock/receiver panel routes.");

        // XR empty-hand fingertip/trigger can select a real receiver for its
        // existing Native pH panel, but cannot read when a tool/GUI owns
        // the trigger, tracking is stale, or the receiver is out of touch.
        Check(ResearchXRReceiverInspectionAdmission.MayInspect(
            true, true, true, false, .02),
            "Fresh tracked XR fingertip touching receiver must admit Native inspection.");
        Check(!ResearchXRReceiverInspectionAdmission.MayInspect(
            false, true, true, false, .02) &&
              !ResearchXRReceiverInspectionAdmission.MayInspect(
            true, false, true, false, .02) &&
              !ResearchXRReceiverInspectionAdmission.MayInspect(
            true, true, false, false, .02) &&
              !ResearchXRReceiverInspectionAdmission.MayInspect(
            true, true, true, true, .02) &&
              !ResearchXRReceiverInspectionAdmission.MayInspect(
            true, true, true, false, .20) &&
              !ResearchXRReceiverInspectionAdmission.MayInspect(
            true, true, true, false, double.NaN),
            "XR receiver inspection must fail closed on Hold, tracking, held tool, UI or distance.");

        // XR Pause/Hold places Review and Recenter on the same row. The
        // visual policy must give two independent raycast rectangles, and
        // Pause must expose the actual existing offline export action.
        var xrPortableUiSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/UI/SpatialPanelPresenter.cs");
        Check(xrPortableUiSource.Contains("Export portable study", StringComparison.Ordinal) &&
              xrPortableUiSource.Contains("SplitReviewAndRecenter(", StringComparison.Ordinal) &&
              xrPortableUiSource.Contains("LastPortableArchiveFileName", StringComparison.Ordinal),
            "XR offline Pause lost its export route/filename or Review-Recenter raycast split.");
        Check(ResearchXRPanelHitLayout.SplitReviewAndRecenter(
                "Pause", true, true) &&
              ResearchXRPanelHitLayout.SplitReviewAndRecenter(
                "Hold", true, true) &&
              !ResearchXRPanelHitLayout.SplitReviewAndRecenter(
                "Lab", true, true) &&
              !ResearchXRPanelHitLayout.SplitReviewAndRecenter(
                "Pause", false, true),
            "XR Pause/Hold Review and Recenter hit rectangles must be exclusive.");
        // Native save chooser is a bounded offline discovery API only:
        // no candidate bytes are treated as Native authority by the picker.
        var chooserFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plv-save-picker-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(chooserFolder);
        try
        {
            var oldSave = System.IO.Path.Combine(chooserFolder, "older.plv.json");
            var nextBackup = System.IO.Path.Combine(chooserFolder, "newer.plv.json.bak");
            var unrelated = System.IO.Path.Combine(chooserFolder, "nope.plv.json.tmp");
            System.IO.File.WriteAllText(oldSave, "untrusted placeholder only");
            System.IO.File.WriteAllText(nextBackup, "untrusted placeholder only");
            System.IO.File.WriteAllText(unrelated, "untrusted placeholder only");
            System.IO.File.SetLastWriteTimeUtc(oldSave,
                new System.DateTime(2020, 1, 1, 0, 0, 0, System.DateTimeKind.Utc));
            System.IO.File.SetLastWriteTimeUtc(nextBackup,
                new System.DateTime(2025, 1, 1, 0, 0, 0, System.DateTimeKind.Utc));
            var names = OfflineArchivePicker.LocalNativeSaveNamesNewest(
                chooserFolder, new[] { oldSave, nextBackup, unrelated,
                    System.IO.Path.Combine(chooserFolder, "..", "outside.plv.json") });
            Check(names.Count == 2 && names[0] == "newer" && names[1] == "older" &&
                  OfflineArchivePicker.NextName(names, names[0]) == "older" &&
                  OfflineArchivePicker.ValidNativeSaveName("older") &&
                  !OfflineArchivePicker.ValidNativeSaveName("../older") &&
                  !OfflineArchivePicker.ValidNativeSaveName("older.plv.json"),
                "Offline Native saved-session chooser admitted traversal/staging or lost a backup.");
        }
        finally { System.IO.Directory.Delete(chooserFolder, true); }

        var saveCopySource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Session/SessionController.cs");
        Check(saveCopySource.Contains("public bool SaveAsNewLocalCopy()") &&
              saveCopySource.Contains("NotebookService.InstallPortableSnapshot(") &&
              saveCopySource.Contains("var verifiedFreshName = false;") &&
              saveCopySource.Contains("saveName = name;") &&
              saveCopySource.Contains("if (verifiedFreshName)"),
            "Local SaveCopy lost its independent source journal, guarded file namespace or no-delete-on-collision rule.");

        // Native run handover must not import and retire the old authority
        // before a confirmed fresh Native .plv.json and Notebook exist.
        var independentDriverSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Core/CoreDriver.cs");
        Check(independentDriverSource.Contains("stagedResearchBootstrap: true") &&
              independentDriverSource.Contains("BootstrapResearchSession(staged)") &&
              independentDriverSource.Contains("return staged;") &&
              !independentDriverSource.Contains("using var staged = new CoreSession"),
            "The new trial preflight must preserve the previous Native context.");
        var independentSessionSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Session/SessionController.cs");
        var independentSaveLine = independentSessionSource.IndexOf(
            "var saved = saves.Save(fresh, root, newName)", StringComparison.Ordinal);
        var independentImportLine = independentSessionSource.IndexOf(
            "CoreSession.ImportSession(preImport", StringComparison.Ordinal);
        var validatedJournalLine = independentSessionSource.IndexOf(
            "var notes = notebook.ExportBranch(NotebookFilePath()", StringComparison.Ordinal);
        var oldRunSaveLine = independentSessionSource.IndexOf(
            "saves.Save(driver.Session, root, oldName)", StringComparison.Ordinal);
        Check(independentSaveLine > 0 && independentImportLine > independentSaveLine &&
              validatedJournalLine > 0 && oldRunSaveLine > validatedJournalLine &&
              independentSaveLine > oldRunSaveLine &&
              independentSessionSource.Contains("driver.ReplaceSession(fresh)"),
            "Unverified old Notebook or undurable new study must not retire Native.");

        // Native alone closes stock preparation: a loaded session with an
        // empty but already-closed source may NEVER offer a second PrepareStock.
        // It must recover via Continue rather than the unprepared ResumeSetup.
        Check(independentDriverSource.Contains("public bool setupClosed;") &&
              independentDriverSource.Contains("!state.setupClosed") &&
              independentDriverSource.Contains("IsTimeHeld && SourceNeedsPreparation") &&
              independentDriverSource.Contains(
                  "requiresSetupRecovery ? \"ResumeSetup\" : \"Continue\""),
            "Unity offered stock preparation after Native setupClosed=true.");

        // TDD: when the learner intentionally launches a second Native
        // study, its first confirmed save is already the obvious recorded
        // comparison baseline. A read-only preview and explicit file label
        // must survive the Native restoration invalidation callback.
        var pinnedFirstRunLine = independentSessionSource.IndexOf(
            "selectedLocalNativeSaveName = oldName;", StringComparison.Ordinal);
        var pinnedFirstRunPreview = independentSessionSource.IndexOf(
            "PreviewRecordedLocalNativeSave(root, oldName)", StringComparison.Ordinal);
        var restoredHandoverLine = independentSessionSource.IndexOf(
            "driver.ReplaceSession(fresh)", StringComparison.Ordinal);
        Check(pinnedFirstRunLine > restoredHandoverLine &&
              pinnedFirstRunPreview > 0 && pinnedFirstRunPreview < restoredHandoverLine,
            "New Native run lost its explicitly saved previous trial as read-only comparison baseline.");
        Check(independentSessionSource.Contains(
            "GUILayout.Label(\"Recorded baseline file: \" + validatedComparisonBaselineName"),
            "Desktop recorded comparison omits the actual previous Native file identity.");

        // Selected saved-recording preview must be visible in both recovery
        // panels; the matching Native-backed Unity PlayMode test is NOT_RUN.
        var previewSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Session/SessionController.cs");
        var xrRecoverySource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/UI/SpatialPanelPresenter.cs");
        Check(previewSource.Contains("RecordedSessionReview.OpenRecoverable(path)") &&
              previewSource.Contains("PreviewRecordedLocalNativeSave(") &&
              previewSource.Contains("selectedLocalNativeSavePreview = null;") &&
              previewSource.Contains("GUILayout.Label(SelectedLocalNativeSavePreview)") &&
              xrRecoverySource.Contains("panels?.SelectedLocalNativeSavePreview"),
            "Read-only Native-save preview lost Desktop or XR recovery integration.");

        // Comparison must use the explicitly chosen offline file, not silently
        // replace it with an active Save name; scientific comparison already
        // requires matching recorded Native model/provider provenance.
        Check(previewSource.Contains("public string ComparisonBaselineName") &&
              previewSource.Contains("RecordedRunComparison.CompareSaved(path, RecordedReview)") &&
              previewSource.Contains("ComparisonBaselinePath()") &&
              previewSource.Contains("recordedCache.InvalidateComparison(); // selection changes saved baseline") &&
              xrRecoverySource.Contains("panels?.ComparisonBaselineName"),
            "Selected saved-file comparison is not wired through real Desktop/XR Review.");

        // Native comparison selection in Review is read-only, with two
        // disjoint XR ray-hit regions for CSV vs saved-baseline selection.
        Check(previewSource.Contains("screen != ScreenId.Pause && screen != ScreenId.Hold)") &&
              previewSource.Contains("Browsing in recorded Review never grants live Native import.") &&
              xrRecoverySource.Contains("AddInstrumentButton(\"Choose comparison run\"") &&
              xrRecoverySource.Contains("canChooseComparison ? 176f : 368f") &&
              xrRecoverySource.Contains("chooser.anchoredPosition = new Vector2(94f, -250f)"),
            "Review baseline chooser or non-overlapping XR hit admission lost.");

        // The new immutable recorded-only CSV producer runs in the real-file
        // Notebook headless suite, and the same action is exposed in Desktop/XR.
        var comparisonCsvSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Session/RecordedRunComparison.cs");
        Check(comparisonCsvSource.Contains("public static class RecordedRunComparisonCsv") &&
              comparisonCsvSource.Contains("FileMode.CreateNew") &&
              previewSource.Contains("public bool ExportCurrentComparisonCsv()") &&
              xrRecoverySource.Contains("AddButton(\"Export comparison CSV\""),
            "Native recorded comparison immutable CSV export lost production/Desktop/XR linkage.");

        // Source-level safeguard for the Unity compile gate. The original
        // generated reserve-stock smoke test briefly escaped its test class;
        // keep the fixture nested and XR quick-note controls panel-routed.
        var unitySmokeSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Tests/PlayMode/NativeRuntimeSmokeTests.cs");
        var unityClassAt = unitySmokeSource.IndexOf(
            "public sealed class NativeRuntimeSmokeTests", StringComparison.Ordinal);
        var firstUnityTestAt = unitySmokeSource.IndexOf(
            "[UnityTest]", StringComparison.Ordinal);
        Check(unityClassAt >= 0 && firstUnityTestAt > unityClassAt &&
              unitySmokeSource.IndexOf("public sealed class NativeRuntimeSmokeTests",
                  unityClassAt + 1, StringComparison.Ordinal) < 0,
            "Authored Unity smoke tests escaped or duplicated their NUnit class.");
        var xrNotebookSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/UI/SpatialPanelPresenter.cs");
        Check(xrNotebookSource.Contains("Quick notes", StringComparison.Ordinal) &&
              xrNotebookSource.Contains("notebookReflectionPage", StringComparison.Ordinal) &&
              xrNotebookSource.Contains(
                  "panels?.Request(PanelAction.AddNotebookNote,", StringComparison.Ordinal) &&
              xrNotebookSource.Contains("directLabNotebook", StringComparison.Ordinal) &&
              xrNotebookSource.Contains("notebookEntryRect.sizeDelta", StringComparison.Ordinal) &&
              xrNotebookSource.Contains("saveRect.sizeDelta", StringComparison.Ordinal),
            "XR learner direct Lab Notebook/Quick notes navigation lost separate hit targets or routing.");

        // CI cannot build Unity player scripts without the Unity SDK; fail
        // source-level CI when a UI edit accidentally discards production
        // physical priming wiring or redirects Pause/Hold chooser to Review.
        var integrationSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Session/SessionController.cs");
        Check(integrationSource.Contains("public bool TryPhysicalFillBuretteFromBottle(") &&
              integrationSource.Contains("private bool HasNativeBurettePrimeCapacity") &&
              integrationSource.Contains("router?.Current?.ScreenId != ScreenId.Lab") &&
              integrationSource.Contains("private bool TransferNativePrime(") &&
              integrationSource.Contains("router.Show(router.Current.ScreenId);"),
            "Unity product route lost physical Native burette prime or offline Pause archive chooser.");

        Check(ResearchReserveBottleRefillAdmission.MayRefill(
                1, 1, .07, .0001, .025, .002, true) &&
              !ResearchReserveBottleRefillAdmission.MayRefill(
                1, 1, .07, .0001, .025, .002, false) &&
              !ResearchReserveBottleRefillAdmission.MayRefill(
                1, .2, .07, .0001, .025, .002, true) &&
              !ResearchReserveBottleRefillAdmission.MayRefill(
                1, 1, -.02, .0001, .025, .002, true) &&
              !ResearchReserveBottleRefillAdmission.MayRefill(
                1, 1, .07, .04, .025, .002, true) &&
              !ResearchReserveBottleRefillAdmission.MayRefill(
                1, 1, .07, .0001, .025, 1.0, true),
            "Native reserve-to-source refill must require real inverted outlet over stable stock mouth.");

        // The source bottle can physically prime the real burette only at
        // its separate upright receiving mouth, never at the receiver or
        // via a held/undocked burette, dry stock, or an open valve.
        Check(ResearchBuretteInletAdmission.MayPrime(
            1, 1, .075, .0001, .018, true, true),
            "An upright docked burette with bottle outlet over its neck must admit measured prime.");
        Check(!ResearchBuretteInletAdmission.MayPrime(
            0, 1, .075, .0001, .018, true, true) &&
              !ResearchBuretteInletAdmission.MayPrime(
            1, 0, .075, .0001, .018, true, true) &&
              !ResearchBuretteInletAdmission.MayPrime(
            1, 1, -.005, .0001, .018, true, true) &&
              !ResearchBuretteInletAdmission.MayPrime(
            1, 1, .16, .0001, .018, true, true) &&
              !ResearchBuretteInletAdmission.MayPrime(
            1, 1, .075, .0005, .018, true, true) &&
              !ResearchBuretteInletAdmission.MayPrime(
            1, 1, .075, .0001, .018, false, true) &&
              !ResearchBuretteInletAdmission.MayPrime(
            1, 1, .075, .0001, .018, true, false),
            "Wrong bottle orientation, distance, aperture, bench ownership or valve status must refuse prime.");
        Check(ResearchAuxiliaryDelivery.Resolve(
                "research-stock-bottle-visual", true, true,
                false, false, true, true) ==
                ResearchAuxiliaryDeliveryIntent.BurettePrime &&
              ResearchAuxiliaryDelivery.Resolve(
                "research-stock-bottle-visual", true, true,
                true, false, true, true) ==
                ResearchAuxiliaryDeliveryIntent.None &&
              ResearchAuxiliaryDelivery.Resolve(
                "research-stock-bottle-visual", true, true,
                false, false, true, false) ==
                ResearchAuxiliaryDeliveryIntent.None &&
              ResearchAuxiliaryDelivery.Resolve(
                "research-pipette-visual", true, true,
                false, false, true, true) ==
                ResearchAuxiliaryDeliveryIntent.None,
            "Physical priming needs exclusive source-bottle and burette-inlet authority.");

        // A 1 mL Native water meniscus is narrower than its visible cup rim.
        // No rinse when the pipette is in the dry annulus around the liquid.
        var rinseWaterRadius = ResearchRinseApertureAdmission.EffectiveRadiusMeters(
            0.010, 0.5, 0.008, 0.5, 0.008);
        Check(Math.Abs(rinseWaterRadius - 0.004) < 1e-12 &&
              ResearchSourceImmersionAdmission.MayAspirate(
                  1, 1, .043, .036, .056, .003 * .003, rinseWaterRadius, 0) &&
              !ResearchSourceImmersionAdmission.MayAspirate(
                  1, 1, .043, .036, .056, .007 * .007, rinseWaterRadius, 0),
            "Inside the cup rim but outside actual Native water is a dry rinse.");
        Check(ResearchRinseApertureAdmission.EffectiveRadiusMeters(
                0.01, 0.5, 0, 0.5, 0.008) == 0d &&
              ResearchRinseApertureAdmission.EffectiveRadiusMeters(
                0.01, double.NaN, 0.008, 0.5, 0.008) == 0d,
            "Empty or malformed Native-water geometry must fail closed.");
                // M2: physical aspiration must reach the Native-rendered liquid
        // surface, not just a lip; disallowed geometry cannot generate an intent.
        Check(ResearchSourceImmersionAdmission.MayAspirate(
            1, 1, .171, .162, .192, .0001, .025, 0),
            "Stationary upright prepared stock must admit an immersed tip.");
        Check(!ResearchSourceImmersionAdmission.MayAspirate(
            1, 1, .150, .162, .192, .0001, .025, 0) &&
              !ResearchSourceImmersionAdmission.MayAspirate(
            1, 1, .191, .162, .192, .0001, .025, 0) &&
              !ResearchSourceImmersionAdmission.MayAspirate(
            1, 1, .171, .162, .192, .0008, .025, 0) &&
              !ResearchSourceImmersionAdmission.MayAspirate(
            1, 0, .171, .162, .192, .0001, .025, 0) &&
              !ResearchSourceImmersionAdmission.MayAspirate(
            1, 1, .171, .162, .192, .0001, .025, .04) &&
              !ResearchSourceImmersionAdmission.MayAspirate(
            1, 1, .171, double.NaN, .192, .0001, .025, 0),
            "Dry, bottomed-out, off-center, inverted, moving and unknown-liquid tips must fail.");
        Check(ResearchAuxiliaryDelivery.Resolve(
            "research-pipette-visual", true, true, false, true, true)
                == ResearchAuxiliaryDeliveryIntent.PipetteAspirate &&
            ResearchAuxiliaryDelivery.Resolve(
            "research-pipette-visual", true, true, true, false, true)
                == ResearchAuxiliaryDeliveryIntent.PipetteDispense &&
            ResearchAuxiliaryDelivery.Resolve(
            "research-pipette-visual", true, true, true, true, true)
                == ResearchAuxiliaryDeliveryIntent.None &&
            ResearchAuxiliaryDelivery.Resolve(
            "research-stock-bottle-visual", true, true, false, true, true)
                == ResearchAuxiliaryDeliveryIntent.None &&
            ResearchAuxiliaryDelivery.Resolve(
            "research-pipette-visual", true, true, false, true, false)
                == ResearchAuxiliaryDeliveryIntent.None,
            "Physical pipette station must be exclusive; paused or bottle aspiration disallowed.");

        Check(ResearchPhysicalSourceBinding.Matches(
                "source", "source", null, false) &&
              ResearchPhysicalSourceBinding.Matches(
                "source", "source", "source", true) &&
              !ResearchPhysicalSourceBinding.Matches(
                "source", "source", "reserve-stock", true) &&
              !ResearchPhysicalSourceBinding.Matches(
                "reserve-stock", "source", null, false) &&
              !ResearchPhysicalSourceBinding.Matches(
                "source", "source", "", true),
            "Physical immersion must not siphon a different Native reserve source.");
        Check(ResearchPhysicalSourceBinding.Matches(
                "source", "reserve-stock", "reserve-stock", true) &&
              !ResearchPhysicalSourceBinding.Matches(
                "source", "reserve-stock", "source", true) &&
              !ResearchPhysicalSourceBinding.Matches(
                "source", "reserve-stock", null, false) &&
              ResearchPhysicalSourceBinding.Matches(
                "source", "source", null, false),
            "A switched Native pipette must draw only from its real represented reserve vessel.");

        var desktopPhysicalSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Input/DesktopInputAdapter.cs");
        var xrPhysicalSource = System.IO.File.ReadAllText(
            "unity/Assets/PharmaLabVR/Runtime/Input/XRInputAdapter.cs");
        Check(desktopPhysicalSource.Contains("screenGestureGate.RequireFreshNeutralInput()") &&
              desktopPhysicalSource.Contains("screenGestureGate.ObserveNeutral(") &&
              desktopPhysicalSource.Contains("!screenGestureGate.IsArmed") &&
              xrPhysicalSource.Contains("screenGestureGate.RequireFreshNeutralInput()") &&
              xrPhysicalSource.Contains("screenGestureGate.ObserveNeutral(") &&
              xrPhysicalSource.Contains("!screenGestureGate.IsArmed"),
            "Returning from Notebook/Review may reuse an XR trigger or Desktop G/valve gesture.");
        Check(desktopPhysicalSource.Contains("if (!IsOperatingLabScreen())") &&
              desktopPhysicalSource.Contains("ResearchLabScreenAdmission.MayActuate(") &&
              xrPhysicalSource.Contains("!IsOperatingLabScreen()") &&
              xrPhysicalSource.Contains("ResearchLabScreenAdmission.MayActuate("),
            "Physical Desktop/XR Native gestures lost Notebook/Review lab-screen isolation.");

        // Leaving Lab disowns a pending trigger without requiring the
        // student to destroy physical ownership of a held Desktop tool.
        // Returning to Lab while G/trigger is still down must not fire; a
        // new neutral frame is observed before any later deliberate press.
        var screenGesture = new ResearchRestorationGestureGate();
        screenGesture.RequireFreshNeutralInput();
        Check(!screenGesture.ObserveNeutral(true, false, true) &&
              !screenGesture.ObserveNeutral(true, true, false) &&
              screenGesture.ObserveNeutral(true, true, true),
            "Screen navigation accepted a carried press or untracked neutral baseline.");
        screenGesture.RequireFreshNeutralInput();
        Check(!screenGesture.IsArmed &&
              !screenGesture.ObserveNeutral(true, false, true) &&
              screenGesture.IsArmed == false,
            "A second Notebook/Review transition reused the previous button release.");
        // Physical material gestures must be admitted only from active Lab,
        // never while the student records a branch-bound Notebook note,
        // reviews recorded evidence, or navigates a paused/held screen.
        Check(ResearchLabScreenAdmission.MayActuate("Lab", true) &&
              !ResearchLabScreenAdmission.MayActuate("Notebook", true) &&
              !ResearchLabScreenAdmission.MayActuate("Review", true) &&
              !ResearchLabScreenAdmission.MayActuate("Inspect", true) &&
              !ResearchLabScreenAdmission.MayActuate("Measure", true) &&
              !ResearchLabScreenAdmission.MayActuate("Pause", true) &&
              !ResearchLabScreenAdmission.MayActuate("Hold", true) &&
              !ResearchLabScreenAdmission.MayActuate("Lab", false) &&
              !ResearchLabScreenAdmission.MayActuate(null, true),
            "Physical material inputs leaked through non-Lab screens or Native Hold.");

        // Deliberate pipette contact with the *other* prepared stock first
        // requests a Native source switch. This must never aspirate any
        // material during the same trigger gesture.
        Check(ResearchPhysicalSourceSelection.Resolve(
            "research-pipette-visual", true, true, false,
            false, true, false, false, true)
                == ResearchAuxiliaryDeliveryIntent.PipetteSelectReserve &&
              ResearchPhysicalSourceSelection.Resolve(
            "research-pipette-visual", true, true, false,
            true, false, false, false, true)
                == ResearchAuxiliaryDeliveryIntent.PipetteSelectPrimary &&
              ResearchPhysicalSourceSelection.Resolve(
            "research-pipette-visual", true, true, false,
            false, true, false, true, true)
                == ResearchAuxiliaryDeliveryIntent.None,
            "Physical mismatched-source touch must switch only once; matched stock aspirates separately.");
        Check(ResearchPhysicalSourceSelection.Resolve(
            "research-pipette-visual", true, true, true,
            false, true, false, false, true)
                == ResearchAuxiliaryDeliveryIntent.None &&
              ResearchPhysicalSourceSelection.Resolve(
            "research-pipette-visual", true, true, false,
            true, true, false, false, true)
                == ResearchAuxiliaryDeliveryIntent.None &&
              ResearchPhysicalSourceSelection.Resolve(
            "research-pipette-visual", true, false, false,
            false, true, false, false, true)
                == ResearchAuxiliaryDeliveryIntent.None &&
              ResearchPhysicalSourceSelection.Resolve(
            "research-pipette-visual", true, true, false,
            false, true, false, false, false)
                == ResearchAuxiliaryDeliveryIntent.None &&
              ResearchPhysicalSourceSelection.Resolve(
            "research-stock-bottle-visual", true, true, false,
            false, true, false, false, true)
                == ResearchAuxiliaryDeliveryIntent.None,
            "Two stocks, receiver overlap, absent grip/press or Native Hold must deny switching.");

        // Restored Native authority must disown held physical gestures until
        // real controls are neutral and the previous grip has been released.
        var restoredGate = new ResearchRestorationGestureGate();
        Check(restoredGate.IsArmed, "Fresh laboratory input should begin armed.");
        restoredGate.RequireFreshNeutralInput();
        Check(!restoredGate.IsArmed &&
              !restoredGate.ObserveNeutral(false, true, true) &&
              !restoredGate.ObserveNeutral(true, false, true) &&
              !restoredGate.ObserveNeutral(true, true, false),
            "Native restoration accepted held equipment, depressed controls or missing tracking.");
        Check(restoredGate.ObserveNeutral(true, true, true) &&
              restoredGate.IsArmed,
            "A released tool, neutral controls and valid tracking did not re-arm input.");
        restoredGate.RequireFreshNeutralInput();
        Check(!restoredGate.IsArmed,
            "A second Native session restoration reused an earlier neutral baseline.");

        // M2: holding a real visual-only pipette/stock bottle must never
        // turn pointer motion into a Native burette valve command. They can
        // still be moved on the workbench with an ordinary left drag.
        Check(DesktopPointerRouting.ResolveForHeldInstrument(true, false, false,
                  true, false, true) == DesktopPointerAction.ToolDrag &&
              DesktopPointerRouting.ResolveForHeldInstrument(true, false, true,
                  true, false, true) == DesktopPointerAction.ToolDrag,
            "Auxiliary research instruments need physical dragging without virtual burette flow.");
        Check(DesktopPointerRouting.ResolveForHeldInstrument(true, true, false,
                  true, false, true) == DesktopPointerAction.Valve &&
              DesktopPointerRouting.ResolveForHeldInstrument(true, true, true,
                  true, false, true) == DesktopPointerAction.ToolDrag,
            "Graduated burette must preserve left-drag valve and Shift+drag movement.");
        Check(DesktopPointerRouting.ResolveForHeldInstrument(true, false, false,
                  true, true, true) == DesktopPointerAction.None &&
              DesktopPointerRouting.ResolveForHeldInstrument(true, false, false,
                  true, false, false) == DesktopPointerAction.None &&
              DesktopPointerRouting.ResolveForHeldInstrument(false, false, false,
                  true, false, true) == DesktopPointerAction.None,
            "UI capture, disabled mode and empty hands must prevent all object manipulation.");
        Check(DesktopPointerRouting.ShouldSampleHeldAsNative(true, false) == false &&
              DesktopPointerRouting.ShouldSampleHeldAsNative(true, true) == true &&
              DesktopPointerRouting.ShouldSampleHeldAsNative(false, false) == false,
            "An auxiliary held tool cannot masquerade as the registered Native burette.");

        Check(DesktopPointerRouting.Resolve(true, true, true, false, true) == DesktopPointerAction.ToolDrag,
            "Shift + left-drag with a held instrument must move its world pose");
        Check(DesktopPointerRouting.Resolve(true, false, true, false, true) == DesktopPointerAction.Valve,
            "Ordinary held left-drag must actuate only the valve");
        Check(DesktopPointerRouting.Resolve(false, true, true, false, true) == DesktopPointerAction.None,
            "Empty hand may not move an unowned tool by shift-click");
        Check(DesktopPointerRouting.Resolve(true, true, true, true, true) == DesktopPointerAction.None &&
              DesktopPointerRouting.Resolve(true, false, true, true, true) == DesktopPointerAction.None,
            "World actions must not steal GUI input focus");
        Check(DesktopPointerRouting.Resolve(true, true, true, false, false) == DesktopPointerAction.None &&
              DesktopPointerRouting.Resolve(true, false, false, false, true) == DesktopPointerAction.None,
            "Disabled input or released button must leave both routes neutral");
        // P03.03: generated Desktop burette outlet must physically exist
        // at the bottom of the mesh, not at an arbitrary stock bottle tip.
        Check(Math.Abs(ResearchOutletGeometry.BottomLocalY(0.25, -1.0, 0.25)) < 1e-12,
            "50 mL burette geometry did not resolve to its actual cylinder foot");
        Check(Math.Abs(ResearchOutletGeometry.BottomLocalY(0.30, -1.0, 0.25) - 0.05) < 1e-12,
            "Mesh transform moved but outlet landmark remained at a magic world offset");
        var invalidOutletRejected = false;
        try { ResearchOutletGeometry.BottomLocalY(0.25, double.NaN, 0.25); }
        catch (ArgumentOutOfRangeException) { invalidOutletRejected = true; }
        Check(invalidOutletRejected, "Nonfinite mesh geometry cannot define a capture outlet");
        // P10.02: accessibility settings survive Boot->Lab and process restarts
        // without being interpreted as Native chemistry or using local number formats.
        var persisted = new LabSetupPreferences("Seated", "Left", 0.77f, 1.25f,
            true, true, true, 0.45f);
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            var encoded = SetupPreferencesCodec.Encode(persisted);
            Check(encoded.Contains("0.77") && encoded.Contains("1.25") &&
                  !encoded.Contains("0,77"),
                "Preference encoding must use invariant decimal separators");
            Check(SetupPreferencesCodec.TryDecode(encoded, out var restored) &&
                  restored.Posture == "Seated" && restored.DominantHand == "Left" &&
                  restored.BenchHeightMeters == 0.77f && restored.TextScale == 1.25f &&
                  restored.CaptionsEnabled && restored.ReducedMotion && restored.HighContrast &&
                  restored.AudioVolume == 0.45f,
                "Roundtrip lost actual ergonomics or accessibility options");
            Check(!SetupPreferencesCodec.TryDecode(encoded.Replace("1;Seated", "2;Seated"), out _),
                "Unknown stored preferences schema silently admitted");
            Check(!SetupPreferencesCodec.TryDecode(encoded.Replace("0.77", "NaN"), out _) &&
                  !SetupPreferencesCodec.TryDecode(encoded + ";injection", out _),
                "Invalid stored values must not enter runtime accessibility state");
            Check(!SetupPreferencesCodec.TryDecode("", out _),
                "Blank persisted content cannot override safe Boot defaults");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previousCulture; }
        // P10.02: actual Unity AudioListener gain must be driven by the
        // same immutable Setup preferences used by Desktop and XR panels.
        Check(Math.Abs(AudioPreferenceRouting.Gain(
            new LabSetupPreferences("Standing", "Right", 0.85f, 1f, true, false, false, 0.35f)) - 0.35f) < 1e-7,
            "Audio-volume preference cannot be routed to real mixer gain");
        Check(AudioPreferenceRouting.Gain(
            new LabSetupPreferences("Seated", "Left", 0.75f, 1.2f, false, true, true, 0f)) == 0f,
            "Mute must be a real zero gain, not only a labelled UI state");
        Check(AudioPreferenceRouting.Gain(
            new LabSetupPreferences("Seated", "Left", 0.75f, 1.2f, false, true, true, 1f)) == 1f,
            "Full-volume option must restore a valid gain");
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

            // P06.02: a newly injected Native driver is a distinct authority
            // even when its branch and revision text happen to match the old one.
            // Presentation holds, Settings history, inspection and Save status
            // must never be inherited from a disposed gameplay session.
            var disposedDriver = new CoreDriver { CurrentNativeBranchId = "shared-branch" };
            var replacementRouter = new ScreenRouter();
            replacementRouter.Configure(disposedDriver);
            replacementRouter.Show(ScreenId.Inspect, "receiver");
            replacementRouter.MarkSaved();
            Check(!replacementRouter.Current.Dirty, "First session save fixture was not established.");
            Check(disposedDriver.PauseSession(), "First session pause fixture was refused.");
            replacementRouter.Show(ScreenId.Pause);
            replacementRouter.Show(ScreenId.Settings);
            var newDriver = new CoreDriver { CurrentNativeBranchId = "shared-branch" };
            replacementRouter.Configure(newDriver);
            Check(replacementRouter.Current.ScreenId == ScreenId.Lab &&
                  replacementRouter.Current.SelectedEntityId == "source" &&
                  replacementRouter.Current.Dirty &&
                  replacementRouter.Current.SupportedActions.Contains("Pause"),
                "Replacing Native authority carried stale Settings/selection/Save across session.");
            disposedDriver.SetHold(true);
            Check(replacementRouter.Current.ScreenId == ScreenId.Lab,
                "Old CoreDriver callback mutated the replacement session panel.");
            newDriver.SetHold(true);
            Check(replacementRouter.Current.ScreenId == ScreenId.Hold,
                "Replacement Native Hold did not take precedence.");
            var pausedReplacement = new CoreDriver { CurrentNativeBranchId = "shared-branch" };
            Check(pausedReplacement.PauseSession(), "Replacement Pause fixture was refused.");
            replacementRouter.Configure(pausedReplacement);
            Check(replacementRouter.Current.ScreenId == ScreenId.Pause &&
                  replacementRouter.Current.Dirty &&
                  replacementRouter.Current.SelectedEntityId == "source",
                "A new paused authority inherited obsolete Hold or Saved state.");
            disposedDriver.SetHold(false);
            newDriver.SetHold(false);
            Check(replacementRouter.Current.ScreenId == ScreenId.Pause,
                "Detached Native drivers changed fresh Pause screen.");
            // P06.02: ScreenRouter.Show is a public presentation entrypoint.
            // It must not bypass the Native Pause/Continue authority protocol
            // when callers (including scene presenters) route directly.
            var guardedNative = new CoreDriver();
            var guardedRouter = new ScreenRouter();
            guardedRouter.Configure(guardedNative);
            Check(guardedNative.PauseSession(), "Native Pause fixture refused.");
            guardedRouter.Show(ScreenId.Pause);
            guardedRouter.Show(ScreenId.Lab);
            Check(guardedRouter.Current.ScreenId == ScreenId.Pause &&
                  guardedNative.IsNativePaused && guardedNative.ContinueCalls == 0,
                "Direct router Show(Lab) displayed live tools without Native Continue.");
            guardedRouter.Show(ScreenId.Settings);
            guardedRouter.Show(ScreenId.Lab);
            Check(guardedRouter.Current.ScreenId == ScreenId.Settings &&
                  guardedNative.IsNativePaused,
                "Settings direct Lab shortcut bypassed paused Native authority.");
            Check(guardedRouter.ReturnFromSettings() &&
                  guardedRouter.Current.ScreenId == ScreenId.Pause,
                "Settings Back did not preserve Native Pause.");
            Check(guardedNative.ContinueSession(), "Native Continue fixture refused.");
            guardedRouter.Show(ScreenId.Lab);
            Check(guardedRouter.Current.ScreenId == ScreenId.Lab,
                "A genuinely resumed Native session remained falsely paused.");
            guardedRouter.Show(ScreenId.Settings);
            Check(guardedNative.PauseSession(), "Late Native pause fixture refused.");
            Check(guardedRouter.ReturnFromSettings() &&
                  guardedRouter.Current.ScreenId == ScreenId.Pause &&
                  guardedNative.IsNativePaused,
                "Settings Back revived live Lab after a Native pause during Settings.");
            // P06.02: Native may pause from focus/system or a separate
            // session adapter, without a PanelAction.Pause button. A real
            // LabViewChanged re-render must not keep live input actions.
            var asynchronousDriver = new CoreDriver();
            var asynchronousRouter = new ScreenRouter();
            asynchronousRouter.Configure(asynchronousDriver);
            Check(asynchronousDriver.PauseSession(),
                "Out-of-band Native Pause fixture failed.");
            asynchronousDriver.UpdateView();
            Check(asynchronousRouter.Current.ScreenId == ScreenId.Pause &&
                  asynchronousDriver.IsNativePaused,
                "Native Pause update left an apparently live Lab screen.");
            // If a Hold covered a Lab before Pause was presented, native
            // recovery must restore Pause rather than stale Lab.
            var heldPauseDriver = new CoreDriver();
            var heldPauseRouter = new ScreenRouter();
            heldPauseRouter.Configure(heldPauseDriver);
            Check(heldPauseDriver.PauseSession(), "Interrupted Native Pause fixture failed.");
            heldPauseDriver.SetHold(true);
            Check(heldPauseRouter.Current.ScreenId == ScreenId.Hold,
                "Native Hold must take precedence over an unrendered Pause.");
            heldPauseDriver.SetHold(false);
            Check(heldPauseRouter.Current.ScreenId == ScreenId.Pause &&
                  heldPauseDriver.IsNativePaused,
                "Releasing a Native Hold resurrected live Lab over paused authority.");
            // P06.02/P11: Native RestartCheckpoint and accepted SessionLoad
            // replace the authority inside the SAME Unity CoreDriver object.
            // The event fires BEFORE its new Native snapshot is published.
            var importedAuthority = new CoreDriver { CurrentNativeBranchId = "old-authority" };
            var importedRouter = new ScreenRouter();
            importedRouter.Configure(importedAuthority);
            importedRouter.Show(ScreenId.Inspect, "receiver");
            importedRouter.MarkSaved();
            Check(!importedRouter.Current.Dirty && importedRouter.Current.SelectedEntityId == "receiver",
                "Same-driver restore fixture did not establish an old saved inspection.");
            importedRouter.Show(ScreenId.Settings);
            Check(importedRouter.Current.ScreenId == ScreenId.Settings,
                "Same-driver restore fixture did not enter old Settings.");
            importedAuthority.RaiseNativeSessionRestored();
            Check(importedRouter.Current.ScreenId == ScreenId.Hold &&
                  importedRouter.Current.SaveState == "Unsaved" &&
                  importedRouter.Current.Dirty &&
                  importedRouter.Current.SelectedEntityId == "source" &&
                  !importedRouter.Current.SupportedActions.Contains("PipetteDispense"),
                "NativeSessionRestored reused a stale Settings, Save, or selected vessel.");
            Check(!importedRouter.ReturnFromSettings(),
                "Native SessionLoad restored the previous session's Settings Back history.");
            importedAuthority.CurrentNativeBranchId = "imported-authority";
            importedAuthority.SetHold(true);
            importedAuthority.UpdateView();
            Check(importedRouter.Current.ScreenId == ScreenId.Hold &&
                  importedRouter.Current.NativeRevision == "7" &&
                  importedRouter.Current.Dirty,
                "A new Native snapshot resurrected the previous authority's saved label.");
            importedAuthority.SetHold(false);
            Check(importedRouter.Current.ScreenId != ScreenId.Settings &&
                  importedRouter.Current.ScreenId != ScreenId.Inspect,
                "Releasing SessionLoad Hold resurrected the old inspection Settings.");

            // P06.02: while the router is inactive, OnDisable unsubscribes
            // from Native events. A restored session must be recognized
            // when OnEnable reattaches, even with reused branch/sequence.
            var sleepingNative = new CoreDriver { CurrentNativeBranchId = "reused-branch" };
            var sleepingRouter = new ScreenRouter();
            sleepingRouter.Configure(sleepingNative);
            sleepingRouter.Show(ScreenId.Inspect, "receiver");
            sleepingRouter.MarkSaved();
            sleepingRouter.Show(ScreenId.Settings);
            Check(sleepingRouter.Current.ScreenId == ScreenId.Settings &&
                  sleepingRouter.Current.SaveState == "Saved",
                "Disabled-router fixture did not establish stale presentation.");
            var disableLifecycle = typeof(ScreenRouter).GetMethod("OnDisable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var enableLifecycle = typeof(ScreenRouter).GetMethod("OnEnable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Check(disableLifecycle != null && enableLifecycle != null,
                "Native panel lifecycle test cannot invoke real component callbacks.");
            disableLifecycle.Invoke(sleepingRouter, null);
            sleepingNative.RaiseNativeSessionRestored();
            Check(sleepingRouter.Current.ScreenId == ScreenId.Settings,
                "Disabled router unexpectedly observed an unsubscribed restore event.");
            enableLifecycle.Invoke(sleepingRouter, null);
            Check(sleepingRouter.Current.ScreenId == ScreenId.Hold &&
                  sleepingRouter.Current.SaveState == "Unsaved" &&
                  sleepingRouter.Current.Dirty &&
                  sleepingRouter.Current.SelectedEntityId == "source",
                "Reenabled router missed Native restoration while inactive.");
            disableLifecycle.Invoke(sleepingRouter, null);

            var unrelatedAuthority = new CoreDriver();
            importedRouter.Configure(unrelatedAuthority);
            importedAuthority.RaiseNativeSessionRestored();
            Check(importedRouter.Current.ScreenId == ScreenId.Lab &&
                  importedRouter.Current.SelectedEntityId == "source",
                "A detached NativeSessionRestored event modified the new CoreDriver panel.");
            // NativeSessionRestored fires BEFORE CoreDriver publishes its
            // replacement snapshot. The outgoing session may have been
            // recovery-ready; that stale flag must not expose Continue.
            var oldReadyNative = new CoreDriver { NativeRecoveryReady = true };
            var snapshotGate = new ScreenRouter();
            snapshotGate.Configure(oldReadyNative);
            oldReadyNative.SetHold(true);
            Check(snapshotGate.Current.ScreenId == ScreenId.Hold &&
                  snapshotGate.Current.SupportedActions.Contains("Continue"),
                "Pre-restore recovery-ready fixture could not show Continue.");
            oldReadyNative.RaiseNativeSessionRestored();
            Check(snapshotGate.Current.ScreenId == ScreenId.Hold &&
                  !snapshotGate.Current.SupportedActions.Contains("Continue"),
                "Unpublished replacement Native snapshot inherited an old Continue action.");
            oldReadyNative.NativeRecoveryReady = false;
            oldReadyNative.UpdateView();
            Check(!snapshotGate.Current.SupportedActions.Contains("Continue"),
                "Replacement Native snapshot not recovery-ready exposed Continue.");
            oldReadyNative.NativeRecoveryReady = true;
            oldReadyNative.UpdateView();
            Check(snapshotGate.Current.SupportedActions.Contains("Continue"),
                "Fresh confirmed recovery-ready Native snapshot remained blocked.");
            // A disabled Unity panel may miss BOTH the accepted Native
            // restore notification and the replacement LabViewChanged.
            // On reactivation a previously published, recovery-ready
            // snapshot must be usable; an unpublished one must stay held.
            var postRestoreNative = new CoreDriver { NativeRecoveryReady = true };
            var postRestoreRouter = new ScreenRouter();
            postRestoreRouter.Configure(postRestoreNative);
            postRestoreNative.SetHold(true);
            postRestoreRouter.Show(ScreenId.Settings);
            disableLifecycle.Invoke(postRestoreRouter, null);
            postRestoreNative.RaiseNativeSessionRestored();
            postRestoreNative.NativeRecoveryReady = true;
            postRestoreNative.UpdateView(); // published while router was disabled
            enableLifecycle.Invoke(postRestoreRouter, null);
            Check(postRestoreRouter.Current.ScreenId == ScreenId.Hold &&
                  postRestoreRouter.Current.SaveState == "Unsaved" &&
                  postRestoreRouter.Current.SupportedActions.Contains("Continue"),
                "Inactive panel ignored an already published recovery-ready Native snapshot.");
            disableLifecycle.Invoke(postRestoreRouter, null);

            var pendingRestoreNative = new CoreDriver { NativeRecoveryReady = true };
            var pendingRestoreRouter = new ScreenRouter();
            pendingRestoreRouter.Configure(pendingRestoreNative);
            pendingRestoreNative.SetHold(true);
            disableLifecycle.Invoke(pendingRestoreRouter, null);
            pendingRestoreNative.RaiseNativeSessionRestored();
            enableLifecycle.Invoke(pendingRestoreRouter, null);
            Check(pendingRestoreRouter.Current.ScreenId == ScreenId.Hold &&
                  !pendingRestoreRouter.Current.SupportedActions.Contains("Continue"),
                "Inactive panel admitted Continue before the replacement Native snapshot.");
            pendingRestoreNative.UpdateView();
            Check(pendingRestoreRouter.Current.SupportedActions.Contains("Continue"),
                "Pending replacement snapshot did not authorize recovery after publication.");
            disableLifecycle.Invoke(pendingRestoreRouter, null);
            // If the router sees NativeSessionRestored while active, then
            // goes inactive before LabViewChanged, reactivation must still
            // recognize the now-published epoch and not remain stuck.
            var midRestoreNative = new CoreDriver { NativeRecoveryReady = true };
            var midRestoreRouter = new ScreenRouter();
            midRestoreRouter.Configure(midRestoreNative);
            midRestoreNative.SetHold(true);
            midRestoreNative.RaiseNativeSessionRestored();
            Check(midRestoreRouter.Current.ScreenId == ScreenId.Hold &&
                  !midRestoreRouter.Current.SupportedActions.Contains("Continue"),
                "In-flight Native restore incorrectly admitted Continue.");
            disableLifecycle.Invoke(midRestoreRouter, null);
            midRestoreNative.UpdateView(); // publication while temporarily inactive
            enableLifecycle.Invoke(midRestoreRouter, null);
            Check(midRestoreRouter.Current.ScreenId == ScreenId.Hold &&
                  midRestoreRouter.Current.SupportedActions.Contains("Continue"),
                "Observed restore event then disabled router never recognized published recovery.");
            disableLifecycle.Invoke(midRestoreRouter, null);
            var driver = new CoreDriver();
            var router = new ScreenRouter();
            router.Configure(driver);
            // P06.02: a saved revision must also bind to the Native branch.
            // Importing a distinct branch with the same sequence is unsaved.
            driver.CurrentNativeBranchId = "branch-a";
            router.MarkSaved();
            Check(!router.Current.Dirty,
                "A confirmed same-branch save remained dirty.");
            var savedSequence = router.Current.NativeRevision;
            driver.CurrentNativeBranchId = "branch-b";
            driver.UpdateView();
            Check(router.Current.NativeRevision == savedSequence && router.Current.Dirty,
                "Another Native branch reused the saved sequence and showed Saved.");
            driver.CurrentNativeBranchId = "branch-a";
            router.MarkSaved();
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
            // P11.02: Notebook navigation and note creation are presentation
            // commands linked to a captured Native event; no Save or chemistry.
            Check(router.Current.SupportedActions.Contains("Notebook"),
                "Lab has no supported notebook entry route.");
            Check(!panels.Request(PanelAction.AddNotebookNote, "Should not be recorded"),
                "Note append bypassed the Notebook panel admission.");
            Check(panels.Request(PanelAction.Notebook) && router.Current.ScreenId == ScreenId.Notebook,
                "Shared Native-backed screen router did not open Notebook.");
            Check(router.Current.SupportedActions.Contains("AddNotebookNote"),
                "Notebook does not expose an explicit event-linked note action.");
            Check(panels.Request(PanelAction.AddNotebookNote, "Prediction before addition"),
                "Notebook note was not sent to the session-owned note store.");
            Check(session.NotebookAppendCalls == 1 && session.SaveCalls == 0,
                "Notebook note was replayed or implicitly saved Native chemistry.");
            Check(panels.Request(PanelAction.AddNotebookNote,
                    "Student selected reflection: I want another Native pH measurement before reaching a conclusion.") &&
                  session.NotebookAppendCalls == 2 &&
                  session.SaveCalls == 0 &&
                  driver.CurrentLabView.EventSequence == "7",
                "XR student-selectable reflection failed to route through the same real branch-linked Notebook.");

            Check(router.Current.SupportedActions.Contains("ExportPortable"),
                "Notebook has no explicit portable Native+notes export action.");
            Check(panels.Request(PanelAction.ExportPortable),
                "Notebook portable export did not reach the session-owned archive writer.");
            Check(session.PortableExportCalls == 1 && session.SaveCalls == 0,
                "Notebook archive export implicitly saved or repeated Native chemistry.");
            Check(panels.Request(PanelAction.Lab) && router.Current.ScreenId == ScreenId.Lab,
                "Notebook return must preserve the active Lab session.");
            Check(driver.CurrentLabView.EventSequence == "7",
                "Writing an annotation fabricated an authoritative Native material revision.");
            Check(panels.Request(PanelAction.Inspect, "receiver"), "Inspect receiver action refused");
            Check(router.Current.ScreenId == ScreenId.Inspect, "Inspect route missing");
            Check(router.Current.ModelIdentity == "IPhreeqc-research", "Native-derived model metadata lost");
            Check(panels.Request(PanelAction.Measure, "receiver"), "Measure action refused");
            Check(router.Current.ScreenId == ScreenId.Measure, "Measure route missing");
            Check(!panels.Request(PanelAction.Load) && session.LoadCalls == 0 &&
                  !panels.Request(PanelAction.ChooseLocalSave) &&
                  !panels.Request(PanelAction.LoadSelectedSave) &&
                  !panels.Request(PanelAction.SaveCopy) &&
                  !panels.Request(PanelAction.NewStudy) &&
                  session.LocalNativeLoadCalls == 0 &&
                  !panels.Request(PanelAction.RestorePortable) &&
                  session.PortableRestoreCalls == 0,
                "Live Desktop Measure unexpectedly permitted any Native session replacement");
            Check(panels.Request(PanelAction.Pause), "Pause action refused");
            Check(router.Current.ScreenId == ScreenId.Pause && driver.IsNativePaused, "Pause not held");
            Check(router.Current.SupportedActions.Contains("ChooseLocalSave") &&
                  router.Current.SupportedActions.Contains("LoadSelectedSave") &&
                  panels.Request(PanelAction.ChooseLocalSave) &&
                  session.LocalNativeChooseCalls == 1 &&
                  session.LocalNativeLoadCalls == 0,
                "Pause did not expose a side-effect-free local Native save selector.");
            session.NativeSelectedLoadPermitted = false;
            Check(!panels.Request(PanelAction.LoadSelectedSave) &&
                  session.LocalNativeLoadCalls == 0 && driver.IsNativePaused,
                "Invalid selected Native save bypassed guarded admission.");
            session.NativeSelectedLoadPermitted = true;
            Check(panels.Request(PanelAction.LoadSelectedSave) &&
                  session.LocalNativeLoadCalls == 1 && driver.IsNativePaused,
                "Selected Native save did not route through Pause without unpausing.");
            Check(router.Current.SupportedActions.Contains("SaveCopy"),
                "Pause does not expose non-overwriting Native/Notebook SaveCopy.");
            Check(router.Current.SupportedActions.Contains("NewStudy") &&
                  panels.CanStartIndependentStudy,
                "Pause cannot start a distinct Native research run.");
            session.NewStudyPermitted = false;
            Check(!panels.Request(PanelAction.NewStudy) &&
                  session.NewStudyCalls == 0,
                "Unavailable new Native study action falsely accepted.");
            session.NewStudyPermitted = true;
            Check(panels.Request(PanelAction.NewStudy) &&
                  session.NewStudyCalls == 1 && session.SaveCalls == 0 &&
                  driver.IsNativePaused,
                "New Native research run did not route from Pause independently of SaveCopy.");
            session.SaveNewCopyPermitted = false;
            Check(!panels.Request(PanelAction.SaveCopy) &&
                  session.SaveCopyCalls == 0,
                "Unavailable SaveCopy must not mutate the active study.");
            session.SaveNewCopyPermitted = true;
            Check(panels.Request(PanelAction.SaveCopy) &&
                  session.SaveCopyCalls == 1 && session.SaveCalls == 0 &&
                  driver.IsNativePaused,
                "New local checkpoint copy was not distinct from Native Save/Continue.");
            Check(router.Current.SupportedActions.Contains("RestorePortable") &&
                  router.Current.SupportedActions.Contains("ChoosePortableArchive") &&
                  panels.Request(PanelAction.ChoosePortableArchive),
                "Offline student package selection missing from Pause.");
            Check(router.Current.SupportedActions.Contains("ExportPortable") &&
                  panels.Request(PanelAction.ExportPortable) &&
                  panels.LastPortableArchiveFileName == "student.plvpack" &&
                  session.PortableExportCalls == 2 &&
                  session.SaveCalls == 0,
                "Paused XR package page must expose a real branch-local export with visible filename.");
            session.PortableRestorePermitted = false;
            Check(!panels.Request(PanelAction.RestorePortable) &&
                  session.PortableRestoreCalls == 0,
                "Unavailable or invalid offline package falsely imported a session.");
            session.PortableRestorePermitted = true;
            Check(panels.Request(PanelAction.RestorePortable) &&
                  session.PortableRestoreCalls == 1 &&
                  driver.IsNativePaused,
                "Native-backed offline restore did not route from Pause without unpausing.");
            Check(router.Current.SupportedActions.Contains("ExportPortable"),
                "Native Pause does not expose portable Export.");
            Check(panels.Request(PanelAction.ExportPortable),
                "Native Pause refused its explicit research archive Export.");
            Check(session.PortableExportCalls == 3 && session.SaveCalls == 0,
                "Portable Export caused an unintended Native Save or duplicate command.");
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
            // P06.02: Desktop Pause must use the same explicit Native-backed saved-session
            // load route, without the VR dock prerequisite or implicit Save.
            Check(router.Current.SupportedActions.Contains("Load"),
                "Desktop Pause has no explicit saved-session Load action");
            session.LoadPermitted = false;
            Check(!panels.Request(PanelAction.Load),
                "Missing Desktop saved session falsely returned Load success");
            Check(session.LoadCalls == 1, "Rejected Desktop load replaced Native session");
            session.LoadPermitted = true;
            Check(panels.Request(PanelAction.Load),
                "Desktop Pause did not dispatch saved-session Load");
            Check(session.LoadCalls == 2 && session.SaveCalls == 0,
                "Desktop load was duplicated or implicitly saved chemistry");
            Check(driver.CurrentLabView.EventSequence == beforeLoadRevision,
                "Desktop load routing created a synthetic Native material revision");
            Check(panels.Request(PanelAction.Settings), "Pause → Settings action refused");
            Check(router.Current.ScreenId == ScreenId.Settings, "Settings not visible");
            Check(panels.Request(PanelAction.Back), "Settings Back refused");
            Check(router.Current.ScreenId == ScreenId.Pause && driver.IsNativePaused,
                "Settings Back unpaused the operating authority");
            Check(panels.Request(PanelAction.Review), "Paused Review refused");
            Check(!panels.Request(PanelAction.RestorePortable) &&
                  session.PortableRestoreCalls == 1,
                "Read-only recorded Review unexpectedly admitted live package import.");
            Check(router.Current.ScreenId == ScreenId.Review, "Review route missing");
            Check(router.Current.SupportedActions.Contains("CompareSaved"),
                "Review cannot explicitly compare with a recorded saved checkpoint.");
            Check(router.Current.SupportedActions.Contains("ChooseLocalSave") &&
                  panels.Request(PanelAction.ChooseLocalSave) &&
                  session.LocalNativeChooseCalls == 2 &&
                  !router.Current.SupportedActions.Contains("LoadSelectedSave") &&
                  !panels.Request(PanelAction.LoadSelectedSave) &&
                  session.LocalNativeLoadCalls == 1 &&
                  driver.IsNativePaused,
                "Review selected a recorded baseline by importing or unpausing Native.");
            Check(panels.Request(PanelAction.CompareSaved),
                "Compare action did not use session-owned recorded source data.");
            Check(router.Current.ScreenId == ScreenId.Compare &&
                session.SavedCompareCalls == 1 && session.SaveCalls == 0,
                "Compare mutated Native chemistry, implicitly saved or missed the Compare route.");
            Check(router.Current.SupportedActions.Contains("ExportComparisonCsv") &&
                  panels.CanExportRecordedComparison &&
                  panels.Request(PanelAction.ExportComparisonCsv) &&
                  session.ComparisonCsvCalls == 1 &&
                  panels.LastComparisonCsvFileName == "native-comparison.plv-compare.csv" &&
                  session.LocalNativeLoadCalls == 1 &&
                  driver.IsNativePaused && session.SaveCalls == 0,
                "Recorded comparison CSV must export real selected study without Native import/Save.");
            Check(panels.Request(PanelAction.Review) &&
                router.Current.ScreenId == ScreenId.Review,
                "Comparing recorded data lost the original Review navigation.");

            Check(router.Current.SupportedActions.Contains("ExportCsv"),
                "Recorded Review did not expose a native-observation CSV export.");
            Check(panels.Request(PanelAction.ExportCsv),
                "Recorded Review CSV export did not reach the session writer.");
            Check(session.CsvExportCalls == 1 && session.SaveCalls == 0,
                "CSV export duplicated an operation or implicitly saved Native chemistry.");
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

            // P06.02: if recovery completes while Settings obscures Hold,
            // Back must not resurrect an obsolete/native-inactive Hold screen.
            var interruptedDriver = new CoreDriver();
            var interruptedRouter = new ScreenRouter();
            interruptedRouter.Configure(interruptedDriver);
            var interruptedSessions = new SessionController();
            var interruptedPanels = new LabPanelController();
            interruptedPanels.Configure(interruptedRouter, interruptedDriver, interruptedSessions);
            interruptedDriver.SetHold(true);
            Check(interruptedRouter.Current.ScreenId == ScreenId.Hold,
                "Asynchronous Native hold not shown");
            Check(interruptedPanels.Request(PanelAction.Settings),
                "Held Settings must remain accessible");
            Check(interruptedPanels.Request(PanelAction.Back) &&
                  interruptedRouter.Current.ScreenId == ScreenId.Hold,
                "While still held, Settings Back must restore active Hold");
            Check(interruptedPanels.Request(PanelAction.Settings),
                "Hold Settings second entry rejected");
            interruptedDriver.SetHold(false);
            Check(interruptedPanels.Request(PanelAction.Back) &&
                  interruptedRouter.Current.ScreenId == ScreenId.Lab,
                "Resolved Native Hold was resurrected by Settings Back");
            Check(interruptedSessions.SaveCalls == 0 && interruptedDriver.ContinueCalls == 0 &&
                  interruptedDriver.PauseCalls == 0,
                "Settings Back issued Save, Pause, or Continue without user intent");
            // Reproduce a hold interrupting a permitted Pause -> Settings
            // navigation, not an unsupported direct Lab -> Settings shortcut.
            Check(interruptedPanels.Request(PanelAction.Pause),
                "Pause must be submitted before opening Settings from Lab");
            Check(interruptedPanels.Request(PanelAction.Settings),
                "Paused Settings navigation unavailable");
            interruptedDriver.SetHold(true);
            Check(interruptedRouter.Current.ScreenId == ScreenId.Hold,
                "Native Hold must supersede previously open Settings");
            Check(interruptedPanels.Request(PanelAction.Settings),
                "Interrupted Settings reopen failed");
            interruptedDriver.SetHold(false);
            Check(interruptedPanels.Request(PanelAction.Back) &&
                  interruptedRouter.Current.ScreenId == ScreenId.Pause,
                "Settings interrupted by recovered Hold lost the underlying Native Pause");
            Check(interruptedDriver.IsNativePaused && interruptedDriver.ContinueCalls == 0 &&
                  interruptedDriver.PauseCalls == 1,
                "Hold recovery during Settings unpaused or re-paused Native authority");

            var heldReturnPreviousContinue = driver.ContinueCalls;
            var heldReturnPreviousSaves = session.SaveCalls;
            var heldReturnPreviousPauses = driver.PauseCalls;
            // Source-linked guard: unprepared FocusLoss and CheckpointRestart
            // must choose ResumeSetup, not Continue (which CLOSES stock choice).
            var coreRecoverySource = System.IO.File.ReadAllText(
                "unity/Assets/PharmaLabVR/Runtime/Core/CoreDriver.cs");
            Check(coreRecoverySource.Contains("HoldReason == \"FocusLoss\"") &&
                  coreRecoverySource.Contains("HoldReason == \"TrackingLoss\"") &&
                  coreRecoverySource.Contains("HoldReason == \"CheckpointRestart\"") &&
                  coreRecoverySource.Contains(
                    "requiresSetupRecovery ? \"ResumeSetup\" : \"Continue\""),
                "Unity silently closed new Native stock setup after FocusLoss or restart.");

            // Source-linked sequencing guard: a successful Native resume
            // must publish the new paused/hold status before the router's
            // HoldChanged(false) callback runs, never with the old snapshot.
            var recoveryCore = System.IO.File.ReadAllText(
                "unity/Assets/PharmaLabVR/Runtime/Core/CoreDriver.cs");
            var recoveryMethod = recoveryCore.IndexOf(
                "public bool ResumeAfterTimeHold()", StringComparison.Ordinal);
            var recoveryEnd = recoveryCore.IndexOf(
                "public bool BeginModeChange(", recoveryMethod, StringComparison.Ordinal);
            var acceptedRecovery = recoveryCore.Substring(recoveryMethod,
                recoveryEnd - recoveryMethod);
            Check(acceptedRecovery.IndexOf("PublishSnapshot();", StringComparison.Ordinal) <
                      acceptedRecovery.IndexOf("HoldChanged?.Invoke(false)", StringComparison.Ordinal),
                "Recovered Native paused flag must precede HoldChanged(false) presentation.");

            // RED: a held imported checkpoint that was saved under Pause
            // resumes *running* when Native accepts Continue. Do not display
            // obsolete Pause while Native time/transport are active.
            var resumedDriver = new CoreDriver();
            var resumedRouter = new ScreenRouter();
            resumedRouter.Configure(resumedDriver);
            var resumedSession = new SessionController();
            var resumedPanels = new LabPanelController();
            resumedPanels.Configure(resumedRouter, resumedDriver, resumedSession);
            Check(resumedPanels.Request(PanelAction.Pause) &&
                  resumedRouter.Current.ScreenId == ScreenId.Pause,
                "Cannot simulate paused original Native checkpoint.");
            resumedDriver.SetHold(true);
            Check(resumedRouter.Current.ScreenId == ScreenId.Hold,
                "Paused checkpoint was not held.");
            var resumePauses = resumedDriver.PauseCalls;
            var resumeContinues = resumedDriver.ContinueCalls;
            resumedDriver.CompleteRecovery(remainsPaused: false, needsPreparation: false);
            Check(resumedRouter.Current.ScreenId == ScreenId.Lab &&
                  !resumedDriver.IsNativePaused && !resumedDriver.IsTimeHeld &&
                  resumedDriver.PauseCalls == resumePauses &&
                  resumedDriver.ContinueCalls == resumeContinues &&
                  resumedSession.SaveCalls == 0,
                "Native Continue released saved Pause but router exposed obsolete Pause UI.");
            // Settings can obscure Hold while the exact same recovery
            // finishes. Back must reconcile the CURRENT Native pause flag.
            var settingsRecoveryDriver = new CoreDriver();
            var settingsRecoveryRouter = new ScreenRouter();
            settingsRecoveryRouter.Configure(settingsRecoveryDriver);
            var settingsRecoverySession = new SessionController();
            var settingsRecoveryPanels = new LabPanelController();
            settingsRecoveryPanels.Configure(settingsRecoveryRouter,
                settingsRecoveryDriver, settingsRecoverySession);
            Check(settingsRecoveryPanels.Request(PanelAction.Pause),
                "Settings recovery could not construct paused Native source.");
            settingsRecoveryDriver.SetHold(true);
            Check(settingsRecoveryPanels.Request(PanelAction.Settings),
                "Held Settings admission missing.");
            settingsRecoveryDriver.CompleteRecovery(remainsPaused: false,
                needsPreparation: false);
            Check(settingsRecoveryPanels.Request(PanelAction.Back) &&
                  settingsRecoveryRouter.Current.ScreenId == ScreenId.Lab &&
                  settingsRecoveryDriver.ContinueCalls == 0 &&
                  settingsRecoverySession.SaveCalls == 0,
                "Settings Back restored stale Pause after Native recovered running.");
            // ResumeSetup is the sole valid exception: genuinely unprepared,
            // still Native-paused stock setup should return to the Lab chooser.
            var setupResumeDriver = new CoreDriver { SourceNeedsPreparation = true };
            var setupResumeRouter = new ScreenRouter();
            setupResumeRouter.Configure(setupResumeDriver);
            setupResumeDriver.SetHold(true);
            setupResumeDriver.CompleteRecovery(remainsPaused: true,
                needsPreparation: true);
            Check(setupResumeRouter.Current.ScreenId == ScreenId.Lab &&
                  setupResumeDriver.IsNativePaused &&
                  setupResumeDriver.SourceNeedsPreparation,
                "Unprepared Native ResumeSetup did not return to stock choice.");

            driver.SetHold(true);
            Check(router.Current.ScreenId == ScreenId.Hold, "Native hold not routed");
            Check(!panels.Request(PanelAction.Continue), "Unavailable held Continue falsely succeeded");
            Check(!panels.Request(PanelAction.Measure, "receiver"), "Held Measure falsely succeeded");
            Check(panels.Request(PanelAction.Review), "Recorded Review blocked during Hold");
            Check(!panels.Request(PanelAction.Lab), "Held Review bypassed native hold");
            Check(router.Current.ScreenId == ScreenId.Review, "Denied held return hid review");
            Check(router.Current.SupportedActions.Contains("Hold"),
                "Held recorded Review has no explicit route back to Native recovery.");
            Check(panels.Request(PanelAction.Hold) &&
                  router.Current.ScreenId == ScreenId.Hold &&
                  driver.IsTimeHeld && !driver.NativeRecoveryReady &&
                  session.RecordedReview == null,
                "Return from recorded Review bypassed Native Hold or left stale Review.");
            Check(session.SaveCalls == heldReturnPreviousSaves &&
                  driver.ContinueCalls == heldReturnPreviousContinue &&
                  driver.PauseCalls == heldReturnPreviousPauses,
                "Review-to-Hold navigation saved/continued/recomputed Native authority.");
            Check(!panels.Request(PanelAction.Hold),
                "Native recovery return must not be a generic screen escape.");
            Check(session.SaveCalls == 0, "Hold/Review caused implicit Save");
            Console.WriteLine($"P06.02 headless routing checks: {checks} passed (NOT Unity, Player or Native Core)");
        }
        finally
        {
            SetupPreferencesSelection.Select(initial);
        }
    }
}
