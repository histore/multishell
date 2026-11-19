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

    private bool _isSyncingPaneSearch;

    /// <summary>
    /// Opens the in-terminal search overlay on the active pane and focuses the search input box (REQ-TERM-006).
    /// </summary>
    [RelayCommand]
    public void OpenSearch()
    {
        if (ActivePane != null)
        {
            ActivePane.OpenSearch();
            SyncSearchStateFromPane(ActivePane);
        }
        else
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
        }
        FocusSearchBoxRequested?.Invoke();
    }

    /// <summary>
    /// Closes the in-terminal search overlay on the active pane, clears the search, and restores focus to terminal (REQ-TERM-006).
    /// </summary>
    [RelayCommand]
    public void CloseSearch()
    {
        if (ActivePane != null)
        {
            ActivePane.CloseSearch();
            SyncSearchStateFromPane(ActivePane);
        }
        else
        {
            IsSearchOpen = false;
            SearchQuery = string.Empty;
            TerminalModel.Search(string.Empty);
            TerminalModel.ClearSelection();
            SearchResultCount = 0;
            CurrentSearchResultIndex = 0;
            SearchMatchSummary = string.Empty;
        }
        FocusTerminalRequested?.Invoke();
    }

    /// <summary>
    /// Toggles the in-terminal search overlay on or off (REQ-TERM-006).
    /// </summary>
    [RelayCommand]
    public void ToggleSearch()
    {
        bool currentlyOpen = ActivePane?.IsSearchOpen ?? IsSearchOpen;
        if (currentlyOpen)
        {
            CloseSearch();
        }
        else
        {
            OpenSearch();
        }
    }

    /// <summary>
    /// Selects and scrolls to the next matching search result on the active pane (REQ-TERM-006).
    /// </summary>
    [RelayCommand]
    public void SearchNext()
    {
        if (ActivePane != null)
        {
            ActivePane.SearchNext();
            SyncSearchStateFromPane(ActivePane);
            return;
        }

        if (SearchResultCount > 0)
        {
            TerminalModel.SelectNextSearchResult();
            CurrentSearchResultIndex = TerminalModel.CurrentSearchResultIndex;
            UpdateSearchMatchSummary();
        }
    }

    /// <summary>
    /// Selects and scrolls to the previous matching search result on the active pane (REQ-TERM-006).
    /// </summary>
    [RelayCommand]
    public void SearchPrevious()
    {
        if (ActivePane != null)
        {
            ActivePane.SearchPrevious();
            SyncSearchStateFromPane(ActivePane);
            return;
        }

        if (SearchResultCount > 0)
        {
            TerminalModel.SelectPreviousSearchResult();
            CurrentSearchResultIndex = TerminalModel.CurrentSearchResultIndex;
            UpdateSearchMatchSummary();
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (_isSyncingPaneSearch) return;

        if (ActivePane != null)
        {
            ActivePane.SearchQuery = value;
            SyncSearchStateFromPane(ActivePane);
            return;
        }

        ExecuteSearch(value);
    }

    private void SyncSearchStateFromPane(TerminalPaneViewModel pane)
    {
        _isSyncingPaneSearch = true;
        try
        {
            IsSearchOpen = pane.IsSearchOpen;
            SearchQuery = pane.SearchQuery;
            SearchResultCount = pane.SearchResultCount;
            CurrentSearchResultIndex = pane.CurrentSearchResultIndex;
            SearchMatchSummary = pane.SearchMatchSummary;
        }
        finally
        {
            _isSyncingPaneSearch = false;
        }
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
