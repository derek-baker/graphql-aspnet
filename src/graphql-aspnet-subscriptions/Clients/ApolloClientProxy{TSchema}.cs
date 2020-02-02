﻿// *************************************************************
// project:  graphql-aspnet
// --
// repo: https://github.com/graphql-aspnet
// docs: https://graphql-aspnet.github.io
// --
// License:  MIT
// *************************************************************

namespace GraphQL.AspNet.Messaging
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Runtime.InteropServices.ComTypes;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using GraphQL.AspNet.Common;
    using GraphQL.AspNet.Common.Extensions;
    using GraphQL.AspNet.Configuration;
    using GraphQL.AspNet.Interfaces.Messaging;
    using GraphQL.AspNet.Interfaces.TypeSystem;
    using GraphQL.AspNet.Messaging.Handlers;
    using GraphQL.AspNet.Messaging.Messages;
    using GraphQL.AspNet.Messaging.Messages.Common;
    using GraphQL.AspNet.Messaging.ServerMessages;
    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// This object wraps a connected websocket to characterize it and provide
    /// GraphQL subscription support.
    /// </summary>
    /// <typeparam name="TSchema">The type of the schema this registration is built for.</typeparam>
    public class ApolloClientProxy<TSchema> : IApolloClientProxy
        where TSchema : class, ISchema
    {
        /// <summary>
        /// Raised when a new message is received from a connected apollo client.
        /// </summary>
        public event OperationMessageRecievedEventHandler MessageRecieved;

        private SchemaSubscriptionOptions<TSchema> _options;
        private HttpContext _context;
        private WebSocket _socket;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApolloClientProxy{TSchema}" /> class.
        /// </summary>
        /// <param name="context">The governing http context for this connection.</param>
        /// <param name="socket">The socket connection with the client.</param>
        /// <param name="options">The options used to configure the registration.</param>
        public ApolloClientProxy(HttpContext context, WebSocket socket, SchemaSubscriptionOptions<TSchema> options)
        {
            _context = Validation.ThrowIfNullOrReturn(context, nameof(context));
            _socket = Validation.ThrowIfNullOrReturn(socket, nameof(socket));
            _options = Validation.ThrowIfNullOrReturn(options, nameof(options));
        }

        private void ApolloSubscriptionRegistration_MessageRecieved(object sender, OperationMessageReceivedEventArgs e)
        {
            if (sender != this)
                return;

            if (e.Message == null)
                return;

            var handler = OperationMessageFactory.CreateHandler(e.Message.Type);
            var outgoingMessages = handler.HandleMessage(e.Message);
            if (outgoingMessages != null && outgoingMessages.Any())
            {
                // messages must be sent in the order recieved
                // each must be awaited individually
                foreach (var message in outgoingMessages)
                {
                    var task = this.SendMessage(message);
                    task.Wait();

                    var exception = task.UnwrapException();
                    if (exception != null)
                        throw exception;
                }
            }
        }

        /// <summary>
        /// Performs initial setup and acknowledgement of this subscription instance and brokers messages
        /// between the client and the graphql runtime for its lifetime.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task MaintainConnection()
        {
            this.MessageRecieved += this.ApolloSubscriptionRegistration_MessageRecieved;

            (var result, var bytes) = await _socket.ReceiveFullMessage(_options.MessageBufferSize);

            // register the socket with an "apollo level" keep alive monitor
            // that will send structured keep alive messages down the pipe
            var keepAliveTimer = new ApolloClientConnectionKeepAliveMonitor(this, _options.KeepAliveInterval);
            keepAliveTimer.Start();

            // message dispatch loop
            while (!result.CloseStatus.HasValue)
            {
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = this.DeserializeMessage(bytes);
                    this.MessageRecieved?.Invoke(this, new OperationMessageReceivedEventArgs(message));
                }

                (result, bytes) = await _socket.ReceiveFullMessage(_options.MessageBufferSize);
            }

            // shut down the socket and the keep alive
            keepAliveTimer.Stop();
            await _socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            _socket = null;

            // unregister any events that may be listening, this subscription is shutting down for good.
            foreach (Delegate d in this.MessageRecieved.GetInvocationList())
            {
                this.MessageRecieved -= (OperationMessageRecievedEventHandler)d;
            }
        }

        /// <summary>
        /// Deserializes the text message (represneted as a UTF-8 encoded byte array) into an
        /// appropriate <see cref="IGraphQLOperationMessage"/>.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <returns>IGraphQLOperationMessage.</returns>
        private IGraphQLOperationMessage DeserializeMessage(IEnumerable<byte> bytes)
        {
            var text = Encoding.UTF8.GetString(bytes.ToArray());

            var options = new JsonSerializerOptions();
            options.PropertyNameCaseInsensitive = true;
            options.AllowTrailingCommas = true;
            options.ReadCommentHandling = JsonCommentHandling.Skip;

            var partialMessage = JsonSerializer.Deserialize<AnonymousClientOperationMessage>(text, options);

            return partialMessage.Convert();
        }

        /// <summary>
        /// Serializes, encodes and sends the given message down to the client.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>Task.</returns>
        public Task SendMessage(IGraphQLOperationMessage message)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new GraphQLOperationMessageConverter());

            // graphql is defined to communcate in UTF-8
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message, typeof(IGraphQLOperationMessage), options);
            if (_socket.State == WebSocketState.Open)
            {
                return _socket.SendAsync(
                    new ArraySegment<byte>(bytes, 0, bytes.Length),
                    WebSocketMessageType.Text,
                    true,
                    default);
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Gets the state of the underlying websocket connection.
        /// </summary>
        /// <value>The state.</value>
        public WebSocketState State => _socket.State;
    }
}