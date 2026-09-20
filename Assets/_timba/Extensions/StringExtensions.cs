using UnityEngine;
using System.Text.RegularExpressions;
using System;
using System.Linq;

public static class StringExtensions
{
    // Method to bold all numbers in a string
    public static string BoldNumbers(this string input) => Regex.Replace(input, @"\d+", "<b>$0</b>");

    // Method that put the first letter of a string in uppercase
    public static string CapitalizeFirstLetter(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpper(input[0]) + input.Substring(1);
    }

    /// <summary>
    /// Inserts spaces before uppercase letters in a string to split concatenated words.
    /// </summary>
    /// <param name="input">The string to split by uppercase letters.</param>
    /// <returns>A string with spaces inserted before uppercase letters.</returns>
    public static string SplitByUpperCase(this string input)
    {
        // Return original string if null or empty
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Insert space before:
        // 1. Uppercase letter preceded by lowercase letter
        // 2. Uppercase letter preceded by uppercase when next is lowercase
        // This handles cases like "JSONParser" correctly
        var result = Regex.Replace(
            input,
            @"(?<=[a-z])([A-Z])|(?<=[A-Z])([A-Z])(?=[a-z])",
            " $1$2"
        );

        return result.Trim();
    }

    /// <summary>
    /// Removes spaces preceding uppercase letters in the input string.
    /// </summary>
    /// <param name="input">The string to compact by removing spaces before uppercase letters.</param>
    /// <returns>A string with spaces before uppercase letters removed.</returns>
    public static string CompactByUpperCase(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Remove spaces before uppercase letters
        var result = Regex.Replace(input, @"\s+(?=[A-Z])", "");
        return result;
    }


    /// <summary>
    /// Extracts and returns the initials from each PascalCase or camelCase word in the input string.
    /// </summary>
    /// <param name="input">The string from which to extract initials.</param>
    /// <returns>A string containing the initials of each word, or an empty string if the input is null or whitespace.</returns>
    public static string GetInitials(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Match words in PascalCase / camelCase
        var matches = Regex.Matches(input, @"[A-Z][a-z]*");

        return string.Concat(matches.Select(m => m.Value[0]));
    }

    /// <summary>
    /// Performs a case-insensitive comparison of two strings.
    /// </summary>
    /// <param name="str1">The first string to compare.</param>
    /// <param name="str2">The second string to compare.</param>
    /// <returns>true if the strings are equal ignoring case; otherwise, false.</returns>
    public static bool Compare(this string str1, string str2)
    {
        return string.Equals(str1, str2, StringComparison.OrdinalIgnoreCase);
    }
}
