// EXPERIMENT ONLY: limited JavaScriptSerializer surface for a full-source net10 WPF probe.
// Not a production serialization migration. Differences (notably DateTime and edge-case
// number formats) must be checked against the legacy serializer before any product adoption.
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace System.Web.Script.Serialization
{
    public sealed class JavaScriptSerializer
    {
        public int MaxJsonLength { get; set; } = 2_097_152;
        public int RecursionLimit { get; set; } = 100;

        private JsonSerializerOptions Options()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                MaxDepth = RecursionLimit
            };
        }

        private void CheckLength(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (MaxJsonLength < 1 || json.Length > MaxJsonLength)
                throw new InvalidOperationException("JSON exceeds the configured length limit.");
            if (RecursionLimit < 1) throw new InvalidOperationException("Invalid JSON recursion limit.");
        }

        public string Serialize(object value)
        {
            string json = JsonSerializer.Serialize(value, value == null ? typeof(object) : value.GetType(), Options());
            CheckLength(json);
            return json;
        }

        public T Deserialize<T>(string json)
        {
            CheckLength(json);
            return JsonSerializer.Deserialize<T>(json, Options());
        }

        public object DeserializeObject(string json)
        {
            CheckLength(json);
            using (JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = RecursionLimit }))
                return ConvertElement(document.RootElement);
        }

        private static object ConvertElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var values = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (JsonProperty property in element.EnumerateObject())
                        values[property.Name] = ConvertElement(property.Value);
                    return values;
                case JsonValueKind.Array:
                    var items = new List<object>();
                    foreach (JsonElement item in element.EnumerateArray())
                        items.Add(ConvertElement(item));
                    return items.ToArray(); // IList + IEnumerable, matching legacy consumers.
                case JsonValueKind.String: return element.GetString();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined: return null;
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int integer)) return integer;
                    if (element.TryGetInt64(out long longInteger)) return longInteger;
                    if (element.TryGetDecimal(out decimal decimalNumber)) return decimalNumber;
                    return element.GetDouble();
                default: throw new FormatException("Unsupported JSON value kind.");
            }
        }
    }
}
