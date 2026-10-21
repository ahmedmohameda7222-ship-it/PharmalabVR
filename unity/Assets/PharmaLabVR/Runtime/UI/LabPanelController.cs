using System.Linq;
using PharmaLabVR.Core;
using PharmaLabVR.Input;
using PharmaLabVR.Session;
using UnityEngine;

namespace PharmaLabVR.UI
{
    public enum PanelAction { Home, EnterDesktop, EnterVR, ConfirmSetup, Lab, Inspect, Measure, Pause, Hold, Review, Notebook, AddNotebookNote, ExportPortable, ExportCsv, CompareSaved, Settings, Save, Continue, Back, ReleaseTool, ParkTool, Load, Recenter, SnapLeft, SnapRight, PrepareResearchStock, PrepareRinseWater, PipetteAspirate, PipetteDispense, PipetteRinse, BottlePour, BottleRinse, PrepareReserveStock, PipetteSwitchReserve, BottleRefill, FillBurette, SelectResearchLesson, RecordCurrentPH, RecordResearchPrediction }

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
        public bool CanPipetteDispense => sessions?.CanPipetteDispense == true;
        public bool CanFillBurette => sessions?.CanFillBurette == true;
        public double PipetteTipVolumeM3 => sessions?.PipetteTipVolumeM3 ?? 0.0;
        public string LastError => sessions?.LastError;
        public string LastInstrumentReceipt => sessions?.LastInstrumentReceipt;
        public string NotebookSummary => sessions?.NotebookSummary ?? "Research notebook unavailable.";
        public string ResearchLessonSummary => sessions?.ResearchLessonSummary ??
            "Research lesson unavailable.";
        public bool HasResearchLesson => sessions?.HasResearchLesson == true;
        public bool CanRecordCurrentNativePH => sessions?.CanRecordCurrentNativePH == true;
        public bool CanRecordResearchPrediction => sessions?.CanRecordResearchPrediction == true;
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
                case PanelAction.BottleRinse:
                    return sessions.TryBottleWaterRinse();
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
                    router.Show(ScreenId.Review);
                    return router.Current.ScreenId == ScreenId.Review;
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
                case PanelAction.Save:
                    return sessions.Save();
                case PanelAction.ExportPortable:
                    return sessions.ExportPortableSession();
                case PanelAction.ExportCsv:
                    return sessions.ExportRecordedCsv();
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
