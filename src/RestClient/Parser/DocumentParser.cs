﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RestClient
{
    public partial class Document
    {
        private static readonly Regex _regexUrl = new(@"^((?<method>get|post|patch|put|delete|head|options|trace))\s*(?<url>[^\s]+)\s*(?<version>HTTP/.*)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _regexHeader = new(@"^(?<name>[^\s]+)?([\s]+)?(?<operator>:)(?<value>.+)", RegexOptions.Compiled);
        private static readonly Regex _regexVariable = new(@"^(?<name>@[^\s]+)\s*(?<equals>=)\s*(?<value>.+)", RegexOptions.Compiled);
        private static readonly Regex _regexRef = new(@"{{[\w]+}}", RegexOptions.Compiled);
        private static readonly Regex _regexRequestVariable = new(@"^(?<declaration>#\s+@name)\s*(?<name>[_a-zA-Z][_a-zA-Z0-9]*)\s*$", RegexOptions.Compiled);

        public bool IsParsing { get; private set; }
        public bool IsValid { get; private set; }

        public void Parse()
        {
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
                ExpandVariables();
                ValidateDocument();

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

        private IEnumerable<ParseItem> ParseLine(int start, string line, List<ParseItem> tokens)
        {
            var trimmedLine = line.Trim();
            List<ParseItem> items = new();

            // Request variable declaration
            if (IsMatch(_regexRequestVariable, trimmedLine, out Match matchRequestVar))
            {
                items.Add(ToParseItem(matchRequestVar, start, "name", ItemType.RequestVariableName, false)!);
            }
            // Comment
            else if (trimmedLine.StartsWith(Constants.CommentChar.ToString()))
            {
                items.Add(ToParseItem(line, start, ItemType.Comment, false));
            }
            // Request body
            else if (IsBodyToken(line, tokens))
            {
                items.Add(ToParseItem(line, start, ItemType.Body));
            }
            // Empty line
            else if (string.IsNullOrWhiteSpace(line))
            {
                items.Add(ToParseItem(line, start, ItemType.EmptyLine));
            }
            // Variable declaration
            else if (IsMatch(_regexVariable, trimmedLine, out Match matchVar))
            {
                items.Add(ToParseItem(matchVar, start, "name", ItemType.VariableName, false)!);
                items.Add(ToParseItem(matchVar, start, "value", ItemType.VariableValue, true)!);
            }
            // Request URL
            else if (IsMatch(_regexUrl, trimmedLine, out Match matchUrl))
            {
                ParseItem method = ToParseItem(matchUrl, start, "method", ItemType.Method)!;
                ParseItem url = ToParseItem(matchUrl, start, "url", ItemType.Url)!;
                ParseItem? version = ToParseItem(matchUrl, start, "version", ItemType.Version);
                items.Add(new Request(this, method, url, version));
                items.Add(method);
                items.Add(url);

                if (version != null)
                {
                    items.Add(version);
                }
            }
            // Header
            else if (tokens.Count > 0 && IsMatch(_regexHeader, trimmedLine, out Match matchHeader))
            {
                ParseItem? prev = tokens.Last();
                if (prev?.Type == ItemType.HeaderValue || prev?.Type == ItemType.Url || prev?.Type == ItemType.Version || prev?.Type == ItemType.Comment)
                {
                    items.Add(ToParseItem(matchHeader, start, "name", ItemType.HeaderName)!);
                    items.Add(ToParseItem(matchHeader, start, "value", ItemType.HeaderValue)!);
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

        public static bool IsMatch(Regex regex, string line, out Match match)
        {
            match = regex.Match(line);
            return match.Success;
        }

        private ParseItem ToParseItem(string line, int start, ItemType type, bool supportsVariableReferences = true)
        {
            var item = new ParseItem(start, line, this, type);

            if (supportsVariableReferences)
            {
                AddVariableReferences(item);
            }

            return item;
        }

        private ParseItem? ToParseItem(Match match, int start, string groupName, ItemType type, bool supportsVariableReferences = true)
        {
            Group? group = match.Groups[groupName];

            if (string.IsNullOrEmpty(group.Value))
            {
                return null;
            }

            return ToParseItem(group.Value, start + group.Index, type, supportsVariableReferences);
        }

        private void AddVariableReferences(ParseItem token)
        {
            foreach (Match match in _regexRef.Matches(token.Text))
            {
                ParseItem? reference = ToParseItem(match.Value, token.Start + match.Index, ItemType.Reference, false);
                token.References.Add(reference);
            }
        }

        private void ValidateDocument()
        {
            IsValid = true;
            foreach (ParseItem item in Items)
            {
                // Variable references
                foreach (ParseItem? reference in item.References)
                {
                    if (VariablesExpanded != null && !VariablesExpanded.ContainsKey(reference.Text.Trim('{', '}')))
                    {
                        reference.Errors.Add(Errors.PL001.WithFormat(reference.Text.Trim('{', '}')));
                        IsValid = false;
                    }
                }

                // URLs
                if (item.Type == ItemType.Url)
                {
                    var uri = item.ExpandVariables();

                    if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
                    {
                        item.Errors.Add(Errors.PL002.WithFormat(uri));
                        IsValid = false;
                    }
                }
            }
        }

        private class Errors
        {
            public static Error PL001 { get; } = new("PL001", "The variable \"{0}\" is not defined.", ErrorCategory.Warning);
            public static Error PL002 { get; } = new("PL002", "\"{0}\" is not a valid absolute URI", ErrorCategory.Warning);
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

                    if (currentRequest != null && item.Previous?.Previous?.Type == ItemType.RequestVariableName)
                    {
                        currentRequest.Name = item.Previous?.Previous?.Text;
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

        private bool IsWwwFormContentHeader(Header header)
        {
            return header.Name.Text.IsTokenMatch("content-type") &&
                header.Value.Text.GetFirstToken().IsTokenMatch("application/x-www-form-urlencoded");
        }

        public event EventHandler? Parsed;
    }
}
