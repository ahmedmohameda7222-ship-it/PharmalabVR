using UnityEngine;

namespace PharmaLabVR.Tools
{
    public sealed class ToolPresenter : MonoBehaviour
    {
        [SerializeField] private string toolId = "unassigned-tool";
        [SerializeField] private LiquidPresenter liquid;
        [SerializeField] private ValveActuator actuator;
        public string ToolId => toolId;

        public void Configure(string id, LiquidPresenter liquidPresenter, ValveActuator valve)
        {
            toolId = id;
            liquid = liquidPresenter;
            actuator = valve;
        }

        public void ApplyCommittedState(double volumeM3, float actuator01)
        {
            liquid?.SetCommittedVolume(volumeM3);
            actuator?.SetNormalized(actuator01);
        }
    }
}
