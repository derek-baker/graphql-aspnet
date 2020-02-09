﻿// *************************************************************
// project:  graphql-aspnet
// --
// repo: https://github.com/graphql-aspnet
// docs: https://graphql-aspnet.github.io
// --
// License:  MIT
// *************************************************************

namespace GraphQL.Subscrptions.Tests.Apollo
{
    using System.Threading.Tasks;
    using GraphQL.AspNet.Configuration;
    using GraphQL.AspNet.Execution.Subscriptions.Apollo;
    using GraphQL.AspNet.Execution.Subscriptions.Apollo.Messages;
    using GraphQL.AspNet.Execution.Subscriptions.Apollo.Messages.ClientMessages;
    using GraphQL.AspNet.Execution.Subscriptions.Apollo.Messages.ServerMessages;
    using GraphQL.AspNet.Schemas;
    using GraphQL.Subscriptions.Tests.Apollo;
    using GraphQL.Subscrptions.Tests.CommonHelpers;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using NUnit.Framework;

    [TestFixture]
    public partial class ApolloClientProxyTests
    {

        [Test]
        public async Task WhenConnectionEstablished_RequiredMessagesReturned()
        {
            var socketClient = new MockClientConnection();
            var options = new SchemaSubscriptionOptions<GraphSchema>();

            var provider = new ServiceCollection().BuildServiceProvider();
            var apolloClient = new ApolloClientProxy<GraphSchema>(provider, null, socketClient, options, false);

            var supervisor = new ApolloClientSupervisor<GraphSchema>();
            supervisor.RegisterNewClient(apolloClient);

            var message = new ApolloConnectionInitMessage();

            // queue a message sequence to the server
            socketClient.QueueClientMessage(new MockClientMessage(new ApolloConnectionInitMessage()));
            socketClient.QueueConnectionClose();

            // execute the connection sequence
            await apolloClient.StartConnection();

            // the server should have sent back two messages to the client (ack, keep alive)
            // per the protocol
            Assert.AreEqual(2, socketClient.ResponseMessageCount);

            // ensure the two response messages are of the appropriate type
            var returnMessage = socketClient.DequeueReturnMessageTo<ApolloResponseMessage>();
            Assert.IsNotNull(returnMessage, "message could not be converted");
            Assert.AreEqual(ApolloMessageType.CONNECTION_ACK, returnMessage.Type);

            returnMessage = socketClient.DequeueReturnMessageTo<ApolloResponseMessage>();
            Assert.IsNotNull(returnMessage, "message could not be converted");
            Assert.AreEqual(ApolloMessageType.CONNECTION_KEEP_ALIVE, returnMessage.Type);
        }
    }
}