using System;
using System.Linq;

namespace RestClient.Parser;

public static class StringExtensions
{
    /// <summary>
    /// Performs culture-invariant, case insensitive comparison to see if a string
    /// is a match for the supplied token string. The test string is trimmed of
    /// spaces first.
    /// </summary>
    public static bool IsTokenMatch(this string input, string token) =>
        string.Compare(input.Trim(), token, StringComparison.InvariantCultureIgnoreCase) == 0;

    /// <summary>
    /// Returns the first token from a list of tokens with the specified separator.
    /// This is used mainly to fetch the MIME type from content-type headers.
    /// </summary>
    public static string GetFirstToken(this string input, char separator = ';') =>
        input.Split(separator)[0];

    /// <summary>
    /// Returns the segment at the specified index from a string separated by a separator.
    /// </summary>
    /// <param name="input">The string with separators.</param>
    /// <param name="index">The index.</param>
    /// <param name="remainder">The output parameter containing the string after the segment.</param>
    /// <param name="separator">The separator.</param>
    /// <returns>The segment.</returns>
    public static string GetSegment(this string input, int index, out string remainder, string separator = Constants.RegexReferenceDelimiter)
    {
        var segments = input.Split(separator.ToCharArray()).ToArray();
        remainder = string.Join(separator, segments.Skip(index + 1).ToArray());
        return segments.ElementAtOrDefault(index);
    }

    /// <summary>
    /// Splits a string into a tuple using the specified separator.
    /// </summary>
    /// <param name="input">The string to be separated into a tuple.</param>
    /// <param name="separator">The separator character.</param>
    /// <returns>The tuple.</returns>
    /// <exception cref="ArgumentException"></exception>
    public static Tuple<string, string> SplitIntoTuple(this string input, char separator = '=')
    {
        var separatorIndex = input.IndexOf(separator);
        if (separatorIndex == 0)
        {
            throw new ArgumentException("Separator not found in input string.");
        }
        string beforeSeparator = input.Substring(0, separatorIndex);
        string afterSeparator = input.Substring(separatorIndex + 1);
        return new Tuple<string, string>(beforeSeparator.Trim(), afterSeparator.Trim());
    }
}
