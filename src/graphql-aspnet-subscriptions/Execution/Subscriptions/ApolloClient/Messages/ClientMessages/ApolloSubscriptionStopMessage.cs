﻿// *************************************************************
// project:  graphql-aspnet
// --
// repo: https://github.com/graphql-aspnet
// docs: https://graphql-aspnet.github.io
// --
// License:  MIT
// *************************************************************

namespace GraphQL.AspNet.Messaging.Messages
{
    using System.Diagnostics;
    using GraphQL.AspNet.Messaging.Messages.Payloads;

    /// <summary>
    /// A message sent by the client when it wants to stop an inflight subscription operation.
    /// </summary>
    ///
    [DebuggerDisplay("Apollo Subscription Stop (Id: {Id})")]
    public class ApolloSubscriptionStopMessage : ApolloMessage<ApolloNullPayload>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApolloSubscriptionStopMessage"/> class.
        /// </summary>
        public ApolloSubscriptionStopMessage()
            : base(ApolloMessageType.STOP)
        {
        }
    }
}