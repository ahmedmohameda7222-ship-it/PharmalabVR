using PharmaLabVR.Core;
using PharmaLabVR.Lab;
using UnityEngine;

namespace PharmaLabVR.Tools
{
    // Research bottle uses an explicit measured Native transfer, never a valve.
    public sealed class BottlePourActuator : MonoBehaviour
    {
        [SerializeField] private CoreDriver driver;
        [SerializeField] private string toolId = "research-stock-bottle";
        [SerializeField] private string sourceId = "source";
        [SerializeField] private string receiverId = "receiver";
        [SerializeField] private string sinkId = "spill";
        [SerializeField] private string waterId = "rinse-water";
        [SerializeField] private string wasteId = "waste";
        private CoreSession testSession;

        public void Configure(CoreDriver owner) { driver = owner; testSession = null; }
        public void Configure(CoreSession session) { driver = null; testSession = session; }
        private MeteredNativeTool Native => new MeteredNativeTool(testSession ?? driver?.Session);
        public double RepresentedFilmVolumeM3 => Native.TipVolume(toolId);

        public LabCommandReceipt Pour(double quantityM3)
        {
            var native = Native;
            native.EnsurePlaced(toolId, sourceId, sinkId, ResearchInstrumentPayloads.BottleProfile);
            return native.Pour(toolId, receiverId, quantityM3);
        }

        public LabCommandReceipt RefillFromPreparedStock(string preparedSourceId, double quantityM3)
        {
            var native = Native;
            native.EnsurePlaced(toolId, sourceId, sinkId, ResearchInstrumentPayloads.BottleProfile);
            return native.RefillBottle(toolId, preparedSourceId, quantityM3);
        }

        public LabCommandReceipt Rinse(double quantityM3) =>
            Native.Rinse(toolId, ResearchInstrumentPayloads.BottleProfile, waterId, wasteId, quantityM3);
    }
}
