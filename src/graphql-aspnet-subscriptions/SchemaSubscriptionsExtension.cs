﻿// *************************************************************
// project:  graphql-aspnet
// --
// repo: https://github.com/graphql-aspnet
// docs: https://graphql-aspnet.github.io
// --
// License:  MIT
// *************************************************************

namespace GraphQL.AspNet
{
    using System;
    using System.Collections.Generic;
    using GraphQL.AspNet.ApolloClient;
    using GraphQL.AspNet.Common;
    using GraphQL.AspNet.Common.Extensions;
    using GraphQL.AspNet.Configuration;
    using GraphQL.AspNet.Defaults;
    using GraphQL.AspNet.Execution.Subscriptions.Apollo;
    using GraphQL.AspNet.Execution.Subscriptions.ApolloServer;
    using GraphQL.AspNet.Interfaces.Clients;
    using GraphQL.AspNet.Interfaces.Configuration;
    using GraphQL.AspNet.Interfaces.Subscriptions;
    using GraphQL.AspNet.Interfaces.TypeSystem;
    using GraphQL.AspNet.Logging;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// A schema extentation encapsulating subscriptions for a given schema.
    /// </summary>
    /// <typeparam name="TSchema">The type of the schema this processor is built for.</typeparam>
    public class SchemaSubscriptionsExtension<TSchema> : ISchemaExtension
        where TSchema : class, ISchema
    {
        private SchemaOptions _primaryOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaSubscriptionsExtension{TSchema}"/> class.
        /// </summary>
        /// <param name="options">The options.</param>
        public SchemaSubscriptionsExtension(SchemaSubscriptionOptions<TSchema> options)
        {
            this.SubscriptionOptions = Validation.ThrowIfNullOrReturn(options, nameof(options));
            this.RequiredServices = new List<ServiceDescriptor>();
            this.OptionalServices = new List<ServiceDescriptor>();
        }

        /// <summary>
        /// This method is called by the parent options just before it is added to the extensions
        /// collection. Use this method to do any sort of configuration, final default settings etc.
        /// This method represents the last opportunity for the extention options to modify its own required
        /// service collection before being incorporated with the DI container.
        /// </summary>
        /// <param name="options">The parent options which owns this extension.</param>
        public void Configure(SchemaOptions options)
        {
            _primaryOptions = options;

            // swap out the master templater for the one that includes
            // support for the subscription action type
            if (!(GraphQLProviders.TemplateProvider is SubscriptionEnabledTemplateProvider))
                GraphQLProviders.TemplateProvider = new SubscriptionEnabledTemplateProvider();

            // add the needed apollo's classes as optional services
            // if the user has already added support for their own handlers
            // they will be safely ignored
            this.OptionalServices.Add(
                new ServiceDescriptor(
                    typeof(ISubscriptionServer<TSchema>),
                    typeof(ApolloSubscriptionServer<TSchema>),
                    ServiceLifetime.Singleton));

            this.OptionalServices.Add(
                  new ServiceDescriptor(
                      typeof(ISubscriptionClientFactory<TSchema>),
                      typeof(ApolloClientFactory<TSchema>),
                      ServiceLifetime.Singleton));

            this.OptionalServices.Add(
                new ServiceDescriptor(
                    typeof(ApolloClientSupervisor<TSchema>),
                    typeof(ApolloClientSupervisor<TSchema>),
                    ServiceLifetime.Singleton));

            this.OptionalServices.Add(
               new ServiceDescriptor(
                   typeof(ClientSubscriptionMaker<TSchema>),
                   typeof(ClientSubscriptionMaker<TSchema>),
                   ServiceLifetime.Transient));
        }

        /// <summary>
        /// Invokes this instance to perform any final setup requirements as part of
        /// its configuration during startup.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="serviceProvider">The service provider.</param>
        public void UseExtension(IApplicationBuilder app = null, IServiceProvider serviceProvider = null)
        {
            // configure the subscription route middleware for invoking the graphql
            // pipeline for the subscription
            if (!this.SubscriptionOptions.DisableDefaultRoute && app != null)
            {
                var routePath = this.SubscriptionOptions.Route.Replace(
                SubscriptionConstants.Routing.SCHEMA_ROUTE_KEY,
                _primaryOptions.QueryHandler.Route);

                var middlewareType = this.SubscriptionOptions.HttpMiddlewareComponentType
                    ?? typeof(DefaultGraphQLHttpSubscriptionMiddleware<TSchema>);

                this.EnsureMiddlewareTypeOrThrow(middlewareType);

                // register the middleware component
                app.UseMiddleware(middlewareType, this.SubscriptionOptions, routePath);
                app.ApplicationServices.WriteLogEntry(
                      (l) => l.SchemaSubscriptionRouteRegistered<TSchema>(
                      routePath));
            }
        }

        /// <summary>
        /// Ensures the middleware type contains a public constructor that accepts the
        /// three parameters required of it by the runtime.
        /// </summary>
        /// <param name="middlewareType">Type of the middleware to inspect.</param>
        private void EnsureMiddlewareTypeOrThrow(Type middlewareType)
        {
            var constructor = middlewareType.GetConstructor(
                new Type[]
                {
                    typeof(RequestDelegate),
                    typeof(SchemaSubscriptionOptions<TSchema>),
                    typeof(string),
                });

            if (constructor == null)
            {
                throw new InvalidOperationException(
                      $"Unable to initialize subscriptions for schema '{typeof(TSchema).FriendlyName()}'. " +
                      $"An attempt was made to use the type '{middlewareType.FriendlyName()}' as the middleware " +
                      "component to handle subscription operation requests. However, this type does not contain a public " +
                      $"constructor that accepts parameters of {typeof(RequestDelegate).FriendlyName()}, {typeof(SchemaSubscriptionOptions<TSchema>)}, and {typeof(string)}.");
            }
        }

        /// <summary>
        /// Gets the options related to this extension instance.
        /// </summary>
        /// <value>The options.</value>
        public SchemaSubscriptionOptions<TSchema> SubscriptionOptions { get; }

        /// <summary>
        /// Gets a collection of services this extension has registered that should be included in
        /// a DI container.
        /// </summary>
        /// <value>The additional types as formal descriptors.</value>
        public List<ServiceDescriptor> RequiredServices { get; }

        /// <summary>
        /// Gets a collection of services this extension has registered that may be included in
        /// a DI container. If they cannot be added, because a reference already exists, they will be skipped.
        /// </summary>
        /// <value>The additional types as formal descriptors.</value>
        public List<ServiceDescriptor> OptionalServices { get; }
    }
}