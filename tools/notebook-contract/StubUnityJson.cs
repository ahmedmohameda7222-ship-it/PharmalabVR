// Headless structural mirror of UnityEngine.JsonUtility for the few serializable
// source view-model fields. The real Unity JSON path is still NOT_RUN.
using System;
using System.Reflection;
using System.Text.Json;

namespace UnityEngine
{
    public static class JsonUtility
    {
        public static T FromJson<T>(string text)
        {
            using (var doc = JsonDocument.Parse(text))
                return (T)Decode(typeof(T), doc.RootElement);
        }

        private static object Decode(Type type, JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Null) return null;
            if (type == typeof(string)) return value.GetString();
            if (type == typeof(int)) return value.GetInt32();
            if (type == typeof(double)) return value.GetDouble();
            if (type == typeof(bool)) return value.GetBoolean();
            if (type.IsArray)
            {
                if (value.ValueKind != JsonValueKind.Array) throw new FormatException("Array expected.");
                var elementType = type.GetElementType();
                var result = Array.CreateInstance(elementType, value.GetArrayLength());
                var index = 0;
                foreach (var item in value.EnumerateArray())
                    result.SetValue(Decode(elementType, item), index++);
                return result;
            }
            if (value.ValueKind != JsonValueKind.Object) throw new FormatException("JSON object expected.");
            var instance = Activator.CreateInstance(type, nonPublic: true);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (value.TryGetProperty(field.Name, out var fieldValue))
                    field.SetValue(instance, Decode(field.FieldType, fieldValue));
            return instance;
        }
    }
}
