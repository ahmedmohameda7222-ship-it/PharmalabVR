using System.Linq;
using PharmaLabVR.Core;
using PharmaLabVR.Input;
using PharmaLabVR.Session;
using UnityEngine;

namespace PharmaLabVR.UI
{
    public enum PanelAction { NewStudy, ExportComparisonCsv, Home, EnterDesktop, EnterVR, ConfirmSetup, Lab, Inspect, Measure, Pause, Hold, Review, Notebook, AddNotebookNote, ExportPortable, ExportCsv, CompareSaved, Settings, Save, Continue, Back, ReleaseTool, ParkTool, Load, ChooseLocalSave, LoadSelectedSave, SaveCopy, Recenter, SnapLeft, SnapRight, PrepareResearchStock, PrepareRinseWater, PipetteAspirate, PipetteDispense, PipetteRinse, BottlePour, BottleRinse, PrepareReserveStock, PipetteSwitchReserve, PipetteSwitchPrimary, BottleRefill, FillBurette, SelectResearchLesson, RecordCurrentPH, RecordResearchPrediction, RecordResearchExplanation, PreviewPortableArchive, ChoosePortableArchive, RestorePortable, NextInstructorAnnotation, RecordInstructorFeedback, SelectWorkspace, ExitFailedLab, SaveAndReviewStudy, Learn }

    public sealed class LabPanelController : MonoBehaviour
    {
        [SerializeField] private ScreenRouter router;
        [SerializeField] private CoreDriver driver;
        [SerializeField] private SessionController sessions;
        [SerializeField] private BootMenu boot;
        public RecordedSessionReview CurrentReview => sessions?.RecordedReview;
        public bool CanReleaseTool => sessions?.CanReleaseTool == true;
        public bool CanParkTool => sessions?.CanParkTool == true;
        public bool CanLoadSavedSession => sessions?.CanLoadSavedSession == true;
        public bool CanSaveNewLocalCopy => sessions?.CanSaveNewLocalCopy == true;
        public bool CanStartIndependentStudy => sessions?.CanStartIndependentStudy == true;
        public bool CanChooseLocalNativeSave =>
            sessions?.CanChooseLocalNativeSave == true;
        public bool CanLoadSelectedLocalNativeSave =>
            sessions?.CanLoadSelectedLocalNativeSave == true;
        public string SelectedLocalNativeSaveName => sessions?.SelectedLocalNativeSaveName;
        public string SelectedLocalNativeSavePreview => sessions?.SelectedLocalNativeSavePreview;
        public string ComparisonBaselineName => sessions?.ComparisonBaselineName ?? "none";
        public bool CanExportRecordedComparison =>
            sessions?.CanExportRecordedComparison == true;
        public string LastComparisonCsvFileName =>
            string.IsNullOrEmpty(sessions?.LastComparisonCsvExportPath) ? null :
            System.IO.Path.GetFileName(sessions.LastComparisonCsvExportPath);
        public string ActiveLocalSessionName => sessions?.ActiveLocalSessionName;
        public bool CanPipetteDispense => sessions?.CanPipetteDispense == true;
        public bool CanFillBurette => sessions?.CanFillBurette == true;
        public double PipetteTipVolumeM3 => sessions?.PipetteTipVolumeM3 ?? 0.0;
        public string LastError => sessions?.LastError;
        public string LastInstrumentReceipt => sessions?.LastInstrumentReceipt;
        public string NotebookSummary => sessions?.NotebookSummary ?? "Research notebook unavailable.";
        public string LearningReviewSummary => sessions?.LearningReviewSummary ?? "Student notes unavailable.";
        public string LearningReviewCompact => sessions?.LearningReviewCompact ?? "Student notes unavailable.";
        public bool HasLearnerCsvExport => !string.IsNullOrEmpty(sessions?.LastLearnerCsvExportPath);
        public bool CanPreviewLocalPortableArchive => sessions?.CanPreviewLocalPortableArchive == true;
        public bool CanChooseLocalPortableArchive => sessions?.CanChooseLocalPortableArchive == true;
        public bool CanRestoreLocalPortableSession =>
            sessions?.CanRestoreLocalPortableSession == true;
        public string LastPortableImportSummary => sessions?.LastPortableImportSummary;
        public bool PipetteUsingReserve =>
            sessions?.ActivePipetteStockInventoryId == "reserve-stock";
        public string LastPortableArchiveFileName =>
            string.IsNullOrEmpty(sessions?.LastPortableArchivePath) ? null :
            System.IO.Path.GetFileName(sessions.LastPortableArchivePath);
        public string SelectedArchiveFileName => sessions?.SelectedArchiveFileName;
        public string InstructorPortableSummary => sessions?.InstructorPortableSummary ??
            "No offline instructor preview selected.";
        public string InstructorPortableCompactSummary =>
            sessions?.InstructorPortableCompactSummary ??
            "No offline instructor preview selected.";
        public bool CanNextInstructorAnnotation =>
            sessions?.CanNextInstructorAnnotation == true;
        public bool CanRecordInstructorFeedback =>
            sessions?.CanRecordInstructorFeedback == true;
        public bool CanFinishNativeStudy =>
            sessions?.CanFinishNativeStudy == true;
        public bool CanExportCurrentReviewPortable =>
            sessions?.CanExportCurrentReviewPortable == true;
        public bool CanExitFailedNativeLab =>
            sessions?.CanExitFailedNativeLab == true;
        public string ResearchLessonSummary => sessions?.ResearchLessonSummary ??
            "Research lesson unavailable.";
        public string ResearchLessonCompact => sessions?.ResearchLessonCompact ??
            "Research evidence unavailable.";
        public bool HasResearchLesson => sessions?.HasResearchLesson == true;
        public bool CanRecordCurrentNativePH => sessions?.CanRecordCurrentNativePH == true;
        public bool CanRecordResearchPrediction => sessions?.CanRecordResearchPrediction == true;
        public bool CanRecordResearchExplanation => sessions?.CanRecordResearchExplanation == true;
        public string ResearchPredictionSummary => sessions?.ResearchPredictionSummary ??
            "Student prediction unavailable.";

