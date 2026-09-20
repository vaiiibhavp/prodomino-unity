using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HelperSharedLibrary
{
    /// <summary>
    /// This class is responsible for fetching and validating profanity words
    /// </summary>
    public abstract class ProfanityValidator
    {
        protected static HashSet<string> _tempBannedWords = new();

        // Cache of precompiled regex patterns for faster repeated checks
        private static readonly Dictionary<string, Regex> _regexCache = new();

        /// <summary>
        /// Initializes the profanity validator by fetching banned words
        /// and precompiling their regex equivalents for faster matching.
        /// </summary>
        public virtual async Task Initialize()
        {
            if (_tempBannedWords.Count > 0 && _regexCache.Count > 0)
                return; // Already initialized, avoid re-fetch

            _tempBannedWords = await FetchBannedWordsAsync();
            if (_tempBannedWords == null || _tempBannedWords.Count == 0)
                throw new InvalidOperationException("No banned words loaded from Remote Config.");

            _regexCache.Clear();

            // Precompile regex patterns for flexible matching
            foreach (var word in _tempBannedWords)
            {
                // Escape regex metacharacters, then allow any number of symbols/spaces between letters
                string pattern = string.Join(@"[\W_]*", Regex.Escape(word.ToLowerInvariant()));
                _regexCache[word] = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
        }

        /// <summary>
        /// Fetches the list of banned or profane words from Remote Config.
        /// </summary>
        public abstract Task<HashSet<string>> FetchBannedWordsAsync();

        /// <summary>
        /// Checks if the given text contains any banned word, even with obfuscations like spaces, symbols, or leetspeak.
        /// </summary>
        public static bool ContainsProfanity(string text, out string bannedWordReference)
        {
            bannedWordReference = string.Empty;

            if (string.IsNullOrWhiteSpace(text) || _tempBannedWords == null || _tempBannedWords.Count == 0)
                return false;

            // 1. Normalize text (remove punctuation, lowercase)
            string normalized = NormalizeText(text);

            // 2. Replace common leetspeak
            normalized = ReplaceLeetSpeak(normalized);

            // 3. Remove extra whitespace
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

            // 4. Create a stripped version (without any spaces)
            string noSpace = normalized.Replace(" ", string.Empty);

            // 5. Quick substring check (fast path)
            foreach (var banned in _tempBannedWords)
            {
                if (normalized.Contains(banned, StringComparison.OrdinalIgnoreCase) ||
                    noSpace.Contains(banned, StringComparison.OrdinalIgnoreCase))
                {
                    bannedWordReference = banned;
                    return true;
                }
            }

            // 6. Regex flexible match (handles b.o.o.b, b 0 0 b, etc.)
            foreach (var kvp in _regexCache)
            {
                var bannedWord = kvp.Key;
                var regex = kvp.Value;

                if (regex.IsMatch(text))
                {
                    bannedWordReference = bannedWord;
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Simplifies text by removing punctuation and forcing lowercase.
        /// </summary>
        protected static string NormalizeText(string input)
        {
            var chars = input.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c));
            return new string(chars.ToArray()).ToLowerInvariant();
        }

        /// <summary>
        /// Converts common leetspeak patterns (numbers -> letters).
        /// </summary>
        protected static string ReplaceLeetSpeak(string input)
        {
            return input
                .Replace('0', 'o')
                .Replace('1', 'i')
                .Replace('3', 'e')
                .Replace('4', 'a')
                .Replace('5', 's')
                .Replace('7', 't')
                .Replace('@', 'a')
                .Replace('$', 's');
        }
    }
}