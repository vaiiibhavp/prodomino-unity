using Newtonsoft.Json;
using System;
using Unity.Services.Friends.Models;

namespace ProDomino.FriendSystem
{
    /// <summary>
    /// Converts MemberRole values to and from their string representations in JSON.
    /// </summary>
    public class MemberRoleConverter : JsonConverter<MemberRole>
    {
        /// <summary>
        /// Deserializes a MemberRole value from a JSON string.
        /// </summary>
        /// <param name="reader">The JsonReader to read from.</param>
        /// <param name="objectType">The type of the object to deserialize.</param>
        /// <param name="existingValue">The existing MemberRole value, if any.</param>
        /// <param name="hasExistingValue">Indicates whether an existing value is present.</param>
        /// <param name="serializer">The JsonSerializer to use for deserialization.</param>
        /// <returns>The deserialized MemberRole value.</returns>
        /// <exception cref="JsonSerializationException">Thrown when the JSON token is not a string or the value is unrecognized.</exception>
        public override MemberRole ReadJson(JsonReader reader, Type objectType, MemberRole existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.String)
                throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing MemberRole.");

            string value = ((string)reader.Value).ToUpperInvariant();

            // Accept both old and new values
            switch (value)
            {
                case "TARGET":
                    return MemberRole.Target;
                case "SOURCE":
                    return MemberRole.Source;
                case "NONE":
                    return MemberRole.None;
                default:
                    throw new JsonSerializationException($"Unknown MemberRole value: {value}");
            }
        }

        /// <summary>
        /// Serializes a MemberRole value to its corresponding string representation in JSON.
        /// </summary>
        /// <param name="writer">The JsonWriter used to write the JSON output.</param>
        /// <param name="value">The MemberRole value to serialize.</param>
        /// <param name="serializer">The JsonSerializer used for serialization.</param>
        public override void WriteJson(JsonWriter writer, MemberRole value, JsonSerializer serializer)
        {
            // Always serialize to the new value
            switch (value)
            {
                case MemberRole.Target:
                    writer.WriteValue("TARGET");
                    break;
                case MemberRole.Source:
                    writer.WriteValue("SOURCE");
                    break;
                case MemberRole.None:
                    writer.WriteValue("NONE");
                    break;
                default:
                    writer.WriteNull();
                    break;
            }
        }
    }
}
