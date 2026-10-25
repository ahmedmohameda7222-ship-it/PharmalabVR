using System;
using PharmaLabVR.Core;
using PharmaLabVR.Tools;
using UnityEngine;

namespace PharmaLabVR.Input
{
    // Physical mouth at the top of the actual burette body. Only admits a
    // *request* for the existing revision-checked Native TransferFixed command.
    public sealed class ResearchBuretteInletTarget : MonoBehaviour
    {
        [SerializeField] private Transform inletEntry;
        [SerializeField, Min(0.001f)] private float mouthRadiusMetres = 0.018f;
        [SerializeField] private string nativeBuretteToolId = "research-tool";
        public string NativeInventoryId => "burette";

        public void Configure(Transform entry)
        {
            if (entry == null || !entry.IsChildOf(transform))
                throw new ArgumentException("The inlet must belong to the physical burette.", nameof(entry));
            inletEntry = entry;
        }

        public bool AdmitsBottle(Transform stockBottle, bool buretteParked, CoreSession native)
        {
            if (!isActiveAndEnabled || inletEntry == null ||
                stockBottle == null || native == null || !native.IsOpen)
                return false;
            var outlet = stockBottle.Find("OutletExit");
            var source = stockBottle.GetComponent<BottlePourActuator>();
            var visualValve = GetComponent<ValveActuator>();
            if (outlet == null || source == null || visualValve == null)
                return false;
            var up = transform.up;
            var delta = outlet.position - inletEntry.position;
            var above = Vector3.Dot(delta, up);
            var radial = delta - up * above;
            var nativeValveClosed = NativeValveClosed(native);
            return ResearchBuretteInletAdmission.MayPrime(
                Vector3.Dot(up, Vector3.up),
                Vector3.Dot(outlet.TransformDirection(Vector3.down), -up),
                above, radial.sqrMagnitude, mouthRadiusMetres, buretteParked,
                nativeValveClosed && visualValve.NormalizedOpening <= 0.01f);
        }

        private bool NativeValveClosed(CoreSession session)
        {
            try
            {
                var state = JsonUtility.FromJson<NativeTools>(session.ReadSnapshot());
                if (state?.tools == null) return false;
                foreach (var tool in state.tools)
                    if (tool != null && tool.id == nativeBuretteToolId)
                        return !float.IsNaN(tool.actuator01) &&
                            !float.IsInfinity(tool.actuator01) &&
                            tool.actuator01 >= 0f && tool.actuator01 <= 0.01f;
            }
            catch (Exception) { }
            return false;
        }

        [Serializable] private sealed class NativeTools { public NativeTool[] tools; }
        [Serializable] private sealed class NativeTool
        {
            public string id;
            public float actuator01;
        }
    }
}
