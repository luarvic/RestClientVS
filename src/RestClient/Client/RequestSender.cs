﻿using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RestClient.Client
{
    public static class RequestSender
    {


        public static async Task<RequestResult> SendAsync(Request request, TimeSpan timeOut, CancellationToken cancellationToken = default)
        {
            RequestResult result = new() { RequestToken = request };
            HttpRequestMessage? requestMessage = BuildRequest(request, result);

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseDefaultCredentials = true,
            };

            using (var client = new HttpClient(handler))
            {
                client.Timeout = timeOut;

                try
                {
                    result.Response = await client.SendAsync(requestMessage, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    result.ErrorMessage = $"Request timed out after {timeOut.TotalSeconds}";
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                }
            }

            return result;
        }

        private static HttpRequestMessage BuildRequest(Request request, RequestResult result)
        {
            var url = request.Url?.ExpandVariables().Trim();
            HttpMethod method = GetMethod(request.Method?.Text);

            var message = new HttpRequestMessage(method, url); ;

            try
            {
                AddBody(request, message);
                AddHeaders(request, message);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }

            return message;
        }

        private static void AddBody(Request request, HttpRequestMessage message)
        {
            if (request.Body == null)
            {
                return;
            }

            if (message.Method == HttpMethod.Get)
            {
                throw new HttpRequestException($"A request body is not supported for {message.Method} requests.");
            }

            message.Content = new StringContent(request.ExpandBodyVariables());
        }

        public static void AddHeaders(Request request, HttpRequestMessage message)
        {
            if (request.Headers != null)
            {
                foreach (Header header in request.Headers)
                {
                    var name = header?.Name?.ExpandVariables();
                    var value = header?.Value?.ExpandVariables();

                    if (name!.Equals("content-type", StringComparison.OrdinalIgnoreCase) && request.Body != null)
                    {
                        // Remove name-value pairs that can follow the MIME type
                        string mimeType = value!.GetFirstToken();

                        message.Content = new StringContent(request.ExpandBodyVariables(), System.Text.Encoding.UTF8, mimeType);
                    }

                    message.Headers.TryAddWithoutValidation(name, value);
                }
            }

            if (!message.Headers.Contains("User-Agent"))
            {
                message.Headers.Add("User-Agent", nameof(RestClient));
            }
        }

        private static HttpMethod GetMethod(string? methodName)
        {
            return methodName?.ToLowerInvariant() switch
            {
                "head" => HttpMethod.Head,
                "post" => HttpMethod.Post,
                "put" => HttpMethod.Put,
                "delete" => HttpMethod.Delete,
                "options" => HttpMethod.Options,
                "trace" => HttpMethod.Trace,
                "patch" => new HttpMethod("PATCH"),
                _ => HttpMethod.Get,
            };
        }
    }
}
