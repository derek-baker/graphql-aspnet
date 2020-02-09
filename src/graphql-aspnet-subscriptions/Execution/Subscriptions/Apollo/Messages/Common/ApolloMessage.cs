﻿// *************************************************************
// project:  graphql-aspnet
// --
// repo: https://github.com/graphql-aspnet
// docs: https://graphql-aspnet.github.io
// --
// License:  MIT
// *************************************************************

namespace GraphQL.AspNet.Execution.Subscriptions.Apollo.Messages.Common
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// A common base class for all apollo messages.
    /// </summary>
    public abstract class ApolloMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApolloMessage" /> class.
        /// </summary>
        /// <param name="messageType">Type of the message.</param>
        public ApolloMessage(ApolloMessageType messageType)
        {
            this.Type = messageType;
            this.Id = null;
        }

        /// <summary>
        /// Gets or sets the identifier for the scoped operation started by a client.
        /// </summary>
        /// <value>The identifier.</value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the type of the message, indicating expected payload types.
        /// </summary>
        /// <value>The type.</value>
        [JsonConverter(typeof(ApolloMessageTypeConverter))]
        public ApolloMessageType Type { get; set; }

        /// <summary>
        /// Gets the payload of the message as a general object.
        /// </summary>
        /// <value>The payload object.</value>
        [JsonIgnore]
        public abstract object PayloadObject { get; }
    }
}