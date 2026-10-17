using UnityEngine;

namespace PharmaLabVR.Tools
{
    // Visual scale only. A graduated burette is read from its meniscus, not
    // from a fabricated pH/quantity widget. Profile: 50 ml in 0.44 m.
    public static class InstrumentMeshBuilder
    {
        private static Material sharedRuntimeInk;
        public static void BuildBuretteGraduations(Transform parent,
            float topLocalY, float verticalSpanMeters, float outerRadiusMeters,
            Material inkMaterial = null)
        {
            if (parent == null || verticalSpanMeters <= 0f || outerRadiusMeters <= 0f)
                throw new System.ArgumentException("A physical scale requires a valid parent and dimensions.");
            var root = parent.Find("BuretteGraduations");
            if (root != null) return; // No duplicates on repeated scene setup.
            root = new GameObject("BuretteGraduations").transform;
            root.SetParent(parent, false);
            if (inkMaterial == null)
            {
                if (sharedRuntimeInk == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    if (shader == null) throw new System.InvalidOperationException("Missing graduation shader.");
                    sharedRuntimeInk = new Material(shader) { color = new Color(0.07f, 0.11f, 0.13f) };
                    if (sharedRuntimeInk.HasProperty("_BaseColor"))
                        sharedRuntimeInk.SetColor("_BaseColor", sharedRuntimeInk.color);
                }
                inkMaterial = sharedRuntimeInk;
            }
            for (var ml = 0; ml <= 50; ++ml)
            {
                var major = ml % 10 == 0;
                var medium = ml % 5 == 0;
                var tick = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tick.name = "Graduation " + ml;
                tick.transform.SetParent(root, false);
                tick.transform.localPosition = new Vector3(outerRadiusMeters, topLocalY -
                    verticalSpanMeters * (ml / 50f), 0f);
                tick.transform.localScale = new Vector3(major ? 0.014f :
                    medium ? 0.010f : 0.005f, 0.0012f, 0.0012f);
                tick.GetComponent<Collider>().enabled = false;
                tick.GetComponent<MeshRenderer>().sharedMaterial = inkMaterial;
                if (!major) continue;
                var label = new GameObject("Burette " + ml + " ml");
                label.transform.SetParent(root, false);
                label.transform.localPosition = new Vector3(outerRadiusMeters + 0.012f,
                    tick.transform.localPosition.y, 0f);
                var text = label.AddComponent<TextMesh>();
                text.text = ml.ToString();
                text.anchor = TextAnchor.MiddleLeft;
                text.alignment = TextAlignment.Left;
                text.characterSize = 0.008f;
                text.fontSize = 40;
                text.color = new Color(0.07f, 0.11f, 0.13f);
            }
        }
    }
}
