using System;
using UnityEngine;

namespace PharmaLabVR.Tools
{
    public sealed class ValveActuator : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float normalizedOpening;
        [SerializeField] private Transform visualHandle;
        [SerializeField] private float maximumRotationDegrees = 90f;
        public event Action<float> Changed;
        public float NormalizedOpening => normalizedOpening;

        public void SetNormalized(float value)
        {
            var next = Mathf.Clamp01(value);
            if (Mathf.Approximately(next, normalizedOpening)) return;
            normalizedOpening = next;
            if (visualHandle != null)
                visualHandle.localRotation = Quaternion.Euler(0f, 0f, -maximumRotationDegrees * normalizedOpening);
            Changed?.Invoke(normalizedOpening);
        }

        public void Close() => SetNormalized(0f);
    }
}
