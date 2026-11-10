using CommunityToolkit.Mvvm.Input;

namespace MultiShell.ViewModels;

/// <summary>
/// Partial class managing per-pane in-terminal text search state and navigation commands.
/// </summary>
public partial class TerminalPaneViewModel
{
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

    [RelayCommand]
    public void ToggleSearch()
    {
        if (IsSearchOpen) CloseSearch();
        else OpenSearch();
    }

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
        if (IsSearchOpen)
        {
            ExecuteSearch(value);
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
        }
        else
        {
            TerminalModel.Search(query);
            SearchResultCount = TerminalModel.SearchResultCount;
            CurrentSearchResultIndex = TerminalModel.CurrentSearchResultIndex;
            UpdateSearchMatchSummary();
        }
    }

    private void UpdateSearchMatchSummary()
    {
        if (string.IsNullOrEmpty(SearchQuery) || SearchResultCount == 0)
        {
            SearchMatchSummary = string.IsNullOrEmpty(SearchQuery) ? string.Empty : "0/0";
        }
        else
        {
            SearchMatchSummary = $"{CurrentSearchResultIndex + 1}/{SearchResultCount}";
        }
    }
}
