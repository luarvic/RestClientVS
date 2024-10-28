using System.Collections.Generic;
using System.Linq;

namespace RestClient.Parser;

public partial class Document
{
    private Dictionary<string, string> _referenceValues = new();
    private string[] _lines;

    protected Document(string[] lines)
    {
        _lines = lines;
        Parse();
    }

    public List<ParseItem> Items { get; private set; } = new List<ParseItem>();
    public List<Request> Requests { get; private set; } = new();
    public List<Variable> Variables { get; private set; } = new();
    public Dictionary<string, string> ReferenceValues => _referenceValues;

    public void UpdateLines(string[] lines)
    {
        _lines = lines;
    }

    public static Document CreateFromLines(params string[] lines)
    {
        var doc = new Document(lines);
        return doc;
    }

    public ParseItem? FindItemFromPosition(int position)
    {
        ParseItem? item = Items.LastOrDefault(x => x.Contains(position));
        ParseItem? reference = item?.References.FirstOrDefault(x => x != null && x.Contains(position));
        return reference ?? item;
    }
}
