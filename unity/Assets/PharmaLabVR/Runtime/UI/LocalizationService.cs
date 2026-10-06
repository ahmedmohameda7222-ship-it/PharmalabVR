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
            values.Clear();
            foreach (var entry in ParseStringObject(table.text)) values[entry.Key] = entry.Value;
            Locale = locale;
        }

        public string Get(string key) => values.TryGetValue(key, out var value) ? value : $"[{key}]";

        private static Dictionary<string, string> ParseStringObject(string json)
        {
            var result = new Dictionary<string, string>();
            var index = 0;
            SkipWhitespace(json, ref index);
            Expect(json, ref index, '{');
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == '}') return result;
            while (index < json.Length)
            {
                var key = ReadString(json, ref index);
                SkipWhitespace(json, ref index);
                Expect(json, ref index, ':');
                SkipWhitespace(json, ref index);
                var value = ReadString(json, ref index);
                result[key] = value;
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == '}') { index++; break; }
                Expect(json, ref index, ',');
                SkipWhitespace(json, ref index);
            }
            SkipWhitespace(json, ref index);
            if (index != json.Length) throw new FormatException("Unexpected localization JSON suffix.");
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
                if (character != '\\') { value.Append(character); continue; }
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
