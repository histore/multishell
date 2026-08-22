using System;
using System.Collections.Generic;
using System.Linq;

namespace MultiShell.Services;

/// <summary>
/// High-performance fuzzy search service supporting subsequence matching with weighted scoring
/// (consecutive character bonuses, word boundary bonuses, prefix matching, and length penalties).
/// </summary>
public class FuzzySearchService : IFuzzySearchService
{
    public bool IsMatch(string pattern, string target, out int score)
    {
        score = 0;

        if (string.IsNullOrEmpty(pattern))
        {
            score = 100;
            return true;
        }

        if (string.IsNullOrEmpty(target))
        {
            return false;
        }

        // Exact match gets highest score
        if (string.Equals(pattern, target, StringComparison.OrdinalIgnoreCase))
        {
            score = 1000;
            return true;
        }

        // Prefix match gets very high score
        if (target.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
        {
            score = 800 - (target.Length - pattern.Length);
            return true;
        }

        // Substring match gets high score
        var substringIndex = target.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (substringIndex >= 0)
        {
            score = 600 - substringIndex - (target.Length - pattern.Length);
            return true;
        }

        // Subsequence matching with scoring
        var patternIdx = 0;
        var targetIdx = 0;
        var currentScore = 0;
        var consecutiveCount = 0;
        var prevMatchedIdx = -2;

        while (patternIdx < pattern.Length && targetIdx < target.Length)
        {
            var pChar = char.ToLowerInvariant(pattern[patternIdx]);
            var tChar = char.ToLowerInvariant(target[targetIdx]);

            if (pChar == tChar)
            {
                var matchScore = 10;

                // Consecutive match bonus
                if (targetIdx == prevMatchedIdx + 1)
                {
                    consecutiveCount++;
                    matchScore += consecutiveCount * 15;
                }
                else
                {
                    consecutiveCount = 0;
                }

                // Word boundary bonus (start of string, after space, slash, backslash, hyphen, underscore, dot)
                if (targetIdx == 0 ||
                    target[targetIdx - 1] == ' ' ||
                    target[targetIdx - 1] == '/' ||
                    target[targetIdx - 1] == '\\' ||
                    target[targetIdx - 1] == '-' ||
                    target[targetIdx - 1] == '_' ||
                    target[targetIdx - 1] == '.')
                {
                    matchScore += 25;
                }

                // Uppercase in camelCase bonus
                if (char.IsUpper(target[targetIdx]))
                {
                    matchScore += 15;
                }

                currentScore += matchScore;
                prevMatchedIdx = targetIdx;
                patternIdx++;
            }

            targetIdx++;
        }

        if (patternIdx == pattern.Length)
        {
            // Matched all characters of pattern
            // Length penalty so shorter exact matches rank higher than bloated strings
            var lengthPenalty = Math.Min(50, (target.Length - pattern.Length) * 2);
            score = Math.Max(1, currentScore - lengthPenalty);
            return true;
        }

        score = 0;
        return false;
    }

    public IEnumerable<T> FilterAndRank<T>(IEnumerable<T> items, string pattern, Func<T, string> textSelector)
    {
        if (items == null) return Enumerable.Empty<T>();

        var trimmedPattern = pattern?.Trim();
        if (string.IsNullOrEmpty(trimmedPattern))
        {
            return items;
        }

        return items
            .Select(item =>
            {
                var text = textSelector(item) ?? string.Empty;
                var isMatch = IsMatch(trimmedPattern, text, out var score);
                return new { Item = item, IsMatch = isMatch, Score = score };
            })
            .Where(x => x.IsMatch)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Item);
    }
}
