using System;
using System.Linq;
using PharmaLabVR.Core;
using PharmaLabVR.Session;
using UnityEngine;

namespace PharmaLabVR.UI
{
    public enum PanelAction { Lab, Inspect, Measure, Pause, Hold, Review, Settings, Save, Continue }

    public sealed class LabPanelController : MonoBehaviour
    {
        [SerializeField] private ScreenRouter router;
        [SerializeField] private CoreDriver driver;
        [SerializeField] private SessionController sessions;

        public void Configure(ScreenRouter screenRouter, CoreDriver coreDriver, SessionController sessionController)
        {
            router = screenRouter;
            driver = coreDriver;
            sessions = sessionController;
        }

        public bool Request(PanelAction action, string entityId = null)
        {
            if (router?.Current == null || driver?.Session == null || sessions == null ||
                !router.Current.SupportedActions.Contains(action.ToString())) return false;
            switch (action)
            {
                case PanelAction.Inspect:
                case PanelAction.Measure:
                    if (string.IsNullOrEmpty(entityId) || !driver.SelectMeasurementVessel(entityId)) return false;
                    router.Show(action == PanelAction.Inspect ? ScreenId.Inspect : ScreenId.Measure, entityId);
                    return router.Current.SelectedEntityId == entityId;
                case PanelAction.Pause:
                    if (!Accepted(driver.Session.SubmitOrdered("Pause"))) return false;
                    router.Show(ScreenId.Pause);
                    return true;
                case PanelAction.Lab:
                    if (router.Current.ScreenId == ScreenId.Pause &&
                        !Accepted(driver.Session.SubmitOrdered("Continue"))) return false;
                    router.Show(ScreenId.Lab);
                    return true;
                case PanelAction.Review:
                    if (sessions.RecordedReview == null) return false;
                    router.Show(ScreenId.Review);
                    return true;
                case PanelAction.Settings:
                    router.Show(ScreenId.Settings);
                    return true;
                case PanelAction.Save:
                    return sessions.Save();
                case PanelAction.Continue:
                    return sessions.ContinueHeldSession();
                default:
                    return false;
            }
        }

        private static bool Accepted(string result)
        {
            var outcome = JsonUtility.FromJson<CommandOutcome>(result);
            return outcome != null && outcome.accepted;
        }

        [Serializable] private sealed class CommandOutcome { public bool accepted; }
    }
}
