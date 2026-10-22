using System;
using PharmaLabVR.Core;
using UnityEngine;

namespace PharmaLabVR.Tools
{
    // A non-authoritative visual subscriber. It never modifies Native volume,
    // material pools, residual composition, scientific results or command order.
    public sealed class NativeMeteredInstrumentVisual : MonoBehaviour
    {
        [SerializeField] private Transform liquidCylinder;
        [SerializeField] private string inventoryId = "source";
        [SerializeField] private bool inventoryIsToolTip;
        [SerializeField] private double capacityM3 = 1e-4;
        [SerializeField] private float maximumHeightM = 0.12f;
        [SerializeField] private float bottomLocalY = 0.013f;
        private CoreDriver driver;
        private Vector3 originalRadialScale;

        public double DisplayedVolumeM3 { get; private set; }

        public void ConfigureRepresentation(Transform fill, string nativeInventoryId, bool toolTip,
                                            double instrumentCapacityM3,
                                            float heightM, float bottomY)
        {
            if (fill == null || string.IsNullOrEmpty(nativeInventoryId) ||
                instrumentCapacityM3 <= 0.0 || heightM <= 0f)
                throw new ArgumentException("A bounded Native instrument geometry is required.");
            liquidCylinder = fill;
            inventoryId = nativeInventoryId;
            inventoryIsToolTip = toolTip;
            capacityM3 = instrumentCapacityM3;
            maximumHeightM = heightM;
            bottomLocalY = bottomY;
            originalRadialScale = fill.localScale;
            Render(0.0);
        }

        // Editor generation cannot persist event delegates. Bind from the
        // actual scene when loaded in a Player, including additive scenes.
        // Never search globally: another lab/Native driver may be active.
        private void OnEnable()
        {
            if (!Application.isPlaying || !gameObject.scene.IsValid()) return;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                var core = root.GetComponentInChildren<CoreDriver>(true);
                if (core == null) continue;
                Configure(core);
                return;
            }
        }

        private void OnDisable()
        {
            if (driver != null) driver.SnapshotChanged -= ApplySnapshot;
            driver = null;
        }

        public void Configure(CoreDriver core)
        {
            if (driver != null) driver.SnapshotChanged -= ApplySnapshot;
            driver = core;
            if (driver != null)
            {
                driver.SnapshotChanged += ApplySnapshot;
                if (driver.Session != null && driver.Session.IsOpen)
                    ApplySnapshot(driver.Session.ReadSnapshot());
            }
        }

        public void ApplySnapshot(string nativeSnapshot)
        {
            if (liquidCylinder == null || string.IsNullOrEmpty(nativeSnapshot)) return;
            var state = JsonUtility.FromJson<Snapshot>(nativeSnapshot);
            double volume = 0.0;
            if (inventoryIsToolTip && state?.tools != null)
            {
                foreach (var tool in state.tools)
                    if (tool != null && tool.id == inventoryId)
                    {
                        volume = tool.tipInventory?.researchAdditiveVolumeM3 ?? 0.0;
                        break;
                    }
            }
            else if (state?.vessels != null)
            {
                foreach (var vessel in state.vessels)
                    if (vessel != null && vessel.id == inventoryId)
                    {
                        volume = vessel.inventory?.researchAdditiveVolumeM3 ?? 0.0;
                        break;
                    }
            }
            Render(volume);
        }

        private void Render(double committedVolumeM3)
        {
            if (liquidCylinder == null) return;
            if (originalRadialScale.x <= 0f || originalRadialScale.z <= 0f)
                originalRadialScale = liquidCylinder.localScale;
            DisplayedVolumeM3 = double.IsNaN(committedVolumeM3) ||
                double.IsInfinity(committedVolumeM3) ? 0.0 :
                Math.Max(0.0, Math.Min(capacityM3, committedVolumeM3));
            var fraction = (float)(DisplayedVolumeM3 / capacityM3);
            var height = maximumHeightM * fraction;
            liquidCylinder.localScale = new Vector3(originalRadialScale.x, height * 0.5f,
                                                    originalRadialScale.z);
            liquidCylinder.localPosition = new Vector3(0f, bottomLocalY + height * 0.5f, 0f);
            liquidCylinder.gameObject.SetActive(fraction > 0f);
        }

        private void OnDestroy()
        {
            if (driver != null) driver.SnapshotChanged -= ApplySnapshot;
        }

        [Serializable] private sealed class Snapshot
        {
            public Vessel[] vessels;
            public Tool[] tools;
        }
        [Serializable] private sealed class Vessel
        {
            public string id;
            public MaterialPool inventory;
        }
        [Serializable] private sealed class Tool
        {
            public string id;
            public MaterialPool tipInventory;
        }
        [Serializable] private sealed class MaterialPool
        {
            public double researchAdditiveVolumeM3;
        }
    }
}
