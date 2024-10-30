using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RestClient.Parser;

public partial class Document
{
    private static class Errors
    {
        public static Error PL001 { get; } = new("PL001", "Unable to resolve reference \"{0}\".", ErrorCategory.Warning);
        public static Error PL002 { get; } = new("PL002", "\"{0}\" is not a valid absolute URI.", ErrorCategory.Warning);
    }

    private static readonly Regex _regexUrl = new(@"^(?<method>get|post|patch|put|delete|head|options|trace)\s*(?<url>[^\s]+)\s*(?<version>HTTP\/\S*)?\s*(?<output>>)?\s*(?<name>@[_a-zA-Z][_a-zA-Z0-9]*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _regexHeader = new(@"^(?<name>[^\s]+)?([\s]+)?(?<operator>:)(?<value>.+)", RegexOptions.Compiled);
    private static readonly Regex _regexVariable = new(@"^(?<name>@[^\s]+)\s*(?<equals>=)\s*(?<value>.+)", RegexOptions.Compiled);
    private static readonly Regex _regexReference = new(Constants.RegexReference, RegexOptions.Compiled);

    public bool IsParsing { get; private set; }

    public event EventHandler? Parsed;

    private IEnumerable<ParseItem> ParseLine(int start, string line, List<ParseItem> tokens)
    {
        var trimmedLine = line.Trim();
        List<ParseItem> items = new();

        // Comment
        if (trimmedLine.StartsWith(Constants.CommentChar.ToString()))
        {
            items.Add(ParseItem(line, start, ItemType.Comment, false));
        }
        // Request body
        else if (IsBodyToken(line, tokens))
        {
            items.Add(ParseItem(line, start, ItemType.Body));
        }
        // Empty line
        else if (string.IsNullOrWhiteSpace(line))
        {
            items.Add(ParseItem(line, start, ItemType.EmptyLine));
        }
        // Variable declaration
        else if (_regexVariable.IsMatch(trimmedLine, out Match matchVar))
        {
            items.Add(ParseItem(matchVar, start, "name", ItemType.VariableName, false)!);
            items.Add(ParseItem(matchVar, start, "value", ItemType.VariableValue, true)!);
        }
        // Request URL
        else if (_regexUrl.IsMatch(trimmedLine, out Match matchUrl))
        {
            ParseItem method = ParseItem(matchUrl, start, "method", ItemType.Method)!;
            ParseItem url = ParseItem(matchUrl, start, "url", ItemType.Url)!;
            ParseItem? version = ParseItem(matchUrl, start, "version", ItemType.Version);
            ParseItem? output = ParseItem(matchUrl, start, "output", ItemType.OutputOperator);
            ParseItem? name = ParseItem(matchUrl, start, "name", ItemType.RequestVariableName);
            items.Add(new Request(this, method, url, version, name));
            items.Add(method);
            items.Add(url);
            if (version != null)
            {
                items.Add(version);
            }
            if (output != null)
            {
                items.Add(output);
            }
            if (name != null)
            {
                items.Add(name);
            }
        }
        // Header
        else if (tokens.Count > 0 && _regexHeader.IsMatch(trimmedLine, out Match matchHeader))
        {
            ParseItem? prev = tokens.Last();
            if (prev?.Type == ItemType.HeaderValue || 
                prev?.Type == ItemType.Url || 
                prev?.Type == ItemType.Version || 
                prev?.Type == ItemType.RequestVariableName || 
                prev?.Type == ItemType.Comment)
            {
                items.Add(ParseItem(matchHeader, start, "name", ItemType.HeaderName)!);
                items.Add(ParseItem(matchHeader, start, "value", ItemType.HeaderValue)!);
            }
        }

        return items;
    }

    private bool IsBodyToken(string line, List<ParseItem> tokens)
    {
        ParseItem? prev = tokens.LastOrDefault();

        if (prev != null && string.IsNullOrWhiteSpace(prev.Text) && string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        if (prev?.Type == ItemType.Body)
        {
            return true;
        }

        if (prev?.Type != ItemType.EmptyLine)
        {
            return false;
        }

        ParseItem? parent = tokens.ElementAtOrDefault(Math.Max(0, tokens.Count - 2));

        if (parent?.Type == ItemType.HeaderValue || parent?.Type == ItemType.Url || parent?.Type == ItemType.Version || (parent?.Type == ItemType.Comment && parent?.TextExcludingLineBreaks != "###"))
        {
            return true;
        }

        return false;
    }

    private ParseItem ParseItem(string line, int start, ItemType type, bool supportsReferences = true)
    {
        var item = new ParseItem(start, line, this, type);

        if (supportsReferences)
        {
            AddReference(item);
        }

        return item;
    }

    private ParseItem? ParseItem(Match match, int start, string groupName, ItemType type, bool supportsVariableReferences = true)
    {
        Group? group = match.Groups[groupName];

        if (string.IsNullOrEmpty(group.Value))
        {
            return null;
        }

        return ParseItem(group.Value, start + group.Index, type, supportsVariableReferences);
    }

    private void AddReference(ParseItem token)
    {
        foreach (Match match in _regexReference.Matches(token.Text))
        {
            ParseItem? reference = ParseItem(match.Value, token.Start + match.Index, ItemType.Reference, false);
            token.References.Add(reference);
        }
    }

    private bool IsWwwFormContentHeader(Header header)
    {
        return header.Name.Text.IsTokenMatch("content-type") &&
            header.Value.Text.GetFirstToken().IsTokenMatch("application/x-www-form-urlencoded");
    }

    private void OrganizeItems()
    {
        Request? currentRequest = null;
        List<Request> requests = new();
        List<Variable> variables = new();

        bool isWwwForm = false;

        foreach (ParseItem? item in Items)
        {
            if (item.Type == ItemType.VariableName)
            {
                var variable = new Variable(item, item.Next!);
                variables.Add(variable);
            }

            else if (item.Type == ItemType.Method)
            {
                currentRequest = (Request)item.Previous!;

                requests.Add(currentRequest);
                currentRequest?.Children?.Add(currentRequest.Method);
                currentRequest?.Children?.Add(currentRequest.Url);

                if (currentRequest?.Version != null)
                {
                    currentRequest?.Children?.Add(currentRequest.Version);
                }

                if (currentRequest?.Name != null)
                {
                    currentRequest?.Children?.Add(currentRequest.Name);
                }
            }

            else if (currentRequest != null)
            {
                if (item.Type == ItemType.HeaderName)
                {
                    var header = new Header(item, item.Next!);

                    currentRequest?.Headers?.Add(header);
                    currentRequest?.Children?.Add(header.Name);
                    currentRequest?.Children?.Add(header.Value);

                    isWwwForm |= IsWwwFormContentHeader(header);
                }
                else if (item.Type == ItemType.Body)
                {
                    if (string.IsNullOrWhiteSpace(item.Text))
                    {
                        continue;
                    }

                    var prevEmptyLine = item.Previous?.Type == ItemType.Body && string.IsNullOrWhiteSpace(item.Previous.Text) ? item.Previous.Text : "";
                    string content = isWwwForm ? item.TextExcludingLineBreaks : item.Text;
                    currentRequest.Body += prevEmptyLine + content;
                    currentRequest?.Children?.Add(item);
                }
                else if (item?.Type == ItemType.Comment)
                {
                    if (item.Text.StartsWith("###"))
                    {
                        currentRequest = null;
                    }
                }
            }
        }

        Variables = variables;
        Requests = requests;
    }

    private void ResolveReferences()
    {
        foreach (var item in Items)
        {
            foreach (var reference in item.References)
            {
                ReferenceValues[reference.Text] = ReferenceParser.Parse(reference.Text, this);
                if (ReferenceValues[reference.Text] == reference.Text)
                {
                    reference.Errors.Add(Errors.PL001.WithFormat(reference.Text.Trim('{', '}')));
                }
            }
        }
    }

    public void Parse(bool refreshOnly = false)
    {
        if (refreshOnly)
        {
            Parsed?.Invoke(this, EventArgs.Empty);
        }

        IsParsing = true;
        var isSuccess = false;
        var start = 0;

        try
        {
            List<ParseItem> tokens = new();

            foreach (var line in _lines)
            {
                IEnumerable<ParseItem>? current = ParseLine(start, line, tokens);

                if (current != null)
                {
                    tokens.AddRange(current);
                }
                start += line.Length;
            }

            Items = tokens;

            OrganizeItems();
            ResolveReferences();

            isSuccess = true;
        }
        finally
        {
            IsParsing = false;

            if (isSuccess)
            {
                Parsed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
