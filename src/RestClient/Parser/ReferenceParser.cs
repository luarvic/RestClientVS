using Json.Path;
using RestClient.Client;
using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RestClient.Parser;

public static class ReferenceParser
{
    private const string RegexObjectRefGroupName = "object";
    private const string GlobalContextReference = "_";
    private const string CookiesReference = "cookies";
    private const string RequestReference = "request";
    private const string ResponseReference = "response";
    private const string HeadersReference = "headers";
    private const string BodyReference = "body";
    private const string EnvironmentReference = "environment";

    public static string Parse(string input, Document document)
    {
        return Regex.Replace(input, Constants.RegexReference, match =>
        {
            var firstSegment = match.Groups[RegexObjectRefGroupName].Value.GetSegment(0, out var expression);

            // Replace reference with the variable value.
            if (string.IsNullOrEmpty(expression))
            {
                var variable = document.Variables.FirstOrDefault(x => x.Name.Text == $"@{firstSegment}");
                return variable != null
                    ? variable.Value.Text
                    : match.Value;
            }

            // Replace reference with the evaluated expression.
            switch (firstSegment)
            {
                case GlobalContextReference:
                    return EvaluateExpression(expression) ?? match.Value;
                default:
                    var request = document.Requests
                        .FirstOrDefault(x => x.Name?.Text == firstSegment);
                    return request == null || request.Result == null
                        ? match.Value
                        : EvaluateExpression(expression, request.Result) ?? match.Value;
            }
        });
    }

    public static string? EvaluateExpression(string expression, RequestResult requestResult)
    {
        var firstSegment = expression.GetSegment(0, out var remainder);
        var secondSegment = remainder.GetSegment(0, out var thirdSegment);

        return firstSegment switch
        {
            RequestReference => secondSegment switch
            {
                BodyReference => EvaluateJsonPath(thirdSegment, requestResult.Request?.Content?.ReadAsStringAsync().Result),
                HeadersReference => GetHeaderValue(thirdSegment, requestResult.Request?.Headers),
                _ => null,
            },
            ResponseReference => secondSegment switch
            {
                BodyReference => EvaluateJsonPath(thirdSegment, requestResult.Response?.Content?.ReadAsStringAsync().Result),
                HeadersReference => GetHeaderValue(thirdSegment, requestResult.Response?.Headers),
                _ => null,
            },
            _ => null,
        };
    }

    public static string? EvaluateExpression(string expression)
    {
        var firstSegment = expression.GetSegment(0, out var remainder);
        var secondSegment = remainder.GetSegment(0, out var thirdSegment);

        return firstSegment switch
        {
            CookiesReference => Cookies.GetInstance().Get(secondSegment, thirdSegment)?.Value,
            EnvironmentReference => Environment.GetEnvironmentVariable(secondSegment),
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
