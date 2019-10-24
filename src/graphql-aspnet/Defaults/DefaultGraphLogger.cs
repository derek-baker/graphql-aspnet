﻿// *************************************************************
// project:  graphql-aspnet
// --
// repo: https://github.com/graphql-aspnet
// docs: https://graphql-aspnet.github.io
// --
// License:  MIT
// *************************************************************

namespace GraphQL.AspNet.Defaults
{
    using System;
    using System.Security.Claims;
    using GraphQL.AspNet.Common;
    using GraphQL.AspNet.Execution.InputModel;
    using GraphQL.AspNet.Interfaces.Execution;
    using GraphQL.AspNet.Interfaces.Logging;
    using GraphQL.AspNet.Interfaces.Middleware;
    using GraphQL.AspNet.Interfaces.TypeSystem;
    using GraphQL.AspNet.Internal.Interfaces;
    using GraphQL.AspNet.Logging;
    using GraphQL.AspNet.Logging.Common;
    using GraphQL.AspNet.Logging.ExecutionEvents;
    using GraphQL.AspNet.Middleware.FieldAuthorization;
    using GraphQL.AspNet.Middleware.FieldExecution;
    using GraphQL.AspNet.Middleware.QueryExecution;
    using GraphQL.AspNet.Security;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// A default logger for use in graphql operations. This logger will automatically append
    /// a unique instance id to each entry logged through it. When injected into DI as a "scoped" lifetime (the default behavior)
    /// this has an effect of attaching a unique id to all messages generated for each graphql request coming
    /// through the system for easy tracking.
    /// </summary>
    public class DefaultGraphLogger : IGraphEventLogger
    {
        private readonly ILogger _logger;

