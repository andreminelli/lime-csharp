﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Lime.Messaging;
using Lime.Protocol.Network;
using Lime.Protocol.Security;
using Lime.Protocol.Serialization;
using Lime.Protocol.Serialization.Newtonsoft;
using Lime.Protocol.Server;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace Lime.Protocol.UnitTests.Common.Network
{
    public abstract class TransportTestsBase
    {
        [SetUp]
        public async Task SetUp()
        {
            ListenerUri = CreateListenerUri();
            EnvelopeSerializer = new EnvelopeSerializer(new DocumentTypeResolver().WithMessagingDocuments());
            TraceWriter = new Mock<ITraceWriter>();
            CancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await SetUpImpl();
        }

        protected virtual Task SetUpImpl() => Task.CompletedTask;

        [TearDown]
        public async Task TearDown()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                await (TransportListener?.StopAsync(cts.Token) ?? Task.CompletedTask);
            }

            TransportListener?.DisposeIfDisposable();
            CancellationTokenSource.Dispose();
        }
        
        protected async Task<(ITransport ClientTransport, ITransport ServerTransport)> GetAndOpenTargetsAsync()
        {
            TransportListener = CreateTransportListener(ListenerUri, EnvelopeSerializer);
            await TransportListener.StartAsync(CancellationToken);
            var clientTransport = new SynchronizedTransportDecorator(CreateClientTransport(EnvelopeSerializer));
            var serverTransportTask = TransportListener.AcceptTransportAsync(CancellationToken);
            await clientTransport.OpenAsync(ListenerUri, CancellationToken);
            var serverTransport = await serverTransportTask;
            await serverTransport.OpenAsync(ListenerUri, CancellationToken);
            return (clientTransport, serverTransport);
        }

        protected abstract Uri CreateListenerUri();
        
        protected abstract ITransportListener CreateTransportListener(Uri uri, IEnvelopeSerializer envelopeSerializer);

        protected abstract ITransport CreateClientTransport(IEnvelopeSerializer envelopeSerializer);
        
        public Uri ListenerUri { get; private set; }

        public ITransportListener TransportListener { get; private set; }

        public IEnvelopeSerializer EnvelopeSerializer { get; private set; }

        public Mock<ITraceWriter> TraceWriter { get; private set; }

        public CancellationTokenSource CancellationTokenSource { get; set; }
        
        public CancellationToken CancellationToken => CancellationTokenSource.Token;

        [Test]
        [Category("OpenAsync")]
        public async Task OpenAsync_NotConnected_ShouldConnect()
        {
            // Act
            var (clientTransport, serverTransport) = await GetAndOpenTargetsAsync();
            
            // Assert
            clientTransport.IsConnected.ShouldBeTrue();
            serverTransport.IsConnected.ShouldBeTrue();
        }
        
        [Test]
        [Category("OpenAsync")]
        public async Task OpenAsync_AlreadyConnectedClient_ThrowsInvalidOperationException()
        {
            // Act
            var (clientTransport, _) = await GetAndOpenTargetsAsync();
            await clientTransport.OpenAsync(ListenerUri, CancellationToken).ShouldThrowAsync<InvalidOperationException>();
        }
        
        [Test]
        [Category("OpenAsync")]
        public async Task OpenAsync_AlreadyConnectedServer_ThrowsInvalidOperationException()
        {
            // Act
            var (_, serverTransport) = await GetAndOpenTargetsAsync();
            await serverTransport.OpenAsync(ListenerUri, CancellationToken).ShouldThrowAsync<InvalidOperationException>();
        }
        
        [Test]
        [Category("SendAsync")]
        public async Task SendAsync_SessionEnvelopeFromClient_ShouldBeReceivedByServer()
        {
            // Arrange
            var session = Dummy.CreateSession();
            var (clientTransport, serverTransport) = await GetAndOpenTargetsAsync();
            
            // Act
            await clientTransport.SendAsync(session, CancellationToken);
            
            // Assert
            var actual = await serverTransport.ReceiveAsync(CancellationToken);
            actual.ShouldNotBeNull();
            var actualSession = actual.ShouldBeOfType<Session>();
            actualSession.Id.ShouldBe(session.Id);
            actualSession.From.ShouldBe(session.From);
            actualSession.To.ShouldBe(session.To);
            actualSession.State.ShouldBe(session.State);
        }
        
        [Test]
        [Category("ReceiveAsync")]
        public async Task ReceiveAsync_SessionEnvelopeFromServer_ShouldBeReceivedByClient()
        {
            // Arrange
            var session = Dummy.CreateSession(SessionState.Established);
            var (clientTransport, serverTransport) = await GetAndOpenTargetsAsync();
            await serverTransport.SendAsync(session, CancellationToken);
            
            // Act
            var actual = await clientTransport.ReceiveAsync(CancellationToken);
            
            // Assert
            actual.ShouldNotBeNull();
            var actualSession = actual.ShouldBeOfType<Session>();
            actualSession.Id.ShouldBe(session.Id);
            actualSession.From.ShouldBe(session.From);
            actualSession.To.ShouldBe(session.To);
            actualSession.State.ShouldBe(session.State);
        }        

        [Test]
        public async Task SendAsync_EstablishedSessionEnvelope_ClientShouldReceive()
        {
            // Arrange            
            var session = Dummy.CreateSession(SessionState.Established);
            var (clientTransport, serverTransport) = await GetAndOpenTargetsAsync();

            // Act
            await serverTransport.SendAsync(session, CancellationToken);
            var actual = await clientTransport.ReceiveAsync(CancellationToken);

            // Assert
            actual.ShouldNotBeNull();
            var actualSession = actual.ShouldBeOfType<Session>();
            actualSession.Id.ShouldBe(session.Id);
            actualSession.From.ShouldBe(session.From);
            actualSession.To.ShouldBe(session.To);
            actualSession.Pp.ShouldBe(session.Pp);
            actualSession.Metadata.ShouldBe(session.Metadata);
            actualSession.State.ShouldBe(session.State);
            actualSession.Scheme.ShouldBe(session.Scheme);
            actualSession.SchemeOptions.ShouldBe(session.SchemeOptions);
            actualSession.Authentication.ShouldBe(session.Authentication);
            actualSession.Compression.ShouldBe(session.Compression);
            actualSession.CompressionOptions.ShouldBe(session.CompressionOptions);
            actualSession.Encryption.ShouldBe(session.Encryption);
            actualSession.EncryptionOptions.ShouldBe(session.EncryptionOptions);
            actualSession.Reason.ShouldBe(session.Reason);
            actualSession.Metadata.ShouldBe(session.Metadata);
        }

        [Test]
        public async Task SendAsync_FullSessionEnvelope_ClientShouldReceive()
        {
            // Arrange            
            var session = Dummy.CreateSession(SessionState.Negotiating);
            var plainAuthentication = Dummy.CreatePlainAuthentication();
            session.Authentication = plainAuthentication;
            session.Compression = SessionCompression.GZip;
            session.CompressionOptions = new[] { SessionCompression.GZip, SessionCompression.None };
            session.Encryption = SessionEncryption.TLS;
            session.EncryptionOptions = new[] { SessionEncryption.TLS, SessionEncryption.None };
            session.Reason = Dummy.CreateReason();
            session.Metadata = Dummy.CreateStringStringDictionary();
            var (clientTransport, serverTransport) = await GetAndOpenTargetsAsync();

            // Act
            await serverTransport.SendAsync(session, CancellationToken);
            var actual = await clientTransport.ReceiveAsync(CancellationToken);

            // Assert
            actual.ShouldNotBeNull();
            var actualSession = actual.ShouldBeOfType<Session>();
            actualSession.Id.ShouldBe(session.Id);
            actualSession.From.ShouldBe(session.From);
            actualSession.To.ShouldBe(session.To);
            actualSession.Pp.ShouldBe(session.Pp);
            actualSession.State.ShouldBe(session.State);
            actualSession.Scheme.ShouldBe(session.Scheme);
            actualSession.SchemeOptions.ShouldBe(session.SchemeOptions);
            var actualSessionAuthentication = actualSession.Authentication.ShouldBeOfType<PlainAuthentication>();
            actualSessionAuthentication.Password.ShouldBe(plainAuthentication.Password);
            actualSession.Compression.ShouldBe(session.Compression);
            actualSession.CompressionOptions.ShouldBe(session.CompressionOptions);
            actualSession.Encryption.ShouldBe(session.Encryption);
            actualSession.EncryptionOptions.ShouldBe(session.EncryptionOptions);
            actualSession.Reason.ShouldNotBeNull();
            actualSession.Reason.Description.ShouldBe(session.Reason.Description);
            actualSession.Reason.Code.ShouldBe(session.Reason.Code);
            actualSession.Metadata.ShouldBe(session.Metadata);
        }

        [Test]
        public async Task SendAsync_ConsumedNotification_ClientShouldReceive()
        {
            // Arrange            
            var notification = Dummy.CreateNotification(Event.Consumed);
            var (clientTransport, serverTransport) = await GetAndOpenTargetsAsync();

            // Act
            await serverTransport.SendAsync(notification, CancellationToken);
            var actual = await clientTransport.ReceiveAsync(CancellationToken);

            // Assert
            actual.ShouldNotBeNull();
            var actualNotification = actual.ShouldBeOfType<Notification>();
            actualNotification.Id.ShouldBe(notification.Id);
            actualNotification.From.ShouldBe(notification.From);
            actualNotification.To.ShouldBe(notification.To);
            actualNotification.Pp.ShouldBe(notification.Pp);
            actualNotification.Metadata.ShouldBe(notification.Metadata);
            actualNotification.Event.ShouldBe(notification.Event);
            actualNotification.Reason.ShouldBe(notification.Reason);
            actualNotification.Metadata.ShouldBe(notification.Metadata);
        }

        [Test]
        public async Task SendAsync_MultipleParallelNotifications_ClientShouldReceive()
        {
            // Arrange            
            var count = Dummy.CreateRandomInt(100) + 1;
            var notifications = Enumerable.Range(0, count)
                .Select(i =>
                {
                    var notification = Dummy.CreateNotification(Event.Consumed);
                    notification.Id = EnvelopeId.NewId();
                    return notification;
                })
                .ToList();
            var (clientTransport, serverTransport) = await GetAndOpenTargetsAsync();
            
            // Act
            var sendTasks = new List<Task>();
            foreach (var notification in notifications)
            {
                sendTasks.Add(
                    serverTransport.SendAsync(notification, CancellationToken));
            }

            var receiveTasks = new List<Task<Envelope>>();
            while (count-- > 0)
            {
                receiveTasks.Add(
                    Task.Run(async () => await clientTransport.ReceiveAsync(CancellationToken),
                    CancellationToken));
            }

            await Task.WhenAll(sendTasks.Concat(receiveTasks));
            var actuals = receiveTasks.Select(t => t.Result).ToList();

            // Assert
            actuals.Count.ShouldBe(notifications.Count);
            foreach (var notification in notifications)
            {
                var actualEnvelope = actuals.FirstOrDefault(e => e.Id == notification.Id);
                actualEnvelope.ShouldNotBeNull();
                var actualNotification = actualEnvelope.ShouldBeOfType<Notification>();
                actualNotification.Id.ShouldBe(notification.Id);
                actualNotification.From.ShouldBe(notification.From);
                actualNotification.To.ShouldBe(notification.To);
                actualNotification.Pp.ShouldBe(notification.Pp);
                actualNotification.Metadata.ShouldBe(notification.Metadata);
                actualNotification.Event.ShouldBe(notification.Event);
                actualNotification.Reason.ShouldBe(notification.Reason);
                actualNotification.Metadata.ShouldBe(notification.Metadata);
            }
        }

        [Test]
        public async Task ReceiveAsync_NewSessionEnvelope_ServerShouldReceive()
        {
            // Arrange            
            var session = Dummy.CreateSession(SessionState.New);
            var (clientTransport, serverTransport) = await GetAndOpenTargetsAsync();

            // Act
            await clientTransport.SendAsync(session, CancellationToken);
            var actual = await serverTransport.ReceiveAsync(CancellationToken);

            // Assert
            actual.ShouldNotBeNull();
            var actualSession = actual.ShouldBeOfType<Session>();
            actualSession.Id.ShouldBe(session.Id);
            actualSession.From.ShouldBe(session.From);
            actualSession.To.ShouldBe(session.To);
            actualSession.Pp.ShouldBe(session.Pp);
            actualSession.Metadata.ShouldBe(session.Metadata);
            actualSession.State.ShouldBe(session.State);
            actualSession.Scheme.ShouldBe(session.Scheme);
            actualSession.SchemeOptions.ShouldBe(session.SchemeOptions);
            actualSession.Authentication.ShouldBe(session.Authentication);
            actualSession.Compression.ShouldBe(session.Compression);
            actualSession.CompressionOptions.ShouldBe(session.CompressionOptions);
            actualSession.Encryption.ShouldBe(session.Encryption);
            actualSession.EncryptionOptions.ShouldBe(session.EncryptionOptions);
            actualSession.Reason.ShouldBe(session.Reason);
            actualSession.Metadata.ShouldBe(session.Metadata);
        }

        [Test]
        public async Task ReceiveAsync_FullSessionEnvelope_ServerShouldReceive()
        {
            // Arrange            
            var session = Dummy.CreateSession(SessionState.Negotiating);
            var plainAuthentication = Dummy.CreatePlainAuthentication();
            session.Authentication = plainAuthentication;
            session.Compression = SessionCompression.GZip;
            session.CompressionOptions = new[] { SessionCompression.GZip, SessionCompression.None };
            session.Encryption = SessionEncryption.TLS;
            session.EncryptionOptions = new[] { SessionEncryption.TLS, SessionEncryption.None };
            session.Reason = Dummy.CreateReason();
            session.Metadata = Dummy.CreateStringStringDictionary();
            var (clientTransport, serverTransport) = await GetAndOpenTargetsAsync();

            // Act
            await clientTransport.SendAsync(session, CancellationToken);
            var actual = await serverTransport.ReceiveAsync(CancellationToken);

            // Assert
            actual.ShouldNotBeNull();
            var actualSession = actual.ShouldBeOfType<Session>();
            actualSession.Id.ShouldBe(session.Id);
            actualSession.From.ShouldBe(session.From);
            actualSession.To.ShouldBe(session.To);
            actualSession.Pp.ShouldBe(session.Pp);
            actualSession.State.ShouldBe(session.State);
            actualSession.Scheme.ShouldBe(session.Scheme);
            actualSession.SchemeOptions.ShouldBe(session.SchemeOptions);
            var actualSessionAuthentication = actualSession.Authentication.ShouldBeOfType<PlainAuthentication>();
            actualSessionAuthentication.Password.ShouldBe(plainAuthentication.Password);
            actualSession.Compression.ShouldBe(session.Compression);
            actualSession.CompressionOptions.ShouldBe(session.CompressionOptions);
            actualSession.Encryption.ShouldBe(session.Encryption);
            actualSession.EncryptionOptions.ShouldBe(session.EncryptionOptions);
            actualSession.Reason.ShouldNotBeNull();
            actualSession.Reason.Description.ShouldBe(session.Reason.Description);
            actualSession.Reason.Code.ShouldBe(session.Reason.Code);
            actualSession.Metadata.ShouldBe(session.Metadata);
        }

        [Test]
        public async Task CloseAsync_ConnectedTransport_PerformClose()
        {
            // Arrange
            var (clientTransport, serverTransport) = await GetAndOpenTargetsAsync();
            var session = Dummy.CreateSession(SessionState.Negotiating);
            await serverTransport.SendAsync(session, CancellationToken); // Send something to assert is connected
            var received = await clientTransport.ReceiveAsync(CancellationToken);

            // Act
            await Task.WhenAll(
                clientTransport.CloseAsync(CancellationToken),
                serverTransport.CloseAsync(CancellationToken));

            // Assert
            try
            {
                await serverTransport.SendAsync(session, CancellationToken); // Send something to assert is connected
                throw new Exception("Send was succeeded but an exception was expected");
            }
            catch (Exception ex)
            {
                ex.ShouldBeOfType<InvalidOperationException>();
            }
        }

        [Test]
        public async Task RemoteEndPoint_ConnectedTransport_ShouldEqualsClientLocalEndPoint()
        {
            // Arrange
            var (clientTransport, serverTransport) = await GetAndOpenTargetsAsync();

            // Act
            var actual = serverTransport.RemoteEndPoint;
            
            // Assert
            var actualIPEndPoint = IPEndPoint.Parse(actual);
            var expectedIPEndPoint = IPEndPoint.Parse(clientTransport.LocalEndPoint);
            actualIPEndPoint.Port.ShouldBe(expectedIPEndPoint.Port);
            var actualAddress = actualIPEndPoint.Address.MapToIPv4();
            var expectedAddress = expectedIPEndPoint.Address.MapToIPv4();
            actualAddress.ShouldBe(expectedAddress);
        }
        
        [Test]
        public async Task LocalEndPoint_ConnectedTransport_ShouldEqualsClientRemoteEndPoint()
        {
            // Arrange
            var (clientTransport, serverTransport) = await GetAndOpenTargetsAsync();

            // Act
            var actual = serverTransport.LocalEndPoint;

            // Assert
            var actualIPEndPoint = IPEndPoint.Parse(actual);
            var expectedIPEndPoint = IPEndPoint.Parse(clientTransport.RemoteEndPoint);
            actualIPEndPoint.Port.ShouldBe(expectedIPEndPoint.Port);
            var actualAddress = actualIPEndPoint.Address.MapToIPv4();
            var expectedAddress = expectedIPEndPoint.Address.MapToIPv4();
            actualAddress.ShouldBe(expectedAddress);
        }
        
        [Test]
        public async Task Options_ConnectedTransport_ReturnsValue()
        {
            // Arrange
            var (clientTransport, serverTransport) = await GetAndOpenTargetsAsync();

            // Act
            var actual = serverTransport.Options;
            
            // Assert
            actual.ShouldNotBeNull();
        }
        
        [Test]
        public async Task ReceiveAsync_InterleavedWritesAndReads_ServerShouldReceive()
        {
            // Arrange            
            var pageSize = 5;
            var count = 9 * pageSize;
            var (clientTransport, serverTransport) = await GetAndOpenTargetsAsync();
            var messages = new Message[count];

            for (int i = 0; i < count; i++)
            {
                messages[i] = Dummy.CreateMessage(Dummy.CreateRandomString(64 * 3));
            }
            var actuals = new Envelope[count];


            // Act
            for (int i = 0; i < count; i += pageSize)
            {
                await clientTransport.SendAsync(messages[i], CancellationToken);
                await clientTransport.SendAsync(messages[i + 1], CancellationToken);
                await clientTransport.SendAsync(messages[i + 2], CancellationToken);
                actuals[i] = await serverTransport.ReceiveAsync(CancellationToken);
                actuals[i + 1] = await serverTransport.ReceiveAsync(CancellationToken);
                await clientTransport.SendAsync(messages[i + 3], CancellationToken);
                actuals[i + 2] = await serverTransport.ReceiveAsync(CancellationToken);
                await clientTransport.SendAsync(messages[i + 4], CancellationToken);
                actuals[i + 3] = await serverTransport.ReceiveAsync(CancellationToken);
                actuals[i + 4] = await serverTransport.ReceiveAsync(CancellationToken);
            }

            // Assert
            for (int i = 0; i < count; i++)
            {
                var actual = actuals[i];
                var message = messages[i];
                actual.ShouldNotBeNull();
                var actualMessage = actual.ShouldBeOfType<Message>();
                CompareMessages(message, actualMessage);
            }
        }
        
        protected static void CompareMessages(Message message, Message actualMessage)
        {
            actualMessage.Id.ShouldBe(message.Id);
            actualMessage.From.ShouldBe(message.From);
            actualMessage.To.ShouldBe(message.To);
            actualMessage.Pp.ShouldBe(message.Pp);
            actualMessage.Metadata.ShouldBe(message.Metadata);
            actualMessage.Type.ShouldBe(message.Type);
            actualMessage.Content.ToString().ShouldBe(message.Content.ToString());
            actualMessage.Metadata.ShouldBe(message.Metadata);
        }
    }
}