        public void ConfigureBoot(ScreenRouter screenRouter, BootMenu bootMenu)
        {
            router = screenRouter;
            boot = bootMenu;
        }

        public void Configure(ScreenRouter screenRouter, CoreDriver coreDriver, SessionController sessionController)
        {
            router = screenRouter;
            driver = coreDriver;
            sessions = sessionController;
        }

        public bool Request(PanelAction action, string entityId = null, double? concentrationMolPerL = null)
        {
            // The ONLY permitted action without a live Native session:
            // explicit recovery from a reported scientific startup failure.
            // All ordinary actions retain the normal ScreenRouter+Native gate.
            if (boot == null && action == PanelAction.ExitFailedLab)
                return sessions?.ExitFailedNativeLab() == true;
            if (boot != null)
            {
                if (router?.Current == null || !router.Current.SupportedActions.Contains(action.ToString())) return false;
                switch (action)
                {
                    case PanelAction.EnterDesktop:
                        boot.ChooseMode(ApplicationMode.Desktop);
                        return router.Current.ScreenId == ScreenId.Setup;
                    case PanelAction.EnterVR:
                        boot.ChooseMode(ApplicationMode.VirtualReality);
                        return router.Current.ScreenId == ScreenId.Setup;
                    case PanelAction.Home:
                        router.Show(ScreenId.Home);
                        return router.Current.ScreenId == ScreenId.Home;
                    case PanelAction.ConfirmSetup:
                        return boot.ConfirmSetup();
                    default:
                        return false;
                }
            }
            if (router?.Current == null || driver?.Session == null || sessions == null ||
                !router.Current.SupportedActions.Contains(action.ToString())) return false;
            switch (action)
            {
                case PanelAction.PrepareResearchStock:
                    return !string.IsNullOrEmpty(entityId) &&
                        sessions.SelectStock(entityId, entityId == "water" ? 0.0 :
                            concentrationMolPerL ?? 0.1);
                case PanelAction.PrepareReserveStock:
                    return !string.IsNullOrEmpty(entityId) &&
                        sessions.TryPrepareResearchReserveStock(entityId, entityId == "water" ? 0.0 :
                            concentrationMolPerL ?? 0.1);
                case PanelAction.PipetteSwitchReserve:
                    return sessions.TryPipetteSwitchReserve();
                case PanelAction.PipetteSwitchPrimary:
                    return sessions.TryPipetteSwitchPrimary();
                case PanelAction.BottleRefill:
                    return sessions.TryBottleRefillFromReserve();
                case PanelAction.PrepareRinseWater:
                    return sessions.TryPrepareResearchRinseWater();
                case PanelAction.PipetteAspirate:
                    return sessions.TryPipetteAspirate();
                case PanelAction.PipetteDispense:
                    return sessions.CanPipetteDispense && sessions.TryPipetteDispense();
                case PanelAction.PipetteRinse:
                    return sessions.TryPipetteWaterRinse();
                case PanelAction.BottlePour:
                    return sessions.TryBottleMeasuredPour();
                case PanelAction.FillBurette:
                    return sessions.TryFillBuretteFromBottle();
                case PanelAction.SelectResearchLesson:
                    return sessions.SelectResearchLesson(entityId);
                case PanelAction.RecordResearchPrediction:
                    return sessions.RecordResearchPrediction(entityId);
                case PanelAction.RecordResearchExplanation:
                    return sessions.RecordResearchExplanation(entityId);
                case PanelAction.BottleRinse:
                    return sessions.TryBottleWaterRinse();
                case PanelAction.Learn:
                    if (string.IsNullOrEmpty(entityId)) return false;
                    router.Show(ScreenId.Learn, entityId);
                    return router.Current.ScreenId == ScreenId.Learn &&
                        router.Current.SelectedEntityId == entityId;
                case PanelAction.Inspect:
                case PanelAction.Measure:
                    if (string.IsNullOrEmpty(entityId) || !driver.SelectMeasurementVessel(entityId)) return false;
                    router.Show(action == PanelAction.Inspect ? ScreenId.Inspect : ScreenId.Measure, entityId);
                    return router.Current.SelectedEntityId == entityId;
                case PanelAction.Pause:
                    if (!driver.PauseSession()) return false;
                    router.Show(ScreenId.Pause);
                    return router.Current.ScreenId == ScreenId.Pause;
                case PanelAction.Lab:
                    if (driver.IsTimeHeld) return false;
                    if (driver.IsNativePaused && !driver.ContinueSession()) return false;
                    if (sessions.RecordedReview != null) sessions.CloseReview();
                    router.Show(ScreenId.Lab);
                    return router.Current.ScreenId == ScreenId.Lab;
                case PanelAction.Review:
                    if (!sessions.OpenReview()) return false;
                    // The recorded export must still have the SAME event
                    // and Native branch that admitted the learner evidence.
                    if (!sessions.ConfirmExactNativeReview(
                            validatedBranch, validatedEvent))
                        return false;
                    router.Show(ScreenId.Review);
                    return router.Current.ScreenId == ScreenId.Review;
                case PanelAction.Hold:
                    // Reviewing while Native is held must never trap a user
                    // away from Continue/Save/recovery. This is presentation
                    // navigation only, never a Native Continue or Save.
                    if (!driver.IsTimeHeld || router.Current.ScreenId != ScreenId.Review)
                        return false;
                    sessions.CloseReview();
                    router.Show(ScreenId.Hold);
                    return router.Current.ScreenId == ScreenId.Hold;
                case PanelAction.Notebook:
                    router.Show(ScreenId.Notebook);
                    return router.Current.ScreenId == ScreenId.Notebook;
                case PanelAction.AddNotebookNote:
                    return sessions.AppendNotebookNote(entityId);
                case PanelAction.RecordCurrentPH:
                    return sessions.RecordCurrentNativeMeasurement();
                case PanelAction.Settings:
                    router.Show(ScreenId.Settings);
                    return router.Current.ScreenId == ScreenId.Settings;
                case PanelAction.Back:
                    return router.ReturnFromSettings();
                case PanelAction.SaveAndReviewStudy:
                    // Explicit Notebook action: durable Native save MUST
                    // succeed before opening read-only Review. A bare Review
                    // elsewhere retains its no-implicit-save contract.
                    if (router.Current.ScreenId != ScreenId.Notebook ||
                        driver.IsTimeHeld || driver.IsNativePaused ||
                        !sessions.CanFinishNativeStudy)
                        return false;
                    var validatedBranch = driver.CurrentNativeBranchId;
                    var validatedEvent = driver.CurrentLabView?.EventSequence;
                    if (!sessions.Save()) return false;
                    // Native work may publish a newer observation while
                    // durable disk I/O is underway. Refuse stale Review
                    // instead of promoting a different event's evidence.
                    if (!sessions.ConfirmFinishNativeStudyAfterSave(
                            validatedBranch, validatedEvent))
                        return false;
                    if (!sessions.OpenReview()) return false;
                    router.Show(ScreenId.Review);
                    return router.Current.ScreenId == ScreenId.Review;
                case PanelAction.Save:
                    return sessions.Save();
                case PanelAction.SaveCopy:
                    return sessions.SaveAsNewLocalCopy();
                case PanelAction.NewStudy:
                    return sessions.StartIndependentStudy();
                case PanelAction.ExportPortable:
                    return sessions.ExportPortableSession();
                case PanelAction.ExportCsv:
                    return sessions.ExportRecordedCsv();
                case PanelAction.ExportComparisonCsv:
                    return sessions.ExportCurrentComparisonCsv();
                case PanelAction.PreviewPortableArchive:
                    return sessions.PreviewLocalPortableArchive(entityId);
                case PanelAction.ChoosePortableArchive:
                    return sessions.ChooseNextLocalArchive();
                case PanelAction.RestorePortable:
                    return sessions.RestoreSelectedLocalPortableSession();
                case PanelAction.NextInstructorAnnotation:
                    return sessions.NextInstructorAnnotation();
                case PanelAction.RecordInstructorFeedback:
                    return sessions.RecordInstructorFeedback(entityId);
                case PanelAction.SelectWorkspace:
                    // Local presentation role only. No academic identity,
                    // authorization, enrollment or grade originates here.
                    if (entityId != "Student" && entityId != "Instructor" &&
                        entityId != "Researcher" && entityId != "Expert")
                        return false;
                    router.SetWorkspace(entityId);
                    return router.Current.Workspace == entityId;
                case PanelAction.CompareSaved:
                    if (!sessions.OpenSavedComparison()) return false;
                    router.Show(ScreenId.Compare);
                    return router.Current.ScreenId == ScreenId.Compare;
                case PanelAction.ReleaseTool:
                    return sessions.ReleaseToolFromDock();
                case PanelAction.ParkTool:
                    return sessions.ParkToolAtDock();
                case PanelAction.Load:
                    return sessions.LoadSavedSession();
                case PanelAction.ChooseLocalSave:
                    return sessions.ChooseNextLocalNativeSave();
                case PanelAction.LoadSelectedSave:
                    return sessions.LoadSelectedLocalNativeSave();
                case PanelAction.Recenter:
                    return sessions.TryRecenterView();
                case PanelAction.SnapLeft:
                    return sessions.TrySnapTurn(-1);
                case PanelAction.SnapRight:
                    return sessions.TrySnapTurn(1);
                case PanelAction.Continue:
                    return sessions.ContinueHeldSession();
                default:
                    return false;
            }
        }

    }
}
