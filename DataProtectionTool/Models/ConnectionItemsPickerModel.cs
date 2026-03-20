using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DataProtectionTool.Models;

public sealed class ConnectionItemsPickerModel : INotifyPropertyChanged
{
    private string _searchText = string.Empty;
    private bool _isSuggestionsVisible;

    public ConnectionItemsPickerModel(string connectionName)
    {
        ConnectionName = connectionName;
    }

    public string ConnectionName { get; }
    public ObservableCollection<FetchedItemOption> AvailableItems { get; } = [];
    public ObservableCollection<FetchedItemOption> FilteredSuggestions { get; } = [];
    public ObservableCollection<FetchedItemOption> SelectedItems { get; } = [];
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            OnPropertyChanged();
        }
    }

    public bool IsSuggestionsVisible
    {
        get => _isSuggestionsVisible;
        set
        {
            if (_isSuggestionsVisible == value)
            {
                return;
            }

            _isSuggestionsVisible = value;
            OnPropertyChanged();
        }
    }

    public void RefreshFilteredSuggestions()
    {
        FilteredSuggestions.Clear();
        var query = SearchText.Trim();
        var candidates = AvailableItems
            .Where(item => !SelectedItems.Any(selected => selected.Key.Equals(item.Key, StringComparison.OrdinalIgnoreCase)))
            .Where(item => string.IsNullOrWhiteSpace(query)
                || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.KindLabel.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            FilteredSuggestions.Add(candidate);
        }
    }

    public void AddSelected(FetchedItemOption option)
    {
        if (SelectedItems.Any(item => item.Key.Equals(option.Key, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SelectedItems.Add(new FetchedItemOption
        {
            Name = option.Name,
            Kind = option.Kind,
            ConnectionName = option.ConnectionName
        });
    }

    public void RemoveSelected(FetchedItemOption option)
    {
        var existing = SelectedItems.FirstOrDefault(item => item.Key.Equals(option.Key, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SelectedItems.Remove(existing);
        }
    }

    public void SyncSelectedWithAvailable()
    {
        var validKeys = AvailableItems.Select(static item => item.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orphaned = SelectedItems.Where(item => !validKeys.Contains(item.Key)).ToList();
        foreach (var item in orphaned)
        {
            SelectedItems.Remove(item);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
