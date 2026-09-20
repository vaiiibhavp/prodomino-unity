using System;

public static class RandomUtils
{
    private static readonly Random random = new Random();

    /// <summary>
    /// Generates a random number with exactly the specified number of digits.
    /// </summary>
    /// <param name="digits">Number of digits (must be >= 1 and <= 18 for long)</param>
    public static long GenerateRandomNumber(int digits)
    {
        if (digits < 1 || digits > 18)
            throw new ArgumentOutOfRangeException(nameof(digits), "Digits must be between 1 and 18 for type long.");

        // Calculate minimum and maximum values for the given number of digits
        long min = (long)Math.Pow(10, digits - 1);     // e.g., digits=3 Å® 100
        long max = (long)Math.Pow(10, digits) - 1;     // e.g., digits=3 Å® 999

        // Generate random number in that range
        return (long)(random.NextDouble() * (max - min + 1)) + min;
    }
}
