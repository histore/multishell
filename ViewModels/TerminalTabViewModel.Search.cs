using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MultiShell.ViewModels;

/// <summary>
/// Partial class for in-terminal find/search overlay, result navigation, and match count summaries (REQ-TERM-006).
/// </summary>
public partial class TerminalTabViewModel
{
    [ObservableProperty]
    private bool _isSearchOpen;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private int _searchResultCount;

    [ObservableProperty]
    private int _currentSearchResultIndex;

    [ObservableProperty]
    private string _searchMatchSummary = string.Empty;

    /// <summary>
    /// Event fired when in-terminal search is opened to request input focus on the search box.
    /// </summary>
    public event Action? FocusSearchBoxRequested;

    /// <summary>
    /// Event fired when in-terminal search is closed to restore input focus back to the terminal.
    /// </summary>
    public event Action? FocusTerminalRequested;

    /// <summary>
    /// Opens the in-terminal search overlay and focuses the search input box (REQ-TERM-006).
    /// </summary>
    [RelayCommand]
    public void OpenSearch()
    {
        IsSearchOpen = true;
        if (TerminalModel.HasSelection && !string.IsNullOrWhiteSpace(TerminalModel.SelectedText))
        {
            SearchQuery = TerminalModel.SelectedText.Trim();
        }
        else if (!string.IsNullOrEmpty(SearchQuery))
        {
            ExecuteSearch(SearchQuery);
        }
        FocusSearchBoxRequested?.Invoke();
    }

    /// <summary>
    /// Closes the in-terminal search overlay, clears the search, and restores focus to terminal (REQ-TERM-006).
    /// </summary>
    [RelayCommand]
    public void CloseSearch()
    {
        IsSearchOpen = false;
        SearchQuery = string.Empty;
        TerminalModel.Search(string.Empty);
        TerminalModel.ClearSelection();
        SearchResultCount = 0;
        CurrentSearchResultIndex = 0;
        SearchMatchSummary = string.Empty;
        FocusTerminalRequested?.Invoke();
    }

    /// <summary>
    /// Toggles the in-terminal search overlay on or off (REQ-TERM-006).
    /// </summary>
    [RelayCommand]
    public void ToggleSearch()
    {
        if (IsSearchOpen)
        {
            CloseSearch();
        }
        else
        {
            OpenSearch();
        }
    }

    /// <summary>
    /// Selects and scrolls to the next matching search result (REQ-TERM-006).
    /// </summary>
    [RelayCommand]
    public void SearchNext()
    {
        if (SearchResultCount > 0)
        {
            TerminalModel.SelectNextSearchResult();
            CurrentSearchResultIndex = TerminalModel.CurrentSearchResultIndex;
            UpdateSearchMatchSummary();
        }
    }

    /// <summary>
    /// Selects and scrolls to the previous matching search result (REQ-TERM-006).
    /// </summary>
    [RelayCommand]
    public void SearchPrevious()
    {
        if (SearchResultCount > 0)
        {
            TerminalModel.SelectPreviousSearchResult();
            CurrentSearchResultIndex = TerminalModel.CurrentSearchResultIndex;
            UpdateSearchMatchSummary();
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        ExecuteSearch(value);
    }

    private void ExecuteSearch(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            TerminalModel.Search(string.Empty);
            SearchResultCount = 0;
            CurrentSearchResultIndex = 0;
            SearchMatchSummary = string.Empty;
            return;
        }

        TerminalModel.Search(query);
        SearchResultCount = TerminalModel.SearchResultCount;
        CurrentSearchResultIndex = TerminalModel.CurrentSearchResultIndex;
        UpdateSearchMatchSummary();
    }

    internal void UpdateSearchMatchSummary()
    {
        if (string.IsNullOrEmpty(SearchQuery))
        {
            SearchMatchSummary = string.Empty;
        }
        else if (SearchResultCount == 0)
        {
            SearchMatchSummary = Loc["Search_Terminal_NoResults"];
        }
        else
        {
            SearchMatchSummary = string.Format(Loc["Search_Terminal_Matches"], CurrentSearchResultIndex + 1, SearchResultCount);
        }
    }
}
