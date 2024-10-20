using Json.Path;
using RestClient.Client;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RestClient.Parser
{
    public static class ParserHelpers
    {
        public static string ExpandRequestVariables(string input, Document document)
        {
            return Regex.Replace(input, Constants.RegexObjectRef, match =>
            {
                var firstSegment = match.Groups[Constants.RegexObjectRefGroupName].Value.GetSegment(0, out var expression);
                var request = document.Requests.FirstOrDefault(x => x.Name == firstSegment);
                return request == null || request.Result == null
                    ? match.Value
                    : EvaluateExpression(expression, request.Result) ?? match.Value;
            });
        }

        public static string? EvaluateExpression(string expression, RequestResult requestResult)
        {
            var requestOrResponse = expression.GetSegment(0, out var remainder);
            var bodyOrHeaders = remainder.GetSegment(0, out var headerNameOrJsonPath);

            return requestOrResponse switch
            {
                "request" => bodyOrHeaders switch
                {
                    "body" => EvaluateJsonPath(headerNameOrJsonPath, requestResult.Request?.Content?.ReadAsStringAsync().Result),
                    "headers" => GetHeaderValue(headerNameOrJsonPath, requestResult.Request?.Headers),
                    _ => null,
                },
                "response" => bodyOrHeaders switch
                {
                    "body" => EvaluateJsonPath(headerNameOrJsonPath, requestResult.Response?.Content?.ReadAsStringAsync().Result),
                    "headers" => GetHeaderValue(headerNameOrJsonPath, requestResult.Response?.Headers),
                    _ => null,
                },
                _ => null,
            };
        }

        public static string? GetHeaderValue(string headerName, HttpHeaders? headers)
        {
            if (headers == null)
            {
                return null;
            }

            // Some headers may contain a list of key-value pairs separated by a semicolon.
            // Example: Set-Cookie: key1=value1; key2=value2
            // We either return the list of values or the value of a specific key.
            var headerNameFirstSegment = headerName.GetSegment(0, out var headerNameSecondSegment);

            if (!headers.TryGetValues(headerNameFirstSegment, out var values))
            {
                return null;
            }

            var x = string.IsNullOrEmpty(headerNameSecondSegment)
                ? string.Join(";", values)
                : values
                    .Select(x => x.Split(';'))
                    .SelectMany(x => x)
                    .Select(x => new { key = x.GetSegment(0, out _, "="), value = x.GetSegment(1, out _, "=") })
                    .Where(x => x.key == headerNameSecondSegment)
                    .Select(x => x.value)
                    .FirstOrDefault();

            return x;
        }

        public static string? EvaluateJsonPath(string? jsonPath, string? json)
        {
            if (jsonPath == null || json == null)
            {
                return null;
            }
            var path = JsonPath.Parse(jsonPath);
            var instance = JsonNode.Parse(json);
            return path.Evaluate(instance).ToString();
        }
    }
}
