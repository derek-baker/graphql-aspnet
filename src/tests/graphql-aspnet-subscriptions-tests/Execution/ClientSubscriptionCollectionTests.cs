﻿// *************************************************************
// project:  graphql-aspnet
// --
// repo: https://github.com/graphql-aspnet
// docs: https://graphql-aspnet.github.io
// --
// License:  MIT
// *************************************************************

namespace GraphQL.Subscriptions.Tests.Apollo
{
    using System.Linq;
    using GraphQL.AspNet.Execution.Subscriptions.Apollo;
    using GraphQL.AspNet.Interfaces.Subscriptions;
    using GraphQL.AspNet.Interfaces.TypeSystem;
    using GraphQL.AspNet.Schemas;
    using GraphQL.AspNet.Schemas.Structural;
    using GraphQL.AspNet.Tests.Framework;
    using GraphQL.Subscriptions.Tests.TestServerHelpers;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class ClientSubscriptionCollectionTests
    {
        private IClientSubscription<GraphSchema> MakeSubscription(string id = "abc123", string routePath = "path1/path2")
        {
            var subscription = new Mock<IClientSubscription<GraphSchema>>();

            var field = new Mock<ISubscriptionGraphField>();
            var path = new GraphFieldPath(AspNet.Execution.GraphCollection.Subscription, routePath);
            field.Setup(x => x.Route).Returns(path);
            subscription.Setup(x => x.Route).Returns(path);
            subscription.Setup(x => x.Field).Returns(field.Object);
            subscription.Setup(x => x.IsValid).Returns(true);
            subscription.Setup(x => x.ClientProvidedId).Returns(id);

            var subClient = new Mock<ISubscriptionClientProxy>();
            subscription.Setup(x => x.Client).Returns(subClient.Object);

            return subscription.Object;
        }

        [Test]
        public void ClientAdded_IsReturnedWhenSearched()
        {
            var subscription = this.MakeSubscription();
            var collection = new ClientSubscriptionCollection<GraphSchema>();

            collection.Add(subscription);

            var foundSubs = collection.RetrieveSubscriptions(subscription.Client);
            Assert.AreEqual(1, foundSubs.Count());
            Assert.AreEqual(1, collection.RetrieveSubscriptions(subscription.Route.Path).Count());

            Assert.AreEqual(subscription, foundSubs.Single());
        }

        [Test]
        public void ClientRemoved_NoLongerReturnedBySearch()
        {
            var subscription = this.MakeSubscription("abc124");
            var collection = new ClientSubscriptionCollection<GraphSchema>();

            collection.Add(subscription);

            // ensure it was added
            var foundSubs = collection.RetrieveSubscriptions(subscription.Client);
            Assert.AreEqual(1, foundSubs.Count());

            var foundSub = foundSubs.Single();

            // try and take it out
            var removedSub = collection.TryRemoveSubscription(subscription.Client, "abc124");

            // ensure the returned item is the one that was originally added
            Assert.IsNotNull(removedSub);
            Assert.AreEqual(foundSub, removedSub);

            // ensure nothing exists that can be found
            Assert.AreEqual(0, foundSubs.Count());
            Assert.AreEqual(0, collection.RetrieveSubscriptions(subscription.Route.Path).Count());
        }
    }
}