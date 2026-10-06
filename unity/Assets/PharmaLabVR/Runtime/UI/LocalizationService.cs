using System;
using System.Collections.Generic;
using UnityEngine;

namespace PharmaLabVR.UI
{
    public sealed class LocalizationService
    {
        private readonly Dictionary<string, string> values = new();
        public string Locale { get; private set; } = "en";

        public void Load(string locale, TextAsset table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            values.Clear();
            var wrapper = JsonUtility.FromJson<Entries>(ConvertObjectToEntries(table.text));
            foreach (var entry in wrapper.items) values[entry.key] = entry.value;
            Locale = locale;
        }

        public string Get(string key) => values.TryGetValue(key, out var value) ? value : $"[{key}]";

        [Serializable] private sealed class Entry { public string key; public string value; }
        [Serializable] private sealed class Entries { public Entry[] items; }
        private static string ConvertObjectToEntries(string json)
        {
            var pairs = new List<string>();
            var trimmed = json.Trim().TrimStart('{').TrimEnd('}');
            foreach (var pair in trimmed.Split(','))
            {
                var split = pair.Split(new[] { ':' }, 2);
                if (split.Length == 2) pairs.Add($"{{\"key\":{split[0]},\"value\":{split[1]}}}");
            }
            return $"{{\"items\":[{string.Join(",", pairs)}]}}";
        }
    }
}
