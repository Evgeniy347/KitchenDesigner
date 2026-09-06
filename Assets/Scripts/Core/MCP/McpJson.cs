using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KitchenDesigner.Core.MCP
{
    public static class McpJson
    {
        public const int TenthMillimetreDecimals = 1;

        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new RoundedNumberConverter() }
        };

        public static string Serialize(object value) =>
            JsonConvert.SerializeObject(value, Settings);

        private static readonly JsonSerializerSettings StrictDeserializationSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error
        };

        private static readonly JsonSerializer StrictDeserializer = JsonSerializer.Create(StrictDeserializationSettings);

        public static T ToObjectStrict<T>(this JObject obj) where T : new()
        {
            object? boxed;
            try
            {
                boxed = obj.ToObject<T>(StrictDeserializer);
            }
            catch (JsonSerializationException ex)
            {
                var extra = ExtractUnknownMember(ex.Message);
                var extraMsg = !string.IsNullOrEmpty(extra) ? $" Unknown field: '{extra}'." : "";
                throw new JsonSerializationException(
                    $"MCP parameter contract violation.{extraMsg} {ex.Message}", ex);
            }
            if (boxed == null)
                throw new JsonSerializationException(
                    $"Deserialization returned null for {typeof(T).Name}.");
            return (T)boxed;
        }

        private static string ExtractUnknownMember(string message)
        {
            const string prefix = "Could not find member '";
            int idx = message.IndexOf(prefix, StringComparison.Ordinal);
            if (idx < 0) return string.Empty;
            int start = idx + prefix.Length;
            int end = message.IndexOf('\'', start);
            return end > start ? message.Substring(start, end - start) : string.Empty;
        }

        public static T ToObjectStrictOrDefault<T>(this JObject? obj) where T : class, new()
            => obj != null ? obj.ToObjectStrict<T>() : new T();

        private sealed class RoundedNumberConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override bool CanConvert(Type t) =>
                t == typeof(float) || t == typeof(double) ||
                t == typeof(float?) || t == typeof(double?);

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                if (value == null) { writer.WriteNull(); return; }
                double d = value is float f ? f : (double)value;
                writer.WriteValue(Math.Round(d, TenthMillimetreDecimals));
            }

            public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
                => throw new NotSupportedException("Write-only converter");
        }
    }
}
