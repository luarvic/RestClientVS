using System.Text.RegularExpressions;

namespace RestClient.Parser;

public static class RegexExtensions
{
    public static bool IsMatch(this Regex regex, string line, out Match match)
    {
        match = regex.Match(line);
        return match.Success;
    }
}
