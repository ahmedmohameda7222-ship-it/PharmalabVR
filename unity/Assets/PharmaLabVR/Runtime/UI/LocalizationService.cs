using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PharmaLabVR.UI
{
    public sealed class LocalizationService
    {
        private readonly Dictionary<string, string> values = new();
        public string Locale { get; private set; } = "en";
        public bool IsRightToLeft => string.Equals(Locale, "ar", StringComparison.OrdinalIgnoreCase);

        public void Load(string locale, TextAsset table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (locale != "en" && locale != "ar")
                throw new ArgumentException("Only pinned English and Arabic catalogs are supported.", nameof(locale));
            // Parse and validate before any externally visible state mutation.
            // A bad/partial localization file cannot blank out a live lab UI.
            var next = ParseStringObject(table.text);
            if (next.Count == 0) throw new FormatException("Localization catalog has no labels.");
            values.Clear();
            foreach (var entry in next) values[entry.Key] = entry.Value;
            Locale = locale;
        }

        public string Get(string key) => values.TryGetValue(key, out var value) ? value : $"[{key}]";

        private static Dictionary<string, string> ParseStringObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("Empty localization JSON.");
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var index = 0;
            SkipWhitespace(json, ref index);
            Expect(json, ref index, '{');
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == '}')
            {
                index++;
            }
            else
            {
                while (index < json.Length)
                {
                    SkipWhitespace(json, ref index);
                    var key = ReadString(json, ref index);
                    if (string.IsNullOrWhiteSpace(key) || result.ContainsKey(key))
                        throw new FormatException("Duplicate or empty localization key.");
                    SkipWhitespace(json, ref index);
                    Expect(json, ref index, ':');
                    SkipWhitespace(json, ref index);
                    var value = ReadString(json, ref index);
                    if (string.IsNullOrWhiteSpace(value))
                        throw new FormatException("Empty localized label: " + key);
                    result.Add(key, value);
                    SkipWhitespace(json, ref index);
                    if (index < json.Length && json[index] == '}')
                    {
                        index++;
                        break;
                    }
                    Expect(json, ref index, ',');
                    SkipWhitespace(json, ref index);
                    if (index >= json.Length || json[index] == '}')
                        throw new FormatException("Trailing comma or unterminated localization object.");
                }
            }
            SkipWhitespace(json, ref index);
            if (index != json.Length)
                throw new FormatException("Unexpected localization JSON suffix.");
            return result;
        }

        private static string ReadString(string json, ref int index)
        {
            Expect(json, ref index, '"');
            var value = new StringBuilder();
            while (index < json.Length)
            {
                var character = json[index++];
                if (character == '"') return value.ToString();
                if (character != '\\')
                {
                    if (char.IsControl(character))
                        throw new FormatException("Unescaped control character in localization string.");
                    value.Append(character);
                    continue;
                }
                if (index >= json.Length) throw new FormatException("Incomplete JSON escape.");
                var escape = json[index++];
                switch (escape)
                {
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case '/': value.Append('/'); break;
                    case 'b': value.Append('\b'); break;
                    case 'f': value.Append('\f'); break;
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    case 'u':
                        if (index + 4 > json.Length) throw new FormatException("Incomplete Unicode escape.");
                        value.Append((char)Convert.ToInt32(json.Substring(index, 4), 16));
                        index += 4;
                        break;
                    default: throw new FormatException("Unsupported JSON escape.");
                }
            }
            throw new FormatException("Unterminated JSON string.");
        }

        private static void SkipWhitespace(string value, ref int index)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index])) index++;
        }

        private static void Expect(string value, ref int index, char expected)
        {
            if (index >= value.Length || value[index] != expected) throw new FormatException($"Expected '{expected}'.");
            index++;
        }
    }
}
