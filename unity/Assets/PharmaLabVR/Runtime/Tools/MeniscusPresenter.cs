using System;
using UnityEngine;

namespace PharmaLabVR.Tools
{
    // Presentation-only, concave liquid surface derived exclusively from the
    // committed Native Core volume. Never advances volume or animation time.
    public sealed class MeniscusPresenter : MonoBehaviour
    {
        [SerializeField] private Transform surface;
        [SerializeField] private double capacityM3 = 0.00005;
        [SerializeField] private float maximumHeight = 0.48f;
        [SerializeField] private float baseLocalY = -0.24f;
        [SerializeField] private float radiusMeters = 0.006f;

        public float Fill01 { get; private set; }
        public double CommittedVolumeM3 { get; private set; }
        public Transform Surface => surface;

        public void Configure(Transform parent, double capacity, float height, float baseY,
            float radius)
        {
            if (parent == null || capacity <= 0.0 || height <= 0f || radius <= 0f)
                throw new ArgumentException("Meniscus geometry requires valid physical bounds.");
            capacityM3 = capacity;
            maximumHeight = height;
            baseLocalY = baseY;
            radiusMeters = radius;
            if (surface == null)
            {
                var child = new GameObject("CommittedMeniscus", typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(parent, false);
                surface = child.transform;
            }
            var mesh = new Mesh { name = "ConcaveMeniscus" };
            const int sectors = 24;
            var vertices = new Vector3[sectors + 2];
            var triangles = new int[sectors * 3];
            vertices[0] = new Vector3(0f, -0.0018f, 0f);
            for (var i = 0; i <= sectors; ++i)
            {
                var angle = i * 2f * Mathf.PI / sectors;
                vertices[i + 1] = new Vector3(radiusMeters * Mathf.Cos(angle),
                    0f, radiusMeters * Mathf.Sin(angle));
            }
            for (var i = 0; i < sectors; ++i)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 2;
                triangles[i * 3 + 2] = i + 1;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null && renderer.sharedMaterial == null)
            {
                var material = new Material(shader) { color = new Color(0.66f, 0.82f, 0.84f, 0.78f) };
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", material.color);
                renderer.sharedMaterial = material;
            }
            SetCommittedVolume(CommittedVolumeM3);
        }

        // LiquidPresenter rebinds the native inventory capacity after bootstrap.
        // Do not regenerate renderer meshes or reset the committed parcel.
        public void SetProfile(double capacity, float height, float baseY)
        {
            if (capacity <= 0.0 || height <= 0f || !double.IsFinite(capacity))
                throw new ArgumentException("Invalid native liquid profile.");
            capacityM3 = capacity;
            maximumHeight = height;
            baseLocalY = baseY;
            SetCommittedVolume(CommittedVolumeM3);
        }

        public void SetCommittedVolume(double volumeM3)
        {
            if (double.IsNaN(volumeM3) || double.IsInfinity(volumeM3) || volumeM3 < 0.0)
                throw new ArgumentOutOfRangeException(nameof(volumeM3));
            if (capacityM3 <= 0.0) throw new InvalidOperationException("Meniscus capacity must be positive.");
            CommittedVolumeM3 = Math.Min(volumeM3, capacityM3);
            Fill01 = (float)(CommittedVolumeM3 / capacityM3);
            if (surface == null) return;
            var position = surface.localPosition;
            position.y = baseLocalY + maximumHeight * Fill01;
            surface.localPosition = position;
            surface.gameObject.SetActive(Fill01 > 0f);
        }
    }
}
