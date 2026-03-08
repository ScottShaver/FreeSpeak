namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service to handle emoji conversions and text replacements
    /// </summary>
    public static class EmojiService
    {
        private static readonly Dictionary<string, string> EmojiTextReplacements = new()
        {
            // Smiley faces
            [":)"] = "🙂",
            [":-)"] = "🙂",
            [":("] = "🙁",
            [":-("] = "🙁",
            [":D"] = "😀",
            [":-D"] = "😀",
            [";)"] = "😉",
            [";-)"] = "😉",
            [":P"] = "😛",
            [":-P"] = "😛",
            [":p"] = "😛",
            [":-p"] = "😛",
            ["XD"] = "😆",
            ["xD"] = "😆",
            [":*"] = "😘",
            [":-*"] = "😘",
            ["<3"] = "❤️",
            ["</3"] = "💔",
            [":'("] = "😢",
            [":')"] = "😂",
            ["O:)"] = "😇",
            ["O:-)"] = "😇",
            [">:("] = "😠",
            [">:-("] = "😠",
            [":|"] = "😐",
            [":-|"] = "😐",
            [":o"] = "😮",
            [":-o"] = "😮",
            [":O"] = "😮",
            [":-O"] = "😮",
            ["8)"] = "😎",
            ["8-)"] = "😎",
            ["B)"] = "😎",
            ["B-)"] = "😎",
            
            // Gestures
            ["(y)"] = "👍",
            ["(n)"] = "👎",
            ["(Y)"] = "👍",
            ["(N)"] = "👎",
            
            // Expressions
            [":s"] = "😕",
            [":-s"] = "😕",
            [":S"] = "😕",
            [":-S"] = "😕",
            [":x"] = "🤐",
            [":-x"] = "🤐",
            [":X"] = "🤐",
            [":-X"] = "🤐",
        };

        /// <summary>
        /// Replace text emoji representations with actual emojis
        /// </summary>
        public static string ReplaceTextEmojis(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var result = text;
            
            // Sort by length descending to replace longer patterns first
            foreach (var replacement in EmojiTextReplacements.OrderByDescending(x => x.Key.Length))
            {
                result = result.Replace(replacement.Key, replacement.Value);
            }
            
            return result;
        }

        /// <summary>
        /// Check if text contains any emoji text representations
        /// </summary>
        public static bool ContainsTextEmojis(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            return EmojiTextReplacements.Keys.Any(key => text.Contains(key));
        }
    }
}
