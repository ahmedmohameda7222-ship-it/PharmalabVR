using System;
using PharmaLabVR.Core;
using PharmaLabVR.Lab;
using UnityEngine;

namespace PharmaLabVR.Tools
{
    // Product ABI bridge. Read Native tool revisions for each intent, never cache
    // or simulate any chemical inventory inside Unity.
    internal sealed class MeteredNativeTool
    {
        private readonly CoreSession session;
        private readonly LabCommandService commands;

        public MeteredNativeTool(CoreSession session)
        {
            if (session == null || !session.IsOpen)
                throw new InvalidOperationException("An open Native session is required.");
            this.session = session;
            commands = new LabCommandService(session);
        }

        public void EnsurePlaced(string id, string source, string sink, string profile)
        {
            var tool = Find(id);
            if (tool != null)
            {
                if (tool.geometryProfileHash != profile || tool.overflowSinkId != sink)
                    throw new InvalidOperationException("Native tool profile or sink mismatch.");
                return;
            }
            var receipt = commands.Submit("PlaceTool",
                ResearchInstrumentPayloads.Place(id, source, sink, profile),
                commands.CaptureTouchedRevisions());
            if (!receipt.Accepted)
                throw new InvalidOperationException("Native tool registration refused: " + receipt.Code);
        }

        public LabCommandReceipt Aspirate(string id, double quantity)
        {
            var tool = Require(id, ResearchInstrumentPayloads.PipetteProfile);
            return commands.Submit("AspiratePipette",
                ResearchInstrumentPayloads.Aspirate(id, quantity, tool.inventoryRevision),
                commands.CaptureTouchedRevisions(tool.sourceInventoryId));
        }

        public LabCommandReceipt Dispense(string id, string receiver, double quantity)
        {
            var tool = Require(id, ResearchInstrumentPayloads.PipetteProfile);
            return commands.Submit("DispensePipette",
                ResearchInstrumentPayloads.DispenseOrPour(id, receiver, quantity, tool.inventoryRevision),
                commands.CaptureTouchedRevisions(receiver));
        }

        public LabCommandReceipt SwitchSource(string id, string nextSource)
        {
            var tool = Require(id, ResearchInstrumentPayloads.PipetteProfile);
            return commands.Submit("SwitchPipetteSource",
                ResearchInstrumentPayloads.SwitchPipetteSource(id, nextSource, tool.inventoryRevision),
                commands.CaptureTouchedRevisions(nextSource));
        }

        public LabCommandReceipt Pour(string id, string receiver, double quantity)
        {
            var tool = Require(id, ResearchInstrumentPayloads.BottleProfile);
            return commands.Submit("PourBottle",
                ResearchInstrumentPayloads.DispenseOrPour(id, receiver, quantity, tool.inventoryRevision),
                commands.CaptureTouchedRevisions(tool.sourceInventoryId, receiver));
        }

        public LabCommandReceipt RefillBottle(string id, string preparedReserveId, double quantity)
        {
            var tool = Require(id, ResearchInstrumentPayloads.BottleProfile);
            return commands.Submit("TransferFixed",
                ResearchInstrumentPayloads.RefillFromPreparedStock(
                    preparedReserveId, tool.sourceInventoryId, tool.overflowSinkId, quantity),
                commands.CaptureTouchedRevisions(preparedReserveId, tool.sourceInventoryId));
        }

        public LabCommandReceipt Rinse(string id, string profile, string waterSource, string sink, double quantity)
        {
            var tool = Require(id, profile);
            return commands.Submit("RinseTool",
                ResearchInstrumentPayloads.Rinse(id, waterSource, sink, quantity, tool.inventoryRevision),
                commands.CaptureTouchedRevisions(waterSource));
        }

        public double TipVolume(string id) => Find(id)?.tipInventory?.researchAdditiveVolumeM3 ?? 0.0;

        private Tool Require(string id, string profile)
        {
            var tool = Find(id);
            if (tool == null || tool.geometryProfileHash != profile ||
                !ulong.TryParse(tool.inventoryRevision, out _))
                throw new InvalidOperationException("Native tool/revision missing: " + id);
            return tool;
        }

        private Tool Find(string id)
        {
            var state = JsonUtility.FromJson<ToolState>(session.ReadSnapshot());
            if (state?.tools == null) throw new InvalidOperationException("Native tool inventory unavailable.");
            foreach (var tool in state.tools)
                if (tool != null && tool.id == id) return tool;
            return null;
        }

        [Serializable] private sealed class ToolState { public Tool[] tools; }
        [Serializable] private sealed class Tool
        {
            public string id;
            public string sourceInventoryId;
            public string overflowSinkId;
            public string geometryProfileHash;
            public string inventoryRevision;
            public Material tipInventory;
        }
        [Serializable] private sealed class Material { public double researchAdditiveVolumeM3; }
    }

    public sealed class PipetteActuator : MonoBehaviour
    {
        [SerializeField] private CoreDriver driver;
        [SerializeField] private string toolId = "research-pipette";
        [SerializeField] private string sourceId = "source";
        public string NativeToolId => toolId;
        public string ConfiguredSourceInventoryId => sourceId;
        [SerializeField] private string receiverId = "receiver";
        [SerializeField] private string sinkId = "spill";
        [SerializeField] private string waterId = "rinse-water";
        [SerializeField] private string wasteId = "waste";
        private CoreSession testSession;

        public void Configure(CoreDriver owner) { driver = owner; testSession = null; }
        public void Configure(CoreSession session) { driver = null; testSession = session; }
        private MeteredNativeTool Native => new MeteredNativeTool(testSession ?? driver?.Session);
        public double RepresentedTipVolumeM3 => Native.TipVolume(toolId);

        public LabCommandReceipt Aspirate(double quantityM3)
        {
            var native = Native;
            native.EnsurePlaced(toolId, sourceId, sinkId, ResearchInstrumentPayloads.PipetteProfile);
            return native.Aspirate(toolId, quantityM3);
        }

        public LabCommandReceipt Dispense(double quantityM3) => Native.Dispense(toolId, receiverId, quantityM3);
        public LabCommandReceipt SwitchPreparedSource(string alreadyPreparedId)
        {
            if (alreadyPreparedId != "source" && alreadyPreparedId != "reserve-stock")
                throw new ArgumentException(
                    "Pipette source must be a represented prepared stock.", nameof(alreadyPreparedId));
            var native = Native;
            // A first-ever physical immersion can select prepared reserve
            // without inventing an aspirate. Native PlaceTool only registers
            // the empty instrument, then SwitchPipetteSource is revision-gated.
            native.EnsurePlaced(toolId, sourceId, sinkId,
                ResearchInstrumentPayloads.PipetteProfile);
            return native.SwitchSource(toolId, alreadyPreparedId);
        }
        public LabCommandReceipt Rinse(double quantityM3)
        {
            var native = Native;
            // RinseTool is revision-gated and cannot target an unregistered
            // Native tool. A first-use wash places the real empty instrument;
            // it does not prefill its tip or fabricate solvent in Unity.
            native.EnsurePlaced(toolId, sourceId, sinkId,
                ResearchInstrumentPayloads.PipetteProfile);
            return native.Rinse(toolId, ResearchInstrumentPayloads.PipetteProfile,
                waterId, wasteId, quantityM3);
        }
    }
}
