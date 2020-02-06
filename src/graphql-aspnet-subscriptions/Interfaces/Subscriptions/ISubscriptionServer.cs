﻿// *************************************************************
// project:  graphql-aspnet
// --
// repo: https://github.com/graphql-aspnet
// docs: https://graphql-aspnet.github.io
// --
// License:  MIT
// *************************************************************

namespace GraphQL.AspNet.Interfaces.Subscriptions
{
    using System.Threading.Tasks;

    /// <summary>
    /// An interface representing a subscription server that can accept events published
    /// by other graphql operations and process them for connected subscribers.
    /// </summary>
    public interface ISubscriptionServer
    {
        /// <summary>
        /// Receives the event (packaged and published by the proxy) and performs
        /// the required work to send it to connected clients.
        /// </summary>
        /// <param name="subscriptionEvent">A subscription event.</param>
        /// <returns>Task.</returns>
        Task ReceiveEvent(ISubscriptionEvent subscriptionEvent);
    }
}