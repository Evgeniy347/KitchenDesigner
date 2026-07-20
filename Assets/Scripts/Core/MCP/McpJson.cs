using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KitchenDesigner.Core.MCP
{
    /// <summary>
    /// Единые настройки JSON-сериализации всех MCP-ответов.
    ///
    /// Зачем:
    /// - float/double округляются до 4 знаков (0.0001 м = 0.1 мм) — модель никогда
    ///   не видит двоичный мусор вида 0.0180000011 или -5.96e-05;
    /// - null-поля опускаются (drawer/table/faceObstructions и т.п. у обычных
    ///   деталей) — ответы короче примерно на треть.
    ///
    /// Эти же настройки используются для ETag get_all_elements: хеш перестаёт
    /// «дребезжать» от суб-0.1мм сдвигов позиций.
    /// </summary>
    public static class McpJson
    {
        /// <summary>Знаков после запятой: 4 для метров = точность 0.1 мм.</summary>
        public const int FloatDecimals = 4;

        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new RoundedNumberConverter() }
        };

        public static string Serialize(object value) =>
            JsonConvert.SerializeObject(value, Settings);

        /// <summary>Десериализация с жёсткой проверкой: неизвестные поля → ошибка.
        /// Контракт MCP не терпит лишних атрибутов — если агент шлёт поле, которого
        /// нет в C#-классе параметров, это баг или опечатка, и лучше сказать сразу.</summary>
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

        /// <summary>Округляет float/double при записи. Чтение не поддерживает
        /// (входящие параметры не трогаем — только исходящие ответы).</summary>
        private sealed class RoundedNumberConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override bool CanConvert(Type t) =>
                t == typeof(float) || t == typeof(double) ||
                t == typeof(float?) || t == typeof(double?);

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                if (value == null) { writer.WriteNull(); return; }
                // float сначала в decimal через строку нельзя — достаточно Math.Round:
                // (double)0.018f = 0.01800000108… → Round(…, 4) = 0.018.
                double d = value is float f ? f : (double)value;
                writer.WriteValue(Math.Round(d, FloatDecimals));
            }

            public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
                => throw new NotSupportedException("Write-only converter");
        }
    }
}
