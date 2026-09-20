using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Unity.Services.Friends.Models;

/// <summary>
/// Custom converter for RelationshipType enum
/// This converter ensures consistent serialization/deserialization
/// without modifying the original enum.
/// </summary>
public class RelationshipTypeConverter : JsonConverter
{
    // Map string values to enum
    private static readonly Dictionary<string, RelationshipType> stringToEnum = new(StringComparer.OrdinalIgnoreCase)
    {
        { "FRIEND", RelationshipType.Friend },
        { "BLOCK", RelationshipType.Block },
        { "FRIEND_REQUEST", RelationshipType.FriendRequest }
    };

    // Map enum to string values
    private static readonly Dictionary<RelationshipType, string> enumToString = new()
    {
        { RelationshipType.Friend, "FRIEND" },
        { RelationshipType.Block, "BLOCK" },
        { RelationshipType.FriendRequest, "FRIEND_REQUEST" }
    };

    // Default value if deserialization fails
    private const RelationshipType defaultValue = RelationshipType.Friend;

    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(RelationshipType);
    }

    /// <summary>
    /// Deserializes a JSON value into a RelationshipType enum, handling string, integer, and null values.
    /// </summary>
    /// <param name="reader">The JsonReader to read from.</param>
    /// <param name="objectType">The type of the object to deserialize.</param>
    /// <param name="existingValue">The existing value of the object being deserialized.</param>
    /// <param name="serializer">The serializer to use for deserialization.</param>
    /// <returns>A RelationshipType value corresponding to the JSON input, or a default value if the input is invalid.</returns>
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        // Handle null values
        if (reader.TokenType == JsonToken.Null)
            return defaultValue;

        // If value is a string
        if (reader.TokenType == JsonToken.String)
        {
            string stringValue = (string)reader.Value;
            if (stringToEnum.TryGetValue(stringValue, out var enumValue))
                return enumValue;
        }

        // If value is an integer
        if (reader.TokenType == JsonToken.Integer)
        {
            int intValue = Convert.ToInt32(reader.Value);
            if (Enum.IsDefined(typeof(RelationshipType), intValue))
                return (RelationshipType)intValue;
        }

        // If nothing matches, return default
        return defaultValue;
    }

    /// <summary>
    /// Serializes a RelationshipType value to JSON using its string representation if available, otherwise its numeric
    /// value.
    /// </summary>
    /// <param name="writer">The JsonWriter used to write the JSON output.</param>
    /// <param name="value">The RelationshipType value to serialize.</param>
    /// <param name="serializer">The JsonSerializer used for serialization.</param>
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        RelationshipType enumValue = (RelationshipType)value;

        // Try to write string mapping
        if (enumToString.TryGetValue(enumValue, out var stringValue))
        {
            writer.WriteValue(stringValue);
            return;
        }

        // Fallback: write numeric value
        writer.WriteValue((int)enumValue);
    }
}
