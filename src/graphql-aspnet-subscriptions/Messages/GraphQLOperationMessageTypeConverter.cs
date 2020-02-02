﻿// *************************************************************
// project:  graphql-aspnet
// --
// repo: https://github.com/graphql-aspnet
// docs: https://graphql-aspnet.github.io
// --
// License:  MIT
// *************************************************************

namespace GraphQL.AspNet.Messaging
{
    using System;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Handles conversion of <see cref="GraphQLOperationMessageType"/> to and from json to handle potential casing
    /// issues with connected clients.
    /// </summary>
    internal class GraphQLOperationMessageTypeConverter : JsonConverter<GraphQLOperationMessageType>
    {
        /// <summary>
        /// Reads and converts the JSON to type <see cref="GraphQLOperationMessageType"/>.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <returns>The converted value.</returns>
        public override GraphQLOperationMessageType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Expected {nameof(JsonTokenType.String)} but got {reader.TokenType.ToString()}");
            }

            return GraphQLOperationMessageTypeExtensions.FromString(reader.GetString());
        }

        /// <summary>
        /// Writes a specified value as JSON.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The value to convert to JSON.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        public override void Write(Utf8JsonWriter writer, GraphQLOperationMessageType value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(GraphQLOperationMessageTypeExtensions.Serialize(value));
        }
    }
}