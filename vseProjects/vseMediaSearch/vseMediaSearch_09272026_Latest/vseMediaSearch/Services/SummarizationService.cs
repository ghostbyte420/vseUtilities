namespace vseMediaSearch.Services;

/// <summary>
/// Produces a short extractive summary of a block of text using a local, dependency-free
/// heuristic (word-frequency scored sentence extraction) — no external API/model call and
/// no secrets required (Rule 17: no new dependency/secret without asking first).
/// </summary>
internal static class SummarizationService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "if", "then", "so", "of", "to", "in", "on", "for",
        "with", "as", "at", "by", "is", "are", "was", "were", "be", "been", "being", "it", "its",
        "this", "that", "these", "those", "from", "into", "over", "under", "than", "such", "not",
        "no", "do", "does", "did", "has", "have", "had", "will", "would", "can", "could", "should",
        "you", "your", "we", "our", "they", "their", "he", "she", "his", "her", "i", "my", "me",
    };

    /// <summary>
    /// Extracts the top <paramref name="maxSentences"/> most representative sentences from
    /// <paramref name="text"/>, in their original order, joined back into a short summary.
    /// Returns the original text unchanged if it is already short enough to not need summarizing.
    /// </summary>
    public static string Summarize(string text, int maxSentences = 5)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var sentences = SplitIntoSentences(text);
        if (sentences.Count <= maxSentences)
        {
            return text.Trim();
        }

        var wordScores = ScoreWordsByFrequency(sentences);

        var scoredSentences = sentences
            .Select((sentence, index) => (sentence, index, score: ScoreSentence(sentence, wordScores)))
            .OrderByDescending(s => s.score)
            .Take(maxSentences)
            .OrderBy(s => s.index)
            .Select(s => s.sentence.Trim());

        return string.Join(" ", scoredSentences);
    }

    private static List<string> SplitIntoSentences(string text)
    {
        // Simple sentence splitter: break on '.', '!', or '?' followed by whitespace/end.
        var sentences = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '.' or '!' or '?' && (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1])))
            {
                var candidate = text[start..(i + 1)].Trim();
                if (candidate.Length > 0)
                {
                    sentences.Add(candidate);
                }

                start = i + 1;
            }
        }

        var tail = text[start..].Trim();
        if (tail.Length > 0)
        {
            sentences.Add(tail);
        }

        return sentences;
    }

    private static Dictionary<string, int> ScoreWordsByFrequency(List<string> sentences)
    {
        var frequencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var sentence in sentences)
        {
            foreach (var word in TokenizeWords(sentence))
            {
                if (StopWords.Contains(word) || word.Length < 3)
                {
                    continue;
                }

                frequencies[word] = frequencies.GetValueOrDefault(word) + 1;
            }
        }

        return frequencies;
    }

    private static double ScoreSentence(string sentence, Dictionary<string, int> wordScores)
    {
        var words = TokenizeWords(sentence).ToList();
        if (words.Count == 0)
        {
            return 0;
        }

        var total = words.Sum(w => wordScores.GetValueOrDefault(w));
        return (double)total / words.Count;
    }

    private static IEnumerable<string> TokenizeWords(string sentence)
    {
        return sentence
            .Split([' ', '\t', '\n', '\r', ',', '.', ';', ':', '"', '\'', '(', ')', '[', ']', '{', '}'],
                StringSplitOptions.RemoveEmptyEntries);
    }
}