        // a unique id under which all entries of this logger are recorded
        // since (by default) the logger is created in a scoped setting
        // this id will be unique per
        private readonly string _loggerInstanceId;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultGraphLogger" /> class.
        /// </summary>
        /// <param name="loggerFactory">The logger factory from which to generate the underlying <see cref="ILogger" />.</param>
        public DefaultGraphLogger(ILoggerFactory loggerFactory)
        {
            Validation.ThrowIfNull(loggerFactory, nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger(Constants.Logging.LOG_CATEGORY);
            _loggerInstanceId = Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Recorded when the startup services generates a new schema instance.
        /// </summary>
        /// <typeparam name="TSchema">The type of the schema that was generated.</typeparam>
        /// <param name="schema">The schema instance.</param>
        public virtual void SchemaInstanceCreated<TSchema>(TSchema schema)
            where TSchema : class, ISchema
        {
            if (!this.IsEnabled(LogLevel.Debug))
                return;

            var entry = new SchemaInstanceCreatedLogEntry<TSchema>(schema);
            this.LogEvent(LogLevel.Debug, entry);
        }

        /// <summary>
        /// Schemas the pipeline registered.
        /// </summary>
        /// <typeparam name="TSchema">The type of the t schema.</typeparam>
        /// <param name="pipleine">The pipleine.</param>
        public virtual void SchemaPipelineRegistered<TSchema>(ISchemaPipeline pipleine)
            where TSchema : class, ISchema
        {
            if (!this.IsEnabled(LogLevel.Debug))
                return;

            var entry = new SchemaPipelineRegisteredLogEntry<TSchema>(pipleine);
            this.LogEvent(LogLevel.Debug, entry);
        }

        /// <summary>
        /// Recorded when the startup services registers a publically available ASP.NET MVC route to which
        /// end users can submit graphql queries.
        /// </summary>
        /// <typeparam name="TSchema">The type of the schema the route was registered for.</typeparam>
        /// <param name="routePath">The relative route path (e.g. '/graphql').</param>
        public virtual void SchemaRouteRegistered<TSchema>(string routePath)
            where TSchema : class, ISchema
        {
            if (!this.IsEnabled(LogLevel.Debug))
                return;

            var entry = new SchemaRouteRegisteredLogEntry<TSchema>(routePath);
            this.LogEvent(LogLevel.Debug, entry);
        }

        /// <summary>
        /// Recorded when a new request is generated by a query controller and passed to an
        /// executor for processing. This event is recorded before any action is taken.
        /// </summary>
        /// <param name="queryContext">The query context.</param>
        public virtual void RequestReceived(GraphQueryExecutionContext queryContext)
        {
            if (!this.IsEnabled(LogLevel.Debug))
                return;

            var entry = new RequestReceivedLogEntry(queryContext);
            this.LogEvent(LogLevel.Trace, entry);
        }

        /// <summary>
        /// Queries the plan cache fetch hit.
        /// </summary>
        /// <typeparam name="TSchema">The type of the t schema.</typeparam>
        /// <param name="queryHash">The query hash.</param>
        public virtual void QueryPlanCacheFetchHit<TSchema>(string queryHash)
            where TSchema : class, ISchema
        {
            if (!this.IsEnabled(LogLevel.Trace))
                return;

            var entry = new QueryPlanCacheHitLogEntry<TSchema>(queryHash);
            this.LogEvent(LogLevel.Trace, entry);
        }

        /// <summary>
        /// Queries the plan cache fetch miss.
        /// </summary>
        /// <typeparam name="TSchema">The type of the t schema.</typeparam>
        /// <param name="queryHash">The query hash.</param>
        public virtual void QueryPlanCacheFetchMiss<TSchema>(string queryHash)
            where TSchema : class, ISchema
        {
            if (!this.IsEnabled(LogLevel.Trace))
                return;

            var entry = new QueryPlanCacheMissLogEntry<TSchema>(queryHash);
            this.LogEvent(LogLevel.Trace, entry);
        }

        /// <summary>
        /// Queries the plan cached.
        /// </summary>
        /// <param name="queryHash">The query hash.</param>
        /// <param name="queryPlan">The query plan.</param>
        public virtual void QueryPlanCached(string queryHash, IGraphQueryPlan queryPlan)
        {
            if (!this.IsEnabled(LogLevel.Debug))
                return;

            var entry = new QueryPlanCacheAddLogEntry(queryHash, queryPlan);
            this.LogEvent(LogLevel.Debug, entry);
        }

        /// <summary>
        /// Recorded when an executor finishes creating a query plan and is ready to
        /// cache and execute against it.
        /// </summary>
        /// <param name="queryPlan">The generated query plan.</param>
        public virtual void QueryPlanGenerated(IGraphQueryPlan queryPlan)
        {
            if (!this.IsEnabled(LogLevel.Debug))
                return;

            var entry = new QueryPlanGeneratedLogEntry(queryPlan);
            this.LogEvent(LogLevel.Debug, entry);
        }

        /// <summary>
        /// Recorded by a field resolver when it starts resolving a field context and
        /// set of source items given to it. This occurs prior to the middleware pipeline being executed.
        /// </summary>
        /// <param name="context">The field resolution context that is being completed.</param>
        public virtual void FieldResolutionStarted(FieldResolutionContext context)
        {
            if (!this.IsEnabled(LogLevel.Trace))
                return;

            var entry = new FieldResolutionStartedLogEntry(context);
            this.LogEvent(LogLevel.Trace, entry);
        }

        /// <summary>
        /// Recorded when the security middleware invokes a security challenge
        /// against a <see cref="ClaimsPrincipal" />.
        /// </summary>
        /// <param name="context">The authorization context that contains the request to be authorized.</param>
        public virtual void FieldResolutionSecurityChallenge(GraphFieldAuthorizationContext context)
        {
            if (!this.IsEnabled(LogLevel.Trace))
                return;

            var entry = new FieldAuthorizationStartedLogEntry(context);
            this.LogEvent(LogLevel.Trace, entry);
        }

        /// <summary>
        /// Recorded when the security middleware completes a security challenge and renders a
        /// result.
        /// </summary>
        /// <param name="context">The authorization context that completed authorization.</param>
        public virtual void FieldResolutionSecurityChallengeResult(GraphFieldAuthorizationContext context)
        {
            var logLevel = context.Result.Status == FieldAuthorizationStatus.Unauthorized
                ? LogLevel.Warning
                : LogLevel.Trace;

            if (!this.IsEnabled(logLevel))
                return;

            var entry = new FieldAuthorizationCompletedLogEntry(context);
            this.LogEvent(logLevel, entry);
        }

        /// <summary>
        /// Recorded by a field resolver when it completes resolving a field context (and its children).
        /// This occurs after the middleware pipeline is executed.
        /// </summary>
        /// <param name="context">The context of the field resolution that was completed.</param>
        public virtual void FieldResolutionCompleted(FieldResolutionContext context)
        {
            if (!this.IsEnabled(LogLevel.Trace))
                return;

            var entry = new FieldResolutionCompletedLogEntry(context);
            this.LogEvent(LogLevel.Trace, entry);
        }

        /// <summary>
        /// Recorded when a controller begins the invocation of an action method to resolve
        /// a field request.
        /// </summary>
        /// <param name="action">The action method on the controller being invoked.</param>
        /// <param name="request">The request being completed by the action method.</param>
        public virtual void ActionMethodInvocationRequestStarted(IGraphMethod action, IDataRequest request)
        {
            if (!this.IsEnabled(LogLevel.Trace))
                return;

            var entry = new ActionMethodInvocationStartedLogEntry(action, request);
            this.LogEvent(LogLevel.Trace, entry);
        }

        /// <summary>
        /// Recorded when a controller completes validation of the model data that will be passed
        /// to the action method.
        /// </summary>
        /// <param name="action">The action method on the controller being invoked.</param>
        /// <param name="request">The request being completed by the action method.</param>
        /// <param name="modelState">The model data that was validated.</param>
        public virtual void ActionMethodModelStateValidated(IGraphMethod action, IDataRequest request, InputModelStateDictionary modelState)
        {
            if (!this.IsEnabled(LogLevel.Trace))
                return;

            var entry = new ActionMethodModelStateValidatedLogEntry(action, request, modelState);
            this.LogEvent(LogLevel.Trace, entry);
        }

        /// <summary>
        /// Recorded when the invocation of action method generated a known exception; generally
        /// related to target invocation errors.
        /// </summary>
        /// <param name="action">The action method on the controller being invoked.</param>
        /// <param name="request">The request being completed by the action method.</param>
        /// <param name="exception">The exception that was generated.</param>
        public virtual void ActionMethodInvocationException(IGraphMethod action, IDataRequest request, Exception exception)
        {
            if (!this.IsEnabled(LogLevel.Error))
                return;

            var entry = new ActionMethodInvocationExceptionLogEntry(action, request, exception);
            this.LogEvent(LogLevel.Error, entry);
        }

        /// <summary>
        /// Recorded when the invocation of action method generated an unknown exception. This
        /// event is called when custom resolver code throws an unhandled exception.
        /// </summary>
        /// <param name="action">The action method on the controller being invoked.</param>
        /// <param name="request">The request being completed by the action method.</param>
        /// <param name="exception">The exception that was generated.</param>
        public virtual void ActionMethodUnhandledException(IGraphMethod action, IDataRequest request, Exception exception)
        {
            if (!this.IsEnabled(LogLevel.Error))
                return;

            var entry = new ActionMethodUnhandledExceptionLogEntry(action, request, exception);
            this.LogEvent(LogLevel.Error, entry);
        }

        /// <summary>
        /// Recorded after a controller invokes and receives a result from an action method.
        /// </summary>
        /// <param name="action">The action method on the controller being invoked.</param>
        /// <param name="request">The request being completed by the action method.</param>
        /// <param name="result">The result object that was returned from the action method.</param>
        public virtual void ActionMethodInvocationCompleted(IGraphMethod action, IDataRequest request, object result)
        {
            if (!this.IsEnabled(LogLevel.Trace))
                return;

            var entry = new ActionMethodInvocationCompletedLogEntry(action, request, request);
            this.LogEvent(LogLevel.Trace, entry);
        }

        /// <summary>
        /// Recorded by an executor after the entire graphql operation has been completed
        /// and final results have been generated.
        /// </summary>
        /// <param name="queryContext">The query context.</param>
        public virtual void RequestCompleted(GraphQueryExecutionContext queryContext)
        {
            if (!this.IsEnabled(LogLevel.Debug))
                return;

            var entry = new RequestCompletedLogEntry(queryContext);
            this.LogEvent(LogLevel.Trace, entry);
        }

        /// <summary>
        /// Checks if the given <paramref name="logLevel" /> is enabled.
        /// </summary>
        /// <param name="logLevel">level to be checked.</param>
        /// <returns><c>true</c> if enabled.</returns>
        public virtual bool IsEnabled(LogLevel logLevel)
        {
            return _logger.IsEnabled(logLevel);
        }

        /// <summary>
        /// Logs the event.
        /// </summary>
        /// <param name="logLevel">The log level.</param>
        /// <param name="logEntry">The log entry.</param>
        public virtual void LogEvent(LogLevel logLevel, GraphLogEntry logEntry)
        {
            logEntry.AddProperty(LogPropertyNames.SCOPE_ID, _loggerInstanceId);
            this.Log(logLevel, logEntry);
        }

        /// <summary>
        /// Writes the provided entry to the log.
        /// </summary>
        /// <param name="logLevel">The log level to record the entry as.</param>
        /// <param name="logEntry">The log entry to record.</param>
        public virtual void Log(LogLevel logLevel, IGraphLogEntry logEntry)
        {
            if (logEntry == null || !this.IsEnabled(logLevel))
                return;

            this.Log(logLevel, logEntry.EventId, logEntry, null, (state, _) => state.ToString());
        }

        /// <summary>
        /// Writes a log entry.
        /// </summary>
        /// <typeparam name="TState">The type of the t state.</typeparam>
        /// <param name="logLevel">Entry will be written on this level.</param>
        /// <param name="eventId">Id of the event.</param>
        /// <param name="state">The entry to be written. Can be also an object.</param>
        /// <param name="exception">The exception related to this entry.</param>
        /// <param name="formatter">Function to create a <c>string</c> message of the <paramref name="state" /> and <paramref name="exception" />.</param>
        public virtual void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            _logger.Log(logLevel, eventId, state, exception, formatter);
        }

        /// <summary>
        /// Begins a logical operation scope.
        /// </summary>
        /// <typeparam name="TState">The type of the t state.</typeparam>
        /// <param name="state">The identifier for the scope.</param>
        /// <returns>An IDisposable that ends the logical operation scope on dispose.</returns>
        public virtual IDisposable BeginScope<TState>(TState state)
        {
            return _logger.BeginScope(state);
        }
    }
}