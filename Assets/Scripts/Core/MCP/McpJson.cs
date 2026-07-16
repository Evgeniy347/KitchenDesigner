using System;
using Newtonsoft.Json;

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
