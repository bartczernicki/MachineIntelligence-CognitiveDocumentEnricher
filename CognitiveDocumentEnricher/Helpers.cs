using System;
using System.Collections.Generic;
using System.Linq;

namespace CognitiveDocumentEnricher
{
    public static class Helpers
    {
        // Chunk the strings and then padd with 40 chars
        public static IEnumerable<string> SplitAndPadFourtyChars(string str, int chunkSize)
        {
            var test = Enumerable.Range(0, (str.Length / chunkSize) + 1).ToList();

            return Enumerable.Range(0, (str.Length / chunkSize) + 1)
                .Select(i => SafeSubstring(str, i * chunkSize, chunkSize + 40));
        }

        public static string CustomSubString(string stringToSplit, int chunkSize, int i)
        {
            var stringReturn = string.Empty;
            var isIndexLarger = IsIndexLargerThanString(stringToSplit, (i + 1)*chunkSize + 40);
            var startIndex = i * chunkSize;
            var endIndex = isIndexLarger ? stringToSplit.Length : chunkSize + 40;

            stringReturn = stringToSplit.Substring(startIndex, endIndex);

            return stringReturn;
        }

        public static bool IsIndexLargerThanString(string stringToSplit, int endIndex)
        {
            var lengthOfString = stringToSplit.Length;

            if (endIndex > lengthOfString)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static string SafeSubstring(this string text, int start, int length)
        {
            return text.Length <= start ? string.Empty
                : text.Length - start <= length ? text.Substring(start)
                : text.Substring(start, length);
        }

        public static bool IsEntity(string entityName)
        {
            var isEntity = false;

            // Is Date check
            DateTime tempDate;
            var isDate = false;
            if (DateTime.TryParse(entityName, out tempDate)) //entities that are not dates
            {
                isDate = true;
            }

            if (!(isDate))
            {
                entityName = entityName.Replace(" ", string.Empty).Replace("%", string.Empty).Replace(".", string.Empty)
                    .Replace("-", string.Empty).Replace("_", string.Empty)
                    .Replace("(", string.Empty).Replace(")", string.Empty)
                    .Replace(",", string.Empty).Replace("$", string.Empty)
                    .Replace("/", string.Empty)
                    .Replace(System.Environment.NewLine, string.Empty);

                if (
                    (entityName.Length > 2) // entities over 2 chars
                    && (!entityName.All(Char.IsDigit)) // entities that are not numbers
                    )
                {
                    isEntity = true;
                }
            }

            return isEntity;
        }
    }
}
