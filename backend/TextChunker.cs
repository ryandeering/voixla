using System.Text;
using System.Text.RegularExpressions;

namespace Voixla.Api;

public static partial class TextChunker
{
    [GeneratedRegex(@"(?<=[\.\!\?…])\s+")]
    private static partial Regex SentenceBoundary();

    public static IReadOnlyList<string> Chunk(string text, int maxChars, int firstChunkMaxChars = 0)
    {
        maxChars = Math.Max(1, maxChars);
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return chunks;
        }

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var paragraphs = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var paragraph in paragraphs)
        {
            var collapsed = Regex.Replace(paragraph.Trim(), @"\s+", " ");
            if (collapsed.Length == 0)
            {
                continue;
            }

            if (collapsed.Length <= maxChars)
            {
                chunks.Add(collapsed);
                continue;
            }

            var sentences = SentenceBoundary().Split(collapsed);
            var current = new StringBuilder();
            foreach (var sentence in sentences)
            {
                if (sentence.Length == 0)
                {
                    continue;
                }

                if (current.Length > 0 && current.Length + 1 + sentence.Length > maxChars)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                }

                if (sentence.Length > maxChars)
                {
                    if (current.Length > 0)
                    {
                        chunks.Add(current.ToString());
                        current.Clear();
                    }
                    chunks.AddRange(HardWrap(sentence, maxChars));
                    continue;
                }

                if (current.Length > 0)
                {
                    current.Append(' ');
                }

                current.Append(sentence);
            }

            if (current.Length > 0)
            {
                chunks.Add(current.ToString());
            }
        }

        if (firstChunkMaxChars > 0 && firstChunkMaxChars < maxChars
            && chunks.Count > 0 && chunks[0].Length > firstChunkMaxChars)
        {
            var (first, rest) = SplitLead(chunks[0], firstChunkMaxChars);
            chunks[0] = first;
            if (rest.Length > 0)
            {
                chunks.Insert(1, rest);
            }
        }

        return chunks;
    }

    private static (string First, string Remainder) SplitLead(string head, int cap)
    {
        var first = Chunk(head, cap)[0];
        var rest = head.Length > first.Length ? head[first.Length..].TrimStart() : string.Empty;
        return (first, rest);
    }

    private static IEnumerable<string> HardWrap(string sentence, int maxChars)
    {
        var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();
        foreach (var word in words)
        {
            if (word.Length > maxChars)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                for (var i = 0; i < word.Length; i += maxChars)
                {
                    yield return word.Substring(i, Math.Min(maxChars, word.Length - i));
                }
                continue;
            }

            if (current.Length > 0 && current.Length + 1 + word.Length > maxChars)
            {
                yield return current.ToString();
                current.Clear();
            }
            if (current.Length > 0)
            {
                current.Append(' ');
            }

            current.Append(word);
        }
        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
