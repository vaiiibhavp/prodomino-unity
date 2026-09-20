using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace HelperSharedLibrary
{
    /// <summary>
    /// Converts Firestore timestamp objects { seconds, nanoseconds } into DateTime and vice versa.
    /// Compatible with Unity IL2CPP (non-generic form).
    /// </summary>
    public class FirestoreTimestampConverter : JsonConverter
    {
        // Required public parameterless constructor
        public FirestoreTimestampConverter() { }

        public override bool CanConvert(Type objectType)
        {
            // Supports both DateTime and nullable DateTime
            return objectType == typeof(DateTime) || objectType == typeof(DateTime?);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            // Handle null values safely
            if (reader.TokenType == JsonToken.Null)
                return DateTime.MinValue;

            try
            {
                // Example Firestore format: { "seconds": 1761161475, "nanoseconds": 378020000 }
                var obj = JObject.Load(reader);

                var seconds = obj["seconds"]?.Value<long>() ?? 0;
                var nanos = obj["nanoseconds"]?.Value<long>() ?? 0;

                // Convert to UTC DateTime
                var dateTime = DateTimeOffset.FromUnixTimeSeconds(seconds)
                    .AddTicks(nanos / 100) // nanoseconds -> ticks
                    .UtcDateTime;

                return dateTime;
            }
            catch
            {
                // Sometimes Firestore SDKs may return ISO strings, fallback to parse
                if (reader.Value is string str && DateTime.TryParse(str, out var parsed))
                    return parsed.ToUniversalTime();

                return DateTime.MinValue;
            }
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            // Convert DateTime to Firestore timestamp format
            var date = value is not null ? (DateTime)value : DateTime.MinValue;
            var offset = new DateTimeOffset(date.ToUniversalTime());
            var seconds = offset.ToUnixTimeSeconds();
            var nanos = (int)((offset.ToUnixTimeMilliseconds() % 1000) * 1_000_000);

            var obj = new JObject
            {
                ["seconds"] = seconds,
                ["nanoseconds"] = nanos
            };

            obj.WriteTo(writer);
        }
    }
}
