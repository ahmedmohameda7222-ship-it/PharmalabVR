// Exact Native-identity fields only, linked in headless .NET tests; Unity uses
// the real UnityEngine.JsonUtility serializer at runtime.
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
            {
                var instance = Activator.CreateInstance(typeof(T), true);
                foreach (var field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public))
                    if (doc.RootElement.TryGetProperty(field.Name, out var value))
                        field.SetValue(instance, value.GetString());
                return (T)instance;
            }
        }
    }
}
