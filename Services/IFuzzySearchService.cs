using System;
using System.Collections.Generic;

namespace MultiShell.Services;

/// <summary>
/// Service contract for fuzzy string matching and score-ranked filtering.
/// </summary>
public interface IFuzzySearchService
{
    /// <summary>
    /// Checks if the target string matches the search pattern fuzzy-wise and calculates a ranking score.
    /// </summary>
    /// <param name="pattern">The search pattern.</param>
    /// <param name="target">The target string to test.</param>
    /// <param name="score">Output score: higher score indicates a closer, higher-quality match.</param>
    /// <returns><c>true</c> if matched; otherwise, <c>false</c>.</returns>
    bool IsMatch(string pattern, string target, out int score);

    /// <summary>
    /// Filters and ranks a collection of items according to a fuzzy search pattern.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="items">The collection of candidate items.</param>
    /// <param name="pattern">The search pattern.</param>
    /// <param name="textSelector">Function to extract the string to match from an item.</param>
    /// <returns>Filtered and score-sorted items.</returns>
    IEnumerable<T> FilterAndRank<T>(IEnumerable<T> items, string pattern, Func<T, string> textSelector);

    /// <summary>
    /// Filters and ranks a collection of items according to a fuzzy search pattern,
    /// breaking score ties with a secondary ordering key.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <typeparam name="TSecondary">The type of the secondary ordering key.</typeparam>
    /// <param name="items">The collection of candidate items.</param>
    /// <param name="pattern">The search pattern.</param>
    /// <param name="textSelector">Function to extract the string to match from an item.</param>
    /// <param name="secondaryKeySelector">Function to extract the secondary ordering key.</param>
    /// <param name="secondaryDescending">Whether to sort the secondary key descending.</param>
    /// <returns>Filtered and score-sorted items.</returns>
    IEnumerable<T> FilterAndRank<T, TSecondary>(
        IEnumerable<T> items,
        string pattern,
        Func<T, string> textSelector,
        Func<T, TSecondary> secondaryKeySelector,
        bool secondaryDescending = true);
}
