﻿using SocketIO.Client.Transport;
using SocketIO.Client.Transport.WebSockets;
using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using SioWebSocketState = SocketIO.Client.Transport.WebSockets.WebSocketState;
using SioWebSocketReceiveResult = SocketIO.Client.Transport.WebSockets.WebSocketReceiveResult;

#if NET461_OR_GREATER
using System.Reflection;
using System.Collections.Generic;
#endif

namespace SocketIO.Client.Windows7
{
    public class SystemNetWebSocketsClientWebSocket : IClientWebSocket
    {
        public SystemNetWebSocketsClientWebSocket()
        {
            _ws = new System.Net.WebSockets.Managed.ClientWebSocket();
#if NET461_OR_GREATER
            AllowHeaders();
#endif
        }

#if NET461_OR_GREATER
        private readonly static HashSet<string> allowHeaders = new HashSet<string>
        {
            "User-Agent"
        };

        private void AllowHeaders()
        {
            var property = _ws.Options
                .GetType()
                .GetProperty("RequestHeaders", BindingFlags.NonPublic | BindingFlags.Instance);
            var headers = property.GetValue(_ws.Options);
            var hinfoField = headers.GetType().GetField("HInfo", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var hinfo = hinfoField.GetValue(null);
            var hhtField = hinfo.GetType().GetField("HeaderHashTable", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var hashTable = hhtField.GetValue(null) as System.Collections.Hashtable;

            foreach (string key in hashTable.Keys)
            {
                if (!allowHeaders.Contains(key))
                {
                    continue;
                }
                var headerInfo = hashTable[key];
                foreach (var item in headerInfo.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                {

                    if (item.Name == "IsRequestRestricted")
                    {
                        bool isRequestRestricted = (bool)item.GetValue(headerInfo);
                        if (isRequestRestricted)
                        {
                            item.SetValue(headerInfo, false);
                        }

                    }
                }

            }
        }
#endif

        readonly System.Net.WebSockets.Managed.ClientWebSocket _ws;

        public SioWebSocketState State => (SioWebSocketState)_ws.State;

        public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            await _ws.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken).ConfigureAwait(false);
        }

        public async Task SendAsync(byte[] bytes, TransportMessageType type, bool endOfMessage, CancellationToken cancellationToken)
        {
            var msgType = WebSocketMessageType.Text;
            if (type == TransportMessageType.Binary)
            {
                msgType = WebSocketMessageType.Binary;
            }
            await _ws.SendAsync(new ArraySegment<byte>(bytes), msgType, endOfMessage, cancellationToken).ConfigureAwait(false);
        }

        public async Task<SioWebSocketReceiveResult> ReceiveAsync(int bufferSize, CancellationToken cancellationToken)
        {
            var buffer = new byte[bufferSize];
            var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            return new SioWebSocketReceiveResult
            {
                Count = result.Count,
                MessageType = (TransportMessageType)result.MessageType,
                EndOfMessage = result.EndOfMessage,
                Buffer = buffer,
            };
        }

        public void AddHeader(string key, string val)
        {
            _ws.Options.SetRequestHeader(key, val);
        }

        public void SetProxy(IWebProxy proxy) => _ws.Options.Proxy = proxy;

        public void Dispose()
        {
            _ws.Dispose();
        }
    }
}
