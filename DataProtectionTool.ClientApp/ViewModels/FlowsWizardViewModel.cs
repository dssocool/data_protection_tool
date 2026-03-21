using DataProtectionTool.ClientApp.Models;
using DataProtectionTool.ClientApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace DataProtectionTool.ClientApp.ViewModels;

public sealed class FlowsWizardViewModel : ObservableObject
{
    public ObservableCollection<FlowListItem> Items { get; } = [];

    public void LoadItems()
    {
        Items.Clear();
        foreach (var item in FlowConfigurationStore.Load())
        {
            Items.Add(item);
        }
    }

    public void SaveItems()
    {
        FlowConfigurationStore.Save(Items);
    }

    public string EnsureUniqueFlowName(string baseName, FlowListItem? currentItem)
    {
        var normalized = string.IsNullOrWhiteSpace(baseName) ? "Flow" : baseName.Trim();
        if (!Items.Any(item => !ReferenceEquals(item, currentItem) && item.FlowName.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return normalized;
        }

        var suffix = 2;
        while (true)
        {
            var candidate = $"{normalized} ({suffix})";
            if (!Items.Any(item => !ReferenceEquals(item, currentItem) && item.FlowName.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }

            suffix++;
        }
    }

    public string BuildUniqueName(string name, Func<FlowListItem, string> selector)
    {
        var seed = string.IsNullOrWhiteSpace(name) ? "Flow" : name.Trim();
        var candidate = $"{seed} Copy";
        var suffix = 2;
        while (Items.Any(item => selector(item).Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{seed} Copy {suffix}";
            suffix++;
        }

        return candidate;
    }
}
