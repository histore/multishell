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
            score = 800;
            return true;
        }

        // Substring match
        var substringIndex = target.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (substringIndex >= 0)
        {
            // Word boundary bonus if the match starts at a word or path boundary
            var isWordBoundary = substringIndex == 0 ||
                                 target[substringIndex - 1] == ' ' ||
                                 target[substringIndex - 1] == '/' ||
                                 target[substringIndex - 1] == '\\' ||
                                 target[substringIndex - 1] == '-' ||
                                 target[substringIndex - 1] == '_' ||
                                 target[substringIndex - 1] == '.';
            score = isWordBoundary ? 650 : 600;
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
            score = Math.Clamp(currentScore, 1, 440);
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
            .Select((item, index) =>
            {
                var text = textSelector(item) ?? string.Empty;
                var isMatch = IsMatch(trimmedPattern, text, out var score);
                return new { Item = item, Index = index, IsMatch = isMatch, Score = score, Length = text.Length };
            })
            .Where(x => x.IsMatch)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Length)
            .ThenBy(x => x.Index)
            .Select(x => x.Item);
    }

    public IEnumerable<T> FilterAndRank<T, TSecondary>(
        IEnumerable<T> items,
        string pattern,
        Func<T, string> textSelector,
        Func<T, TSecondary> secondaryKeySelector,
        bool secondaryDescending = true)
    {
        if (items == null) return Enumerable.Empty<T>();

        var trimmedPattern = pattern?.Trim();
        if (string.IsNullOrEmpty(trimmedPattern))
        {
            return secondaryDescending
                ? items.OrderByDescending(secondaryKeySelector)
                : items.OrderBy(secondaryKeySelector);
        }

        var query = items
            .Select((item, index) =>
            {
                var text = textSelector(item) ?? string.Empty;
                var isMatch = IsMatch(trimmedPattern, text, out var score);
                return new { Item = item, Index = index, IsMatch = isMatch, Score = score, Length = text.Length };
            })
            .Where(x => x.IsMatch);

        return secondaryDescending
            ? query
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => secondaryKeySelector(x.Item))
                .ThenBy(x => x.Length)
                .ThenBy(x => x.Index)
                .Select(x => x.Item)
            : query
                .OrderByDescending(x => x.Score)
                .ThenBy(x => secondaryKeySelector(x.Item))
                .ThenBy(x => x.Length)
                .ThenBy(x => x.Index)
                .Select(x => x.Item);
    }
}
