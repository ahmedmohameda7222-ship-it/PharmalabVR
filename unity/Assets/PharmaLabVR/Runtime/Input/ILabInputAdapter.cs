using System;
using UnityEngine;

namespace PharmaLabVR.Input
{
    [Serializable]
    public struct LabInputSample
    {
        public string toolId;
        public ulong sampleSequence;
        public ulong captureMonotonicNs;
        public Vector3 positionMetres;
        public Quaternion rotation;
        public bool trackingValid;
        public float actuator01;
        public string geometryProfileHash;
    }

    public interface ILabInputAdapter
    {
        LabInputSample[] SampleInputs(ulong nowNs);
        void ResetBaselines();
        void SetEnabled(bool value);
    }
}
