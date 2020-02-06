﻿// *************************************************************
// project:  graphql-aspnet
// --
// repo: https://github.com/graphql-aspnet
// docs: https://graphql-aspnet.github.io
// --
// License:  MIT
// *************************************************************

namespace GraphQL.AspNet.Messaging.Handlers
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using GraphQL.AspNet.Interfaces.Messaging;

    /// <summary>
    /// A base handler for capturing common logic across all message handlers.
    /// </summary>
    internal abstract class ApolloMessageHandler : IApolloMessageHandler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApolloMessageHandler"/> class.
        /// </summary>
        protected ApolloMessageHandler()
        {
        }

        /// <summary>
        /// Determines whether this instance can process the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns><c>true</c> if this instance can handle the specified message; otherwise, <c>false</c>.</returns>
        public virtual bool CanHandleMessage(IApolloMessage message)
        {
            return message != null && message.Type == this.MessageType;
        }

        /// <summary>
        /// Handles the message, executing the logic of this handler against it.
        /// </summary>
        /// <param name="clientProxy">The client proxy.</param>
        /// <param name="message">The message to be handled.</param>
        /// <returns>A newly set of messages (if any) to be sent back to the client.</returns>
        public abstract Task<IEnumerable<IApolloMessage>> HandleMessage(
            ISubscriptionClientProxy clientProxy,
            IApolloMessage message);

        /// <summary>
        /// Gets the type of the message this handler can process.
        /// </summary>
        /// <value>The type of the message.</value>
        public abstract ApolloMessageType MessageType { get; }
    }
}