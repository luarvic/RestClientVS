using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace RestClient
{
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
        public static string GetSegment(this string input, int index, out string remainder, string separator = ".")
        {
            var segments = input.Split(separator.ToCharArray()).ToArray();
            remainder = string.Join(separator, segments.Skip(index + 1).ToArray());
            return segments.ElementAtOrDefault(index);
        }

        public static Tuple<string, string> SplitIntoTuple(this string input, char separator = '=')
        {
            var values = input.Split(separator);
            return new Tuple<string, string>(values.FirstOrDefault(), values.Skip(1).FirstOrDefault());
        }
    }
}
