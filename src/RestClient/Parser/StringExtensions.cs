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
        /// Expands request variables in a string by replacing them with their values.
        /// </summary>
        /// <param name="input">The string to process.</param>
        /// <param name="document">The document that contains requests.</param>
        public static string ExpandRequestVariables(this string input, Document document)
        {
            return Regex.Replace(input, Constants.RegexObjectRef, match =>
            {
                var firstSegment = match.Groups[Constants.RegexObjectRefGroupName].Value.GetSegment(0, out var remainder);
                var request = document.Requests.FirstOrDefault(x => x.Name == firstSegment);
                return request == null || request.Result == null ? match.Value : remainder.GetPropertyValue(request.Result);
            });
        }

        /// <summary>
        /// Returns the string value of a property from an object by its string representation.
        /// E.g. "Foo.Bar".GetPropertyValue(obj) would return obj.Foo.Bar.ToString().
        /// </summary>
        /// <param name="input">The string that represent the object property.</param>
        /// <param name="obj">The object.</param>
        /// <returns>The object.</returns>
        public static string GetPropertyValue(this string input, object obj)
        {
            var firstSegment = input.GetSegment(0, out var remainder);
            if (string.IsNullOrEmpty(firstSegment))
            {
                return obj.ToString();
            }
            var value = obj.GetType().GetProperty(firstSegment)?.GetValue(obj);
            if (value == null)
            {
                return string.Empty;
            }
            return remainder.GetPropertyValue(value);
        }

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
            return segments[index];
        }
    }
}
