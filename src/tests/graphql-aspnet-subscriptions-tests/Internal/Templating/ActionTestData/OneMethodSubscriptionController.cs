﻿// *************************************************************
// project:  graphql-aspnet
// --
// repo: https://github.com/graphql-aspnet
// docs: https://graphql-aspnet.github.io
// --
// License:  MIT
// *************************************************************

namespace GraphQL.Subscrptions.Tests.Internal.Templating.ActionTestData
{
    using System.ComponentModel;
    using System.Threading.Tasks;
    using GraphQL.AspNet.Attributes;
    using GraphQL.AspNet.Controllers;
    using GraphQL.AspNet.Interfaces.Controllers;
    using GraphQL.AspNet.Tests.CommonHelpers;

    public class OneMethodSubscriptionController : GraphController
    {
        [Subscription("path1")]
        [Description("SubDescription")]
        public TwoPropertyObject SingleMethod(TwoPropertyObject data)
        {
            return data;
        }
    }
}