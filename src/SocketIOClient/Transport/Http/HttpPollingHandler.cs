﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SocketIOClient.Extensions;

namespace SocketIOClient.Transport.Http
{
    public abstract class HttpPollingHandler : IHttpPollingHandler
    {
        public HttpPollingHandler(HttpClient httpClient)
        {
            HttpClient = httpClient;
        }

        protected HttpClient HttpClient { get; set; }
        public Action<string> OnTextReceived { get; set; }
        public Action<byte[]> OnBytesReceived { get; set; }

        protected string AppendRandom(string uri)
        {
            return uri + "&t=" + DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        public async Task GetAsync(string uri, CancellationToken cancellationToken)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, AppendRandom(uri));
            var resMsg = await HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!resMsg.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Response status code does not indicate success: {resMsg.StatusCode}");
            }
            await ProduceMessageAsync(resMsg).ConfigureAwait(false);
        }

        public async Task SendAsync(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var resMsg = await HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!resMsg.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Response status code does not indicate success: {resMsg.StatusCode}");
            }
            await ProduceMessageAsync(resMsg).ConfigureAwait(false);
        }

        public async virtual Task PostAsync(string uri, string content, CancellationToken cancellationToken)
        {
            var httpContent = new StringContent(content);
            var resMsg = await HttpClient.PostAsync(AppendRandom(uri), httpContent, cancellationToken).ConfigureAwait(false);
            await ProduceMessageAsync(resMsg).ConfigureAwait(false);
        }

        public abstract Task PostAsync(string uri, IEnumerable<byte[]> bytes, CancellationToken cancellationToken);

        private async Task ProduceMessageAsync(HttpResponseMessage resMsg)
        {
            if (resMsg.Content.Headers.ContentType.MediaType == "application/octet-stream")
            {
                byte[] bytes = await resMsg.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                ProduceBytes(bytes);
            }
            else
            {
                string text = await resMsg.Content.ReadAsStringAsync().ConfigureAwait(false);
                ProduceText(text);
            }
        }

        protected abstract void ProduceText(string text);

        protected void OnText(string text)
        {
            OnTextReceived.TryInvoke(text);
        }

        protected void OnBytes(byte[] bytes)
        {
            OnBytesReceived.TryInvoke(bytes);
        }

        private void ProduceBytes(byte[] bytes)
        {
            int i = 0;
            while (bytes.Length > i + 4)
            {
                byte type = bytes[i];
                var builder = new StringBuilder();
                i++;
                while (bytes[i] != byte.MaxValue)
                {
                    builder.Append(bytes[i]);
                    i++;
                }
                i++;
                int length = int.Parse(builder.ToString());
                if (type == 0)
                {
                    var buffer = new byte[length];
                    Buffer.BlockCopy(bytes, i, buffer, 0, buffer.Length);
                    OnText(Encoding.UTF8.GetString(buffer));
                }
                else if (type == 1)
                {
                    var buffer = new byte[length - 1];
                    Buffer.BlockCopy(bytes, i + 1, buffer, 0, buffer.Length);
                    OnBytes(buffer);
                }
                i += length;
            }
        }
    }
}