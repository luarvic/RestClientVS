﻿using RestClient.Client;
using RestClient.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RestClient
{
    public class Request : ParseItem
    {
        private readonly Document _document;

        public Request(Document document, ParseItem method, ParseItem url, ParseItem? version) : base(method.Start, method.Text, document, ItemType.Request)
        {
            _document = document;
            Method = method;
            Url = url;
            Version = version;
        }

        public List<ParseItem>? Children { get; set; } = new List<ParseItem>();

        public ParseItem Method { get; }
        public ParseItem Url { get; }

        public ParseItem? Version { get; }

        public List<Header>? Headers { get; } = new();

        public string? Body { get; set; }
        public override int Start => Method?.Start ?? 0;
        public override int End => Children.LastOrDefault()?.End ?? 0;
        public bool IsActive { get; set; }
        public bool LastRunWasSuccess { get; set; } = true;
        public string? Name { get; set; }
        public RequestResult? Result { get; set; }
        private Action _completionAction { get; set; }

        public void StartActive(Action OnCompletion)
        {
            _completionAction = OnCompletion;
            IsActive = true;
            LastRunWasSuccess = true;
        }
        public void EndActive(bool IsSuccessStatus)
        {
            IsActive = false;
            LastRunWasSuccess = IsSuccessStatus;
            _completionAction?.Invoke();
        }

        public override string ToString()
        {
            StringBuilder sb = new();

            sb.AppendLine($"{Method?.Text} {Url?.ExpandVariables()}");

            foreach (Header header in Headers!)
            {
                sb.AppendLine($"{header?.Name?.ExpandVariables()}: { header?.Value?.ExpandVariables()}");
            }

            if (!string.IsNullOrEmpty(Body))
            {
                sb.AppendLine(ExpandBodyVariables());
            }

            return sb.ToString().Trim();
        }

        public string ExpandBodyVariables()
        {
            if (Body == null)
            {
                return "";
            }

            // Then replace the references with the expanded values
            var clean = Body;

            if (_document.VariablesExpanded != null)
            {
                foreach (var key in _document.VariablesExpanded.Keys)
                {
                    clean = clean.Replace("{{" + key + "}}", _document.VariablesExpanded[key].Trim());
                }
            }

            clean = ParserHelpers.ExpandRequestVariables(clean, Document);

            return clean.Trim();
        }
    }
}
