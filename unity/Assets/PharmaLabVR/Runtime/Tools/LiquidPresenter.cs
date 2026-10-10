using System;
using UnityEngine;

namespace PharmaLabVR.Tools
{
    public sealed class LiquidPresenter : MonoBehaviour
    {
        [SerializeField] private Transform liquidVisual;
        [SerializeField] private double capacityM3 = 0.00005;
        [SerializeField] private float maximumVisualHeight = 0.18f;
        [SerializeField] private float baseLocalY;

        public double CommittedVolumeM3 { get; private set; }
        public float Fill01 { get; private set; }

        public void Configure(Transform visual, double capacity, float height, float baseY)
        {
            liquidVisual = visual;
            capacityM3 = capacity;
            maximumVisualHeight = height;
            baseLocalY = baseY;
            SetCommittedVolume(CommittedVolumeM3);
        }

        public void SetCommittedVolume(double volumeM3)
        {
            if (double.IsNaN(volumeM3) || double.IsInfinity(volumeM3) || volumeM3 < 0.0)
                throw new ArgumentOutOfRangeException(nameof(volumeM3));
            if (capacityM3 <= 0.0) throw new InvalidOperationException("Liquid capacity must be positive.");
            CommittedVolumeM3 = Math.Min(volumeM3, capacityM3);
            Fill01 = (float)(CommittedVolumeM3 / capacityM3);
            if (liquidVisual == null) return;
            var height = maximumVisualHeight * Fill01;
            var scale = liquidVisual.localScale;
            var filter = liquidVisual.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            var meshHeight = mesh != null ? mesh.bounds.size.y : 1f;
            scale.y = height / (meshHeight > 0f ? meshHeight : 1f);
            liquidVisual.localScale = scale;
            var position = liquidVisual.localPosition;
            position.y = baseLocalY + height * 0.5f;
            liquidVisual.localPosition = position;
            liquidVisual.gameObject.SetActive(Fill01 > 0f);
        }
    }
}
