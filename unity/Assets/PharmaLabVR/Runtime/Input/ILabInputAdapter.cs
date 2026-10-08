using System;
using UnityEngine;

namespace PharmaLabVR.Input
{
    [Serializable]
    public struct LabCaptureEstimate
    {
        public string destinationInventoryId;
        public float fraction;
    }

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
        public string coordinateFrame;
        public string geometryProfileHash;
        public ulong profileRevision;
        public ulong toolRevision;
        public LabCaptureEstimate[] captureFractions;
    }

    public interface ILabInputAdapter
    {
        LabInputSample[] SampleInputs(ulong nowNs);
        void ResetBaselines();
        void SetEnabled(bool value);
    }
}
