using RestClient.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RestClient.Parser;

public class Request : ParseItem
{
    private Action? _completionAction;

    public Request(Document document, ParseItem method, ParseItem url, ParseItem? version, ParseItem? name) :
        base(method.Start, method.Text, document, ItemType.Request)
    {
        Method = method;
        Url = url;
        Version = version;
        Name = name;
    }

    public List<ParseItem>? Children { get; set; } = new List<ParseItem>();
    public ParseItem Method { get; }
    public ParseItem Url { get; }
    public ParseItem? Version { get; }
    public ParseItem? Name { get; set; }
    public List<Header>? Headers { get; } = new();
    public string? Body { get; set; }
    public override int Start => Method?.Start ?? 0;
    public override int End => Children.LastOrDefault()?.End ?? 0;
    public bool IsActive { get; set; }
    public bool LastRunWasSuccess { get; set; } = true;
    public RequestResult? Result { get; set; }

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

        sb.AppendLine($"{Method?.Text} {Url?.ResolveReferences()}");

        foreach (Header header in Headers!)
        {
            sb.AppendLine($"{header?.Name?.ResolveReferences()}: {header?.Value?.ResolveReferences()}");
        }

        if (!string.IsNullOrEmpty(Body))
        {
            sb.AppendLine(ResolveBodyReferences());
        }

        return sb.ToString().Trim();
    }

    public string ResolveBodyReferences()
    {
        var clean = Body;
        clean = ReferenceParser.Parse(clean, Document);
        return clean.Trim();
    }
}
