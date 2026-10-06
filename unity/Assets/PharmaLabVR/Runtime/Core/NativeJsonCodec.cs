using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PharmaLabVR.Input;

namespace PharmaLabVR.Core
{
    public static class NativeJsonCodec
    {
        public static string EncodeInputBatch(IReadOnlyList<LabInputSample> samples)
        {
            var json = new StringBuilder("[");
            for (var index = 0; index < samples.Count; index++)
            {
                if (index > 0) json.Append(',');
                var sample = samples[index];
                json.Append("{\"toolId\":\"").Append(Escape(sample.toolId)).Append("\",")
                    .Append("\"sampleSequence\":\"").Append(sample.sampleSequence).Append("\",")
                    .Append("\"captureMonotonicNs\":\"").Append(sample.captureMonotonicNs).Append("\",")
                    .Append("\"positionMetres\":[").Append(Number(sample.positionMetres.x)).Append(',').Append(Number(sample.positionMetres.y)).Append(',').Append(Number(sample.positionMetres.z)).Append("],")
                    .Append("\"rotation\":[").Append(Number(sample.rotation.x)).Append(',').Append(Number(sample.rotation.y)).Append(',').Append(Number(sample.rotation.z)).Append(',').Append(Number(sample.rotation.w)).Append("],")
                    .Append("\"trackingValid\":").Append(sample.trackingValid ? "true" : "false").Append(',')
                    .Append("\"actuator01\":").Append(Number(sample.actuator01)).Append(',')
                    .Append("\"geometryProfileHash\":\"").Append(Escape(sample.geometryProfileHash)).Append("\"}");
            }
            return json.Append(']').ToString();
        }

        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Escape(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
