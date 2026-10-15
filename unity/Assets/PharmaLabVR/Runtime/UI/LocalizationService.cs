using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PharmaLabVR.UI
{
    // English-only static UI copy. No language switch, bidi/RTL shaping, or
    // second-language fallback is part of the product.
    public sealed class LocalizationService
    {
        private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
        public string Locale => "en";

        public void Load(string locale, TextAsset table)
        {
            if (locale != "en")
                throw new ArgumentException("PharmaLabVR supports English only.", nameof(locale));
            if (table == null) throw new ArgumentNullException(nameof(table));
            var parsed = ParseStringObject(table.text);
            if (parsed.Count == 0) throw new FormatException("Empty English text catalog.");
            values.Clear();
            foreach (var kv in parsed) values.Add(kv.Key, kv.Value);
        }

        public string Get(string key) => values.TryGetValue(key, out var value)
            ? value : "[" + key + "]";

        private static Dictionary<string, string> ParseStringObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Missing English text.");
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var index = 0;
            SkipWhitespace(json, ref index);
            Expect(json, ref index, '{');
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == '}') index++;
            else
            {
                while (index < json.Length)
                {
                    SkipWhitespace(json, ref index);
                    var key = ReadString(json, ref index);
                    if (string.IsNullOrWhiteSpace(key) || values.ContainsKey(key))
                        throw new FormatException("Duplicate or empty text key.");
                    SkipWhitespace(json, ref index);
                    Expect(json, ref index, ':');
                    SkipWhitespace(json, ref index);
                    var value = ReadString(json, ref index);
                    if (string.IsNullOrWhiteSpace(value))
                        throw new FormatException("Empty English text value.");
                    if (ContainsArabicScript(key) || ContainsArabicScript(value))
                        throw new FormatException("Non-English script is not allowed in product UI copy.");
                    values.Add(key, value);
                    SkipWhitespace(json, ref index);
                    if (index < json.Length && json[index] == '}') { index++; break; }
                    Expect(json, ref index, ',');
                    SkipWhitespace(json, ref index);
                    if (index == json.Length || json[index] == '}')
                        throw new FormatException("Trailing comma in English text.");
                }
            }
            SkipWhitespace(json, ref index);
            if (index != json.Length) throw new FormatException("Trailing content after English text.");
            return values;
        }

        private static bool ContainsArabicScript(string text)
        {
            foreach (var c in text)
            {
                if ((c >= '\u0600' && c <= '\u06FF') ||
                    (c >= '\u0750' && c <= '\u077F') ||
                    (c >= '\u08A0' && c <= '\u08FF') ||
                    (c >= '\uFB50' && c <= '\uFDFF') ||
                    (c >= '\uFE70' && c <= '\uFEFF'))
                    return true;
            }
            return false;
        }

        private static string ReadString(string json, ref int index)
        {
            Expect(json, ref index, '"');
            var value = new StringBuilder();
            while (index < json.Length)
            {
                var current = json[index++];
                if (current == '"') return value.ToString();
                if (current != '\\')
                {
                    if (char.IsControl(current))
                        throw new FormatException("Unescaped control character in English text.");
                    value.Append(current);
                    continue;
                }
                if (index >= json.Length) throw new FormatException("Incomplete escape.");
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
                        if (!ushort.TryParse(json.Substring(index, 4),
                                System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture, out var codepoint))
                            throw new FormatException("Invalid Unicode escape.");
                        value.Append((char)codepoint);
                        index += 4;
                        break;
                    default: throw new FormatException("Unsupported escape.");
                }
            }
            throw new FormatException("Unterminated English text string.");
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }

        private static void Expect(string json, ref int index, char expected)
        {
            if (index >= json.Length || json[index] != expected)
                throw new FormatException("Malformed English text JSON.");
            index++;
        }
    }
}
