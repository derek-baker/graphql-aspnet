﻿// *************************************************************
// project:  graphql-aspnet
// --
// repo: https://github.com/graphql-aspnet
// docs: https://graphql-aspnet.github.io
// --
// License:  MIT
// *************************************************************

namespace GraphQL.AspNet.Configuration.Mvc
{
    using System;
    using System.Reflection;
    using GraphQL.AspNet.Common;
    using GraphQL.AspNet.Defaults;
    using GraphQL.AspNet.Execution;
    using GraphQL.AspNet.Interfaces.Configuration;
    using GraphQL.AspNet.Interfaces.Engine;
    using GraphQL.AspNet.Interfaces.Execution;
    using GraphQL.AspNet.Interfaces.Logging;
    using GraphQL.AspNet.Interfaces.Middleware;
    using GraphQL.AspNet.Interfaces.TypeSystem;
    using GraphQL.AspNet.Interfaces.Web;
    using GraphQL.AspNet.Internal.Interfaces;
    using GraphQL.AspNet.Middleware.FieldAuthorization;
    using GraphQL.AspNet.Middleware.FieldExecution;
    using GraphQL.AspNet.Middleware.QueryExecution;
    using GraphQL.AspNet.Parsing;
    using GraphQL.AspNet.Web;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// A builder for adding and configuring the controller based <see cref="ISchema" /> to a DI container
    /// and any MVC settings if applicable.
    /// </summary>
    /// <typeparam name="TSchema">The type of the graphql schema to inject.</typeparam>
    public class GraphQLSchemaInjector<TSchema> : ISchemaInjector<TSchema>
        where TSchema : class, ISchema
    {
        private readonly IServiceCollection _serviceCollection;
        private readonly Action<SchemaOptions> _configureOptions;
        private readonly SchemaBuilder<TSchema> _schemaBuilder;
        private SchemaOptions _options;
        private GraphQueryHandler<TSchema> _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphQLSchemaInjector{TSchema}" /> class.
        /// </summary>
        /// <param name="serviceCollection">The service collection.</param>
        /// <param name="configureOptions">The use supplied action method to configure the
        /// primary options used to govern schema runtime operations.</param>
        public GraphQLSchemaInjector(IServiceCollection serviceCollection, Action<SchemaOptions> configureOptions)
        {
            _serviceCollection = Validation.ThrowIfNullOrReturn(serviceCollection, nameof(serviceCollection));
            _configureOptions = configureOptions;
            _schemaBuilder = new SchemaBuilder<TSchema>();
        }

        /// <summary>
        /// Configures the services and settings for the schema using the supplied configuration function.
        /// </summary>
        public void ConfigureServices()
        {
            // create the builder to guide the rest of the setup operations
            _options = new SchemaOptions(typeof(TSchema));
            _schemaBuilder.TypeReferenceAdded += this.TypeReferenced_EventHandler;
            _options.TypeReferenceAdded += this.TypeReferenced_EventHandler;

            _configureOptions?.Invoke(_options);

            // register global directives to the schema
            foreach (var type in Constants.GlobalDirectives)
            {
                _options.AddGraphType(type);
            }

            // add execution assembly for auto loading of graph types
            if (_options.AutoRegisterLocalGraphEntities)
            {
                var assembly = Assembly.GetEntryAssembly();
                _options.AddGraphAssembly(assembly);
            }

            // ensure some http processor is set
            if (_options.QueryHandler.HttpProcessorType == null)
            {
                if (_options.QueryHandler.AuthenticatedRequestsOnly)
                    _options.QueryHandler.HttpProcessorType = typeof(SecureGraphQLHttpProcessor<TSchema>);
                else
                    _options.QueryHandler.HttpProcessorType = typeof(DefaultGraphQLHttpProcessor<TSchema>);
            }

            // register the schema
            _serviceCollection.TryAddSingleton(this.BuildNewSchemaInstance);

            // setup default middleware for each required pipeline
            var queryPipelineHelper = new QueryExecutionPipelineHelper<TSchema>(_schemaBuilder.QueryExecutionPipeline);
            queryPipelineHelper.AddDefaultMiddlewareComponents(_options);

            var fieldPipelineHelper = new FieldExecutionPipelineHelper<TSchema>(_schemaBuilder.FieldExecutionPipeline);
            fieldPipelineHelper.AddDefaultMiddlewareComponents(_options);

            var authPipelineHelper = new FieldAuthorizationPipelineHelper<TSchema>(_schemaBuilder.FieldAuthorizationPipeline);
            authPipelineHelper.AddDefaultMiddlewareComponents(_options);

            // register the DI entries for each pipeline
            _serviceCollection.TryAddSingleton(this.CreatePipelineFactory(_schemaBuilder.FieldExecutionPipeline));
            _serviceCollection.TryAddSingleton(this.CreatePipelineFactory(_schemaBuilder.FieldAuthorizationPipeline));
            _serviceCollection.TryAddSingleton(this.CreatePipelineFactory(_schemaBuilder.QueryExecutionPipeline));

            this.RegisterEngineComponents();
        }

        /// <summary>
        /// Registers the default engine components for this schema. These components are used by the default pipelines
        /// registered as part of this injector.
        /// </summary>
        private void RegisterEngineComponents()
        {
            // "per schema" engine components
            _serviceCollection.TryAddSingleton<IQueryOperationComplexityCalculator<TSchema>, DefaultOperationComplexityCalculator<TSchema>>();
            _serviceCollection.TryAddSingleton<IGraphResponseWriter<TSchema>, DefaultResponseWriter<TSchema>>();
            _serviceCollection.TryAddSingleton<IGraphQueryDocumentGenerator<TSchema>, DefaultGraphQueryDocumentGenerator<TSchema>>();
            _serviceCollection.TryAddSingleton<IGraphQueryPlanGenerator<TSchema>, DefaultGraphQueryPlanGenerator<TSchema>>();
            _serviceCollection.TryAddSingleton<IGraphQueryExecutionMetricsFactory<TSchema>, DefaultGraphQueryExecutionMetricsFactory<TSchema>>();

            // "per request per schema" components
            _serviceCollection.TryAddTransient(typeof(IGraphQLHttpProcessor<TSchema>), _options.QueryHandler.HttpProcessorType);

            // "per application server" instance
            _serviceCollection.TryAddSingleton<IGraphQLDocumentParser, GraphQLParser>();
            _serviceCollection.TryAddScoped<IGraphLogger>(sp => sp?.GetService<IGraphEventLogger>());
            _serviceCollection.TryAddScoped<IGraphEventLogger>((sp) =>
            {
                var factory = sp?.GetService<ILoggerFactory>();
                if (factory == null)
                    return null;

                return new DefaultGraphLogger(factory);
            });
        }

        /// <summary>
        /// Creates the factory for the DI container to createa  pipeline of the given type.
        /// </summary>
        /// <typeparam name="TMiddleware">The type of middleware supported by the pipeline.</typeparam>
        /// <typeparam name="TContext">The type of the context the middleware components can handle.</typeparam>
        /// <param name="pipelineBuilder">The pipeline builder.</param>
        /// <returns>ISchemaPipeline&lt;TSchema, TContext&gt;.</returns>
        private Func<IServiceProvider, ISchemaPipeline<TSchema, TContext>>
            CreatePipelineFactory<TMiddleware, TContext>(SchemaPipelineBuilder<TSchema, TMiddleware, TContext> pipelineBuilder)
                where TMiddleware : class, IGraphMiddlewareComponent<TContext>
                where TContext : class, IGraphMiddlewareContext
        {
            return (sp) =>
            {
                var pipeline = pipelineBuilder.Build();
                this.WriteLogEntry(sp, (l) => l.SchemaPipelineRegistered<TSchema>(pipeline));
                return pipeline;
            };
        }

        /// <summary>
        /// Responds to an event raised by a child configuration component by adding the raised type to the DI container
        /// controlled by this injector.
        /// </summary>
        private void TypeReferenced_EventHandler(object sender, TypeReferenceEventArgs e)
        {
            _serviceCollection.TryAdd(new ServiceDescriptor(e.Type, e.Type, e.LifeTime));
        }

        /// <summary>
        /// Attempts to create a new instance of the schema using whatever constructore setup is required by it then initialize all preconfigured graph types into it.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <returns>TSchema.</returns>
        private TSchema BuildNewSchemaInstance(IServiceProvider serviceProvider)
        {
            var schemaInstance = GraphSchemaBuilder.BuildSchema<TSchema>(serviceProvider);
            var initializer = new GraphSchemaInitializer(_options);
            initializer.Initialize(schemaInstance);

            this.WriteLogEntry(
                  serviceProvider,
                  (l) => l.SchemaInstanceCreated(schemaInstance));

            return schemaInstance;
        }

        /// <summary>
        /// Uses the schema.
        /// </summary>
        /// <param name="appBuilder">The application builder.</param>
        public void UseSchema(IApplicationBuilder appBuilder)
        {
            this.UseSchema(appBuilder?.ApplicationServices);

            if (_options.QueryHandler.DisableDefaultRoute)
                return;

            // when possible, create the singleton of hte processor up front to avoid any
            // calls into the DI container at runtime.
            _handler = new GraphQueryHandler<TSchema>();
            appBuilder.Map(_options.QueryHandler.Route, _handler.CreateInvoker);

            this.WriteLogEntry(
                  appBuilder?.ApplicationServices,
                  (l) => l.SchemaRouteRegistered<TSchema>(
                  _options.QueryHandler.Route));
        }

        /// <summary>
        /// Performs final configuration on graphql and preparses any referenced types for their meta data.
        /// Will NOT attempt to register an HTTP for the schema.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        public void UseSchema(IServiceProvider serviceProvider)
        {
            // pre-parse any types known to this schema
            var preCacher = new SchemaPreCacher();
            preCacher.PrecacheTemplates(_options.RegisteredSchemaTypes);
        }

        /// <summary>
        /// Writes the startup log entry to the event logger if it can be generated from the service provider.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="writeFunction">The write function.</param>
        private void WriteLogEntry(IServiceProvider serviceProvider, Action<IGraphEventLogger> writeFunction)
        {
            if (serviceProvider != null)
            {
                using (var scopedProvider = serviceProvider.CreateScope())
                {
                    var logger = scopedProvider.ServiceProvider.GetService<IGraphEventLogger>();
                    if (logger != null)
                        writeFunction(logger);
                }
            }
        }

        /// <summary>
        /// Gets the pipeline builder for the schema being tracked by this instance.
        /// </summary>
        /// <value>The pipeline builder.</value>
        public ISchemaBuilder<TSchema> SchemaBuilder => _schemaBuilder;
    }
}