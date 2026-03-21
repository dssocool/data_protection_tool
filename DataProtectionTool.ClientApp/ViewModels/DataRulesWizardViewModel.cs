using DataProtectionTool.ClientApp.Models;
using DataProtectionTool.ClientApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace DataProtectionTool.ClientApp.ViewModels;

public sealed class DataRulesWizardViewModel : ObservableObject
{
    public ObservableCollection<DataRuleRecord> Items { get; } = [];

    public void LoadItems()
    {
        Items.Clear();
        foreach (var item in DataRuleConfigurationStore.Load())
        {
            Items.Add(item);
        }
    }

    public void SaveItems()
    {
        DataRuleConfigurationStore.Save(Items);
    }

    public string BuildUniqueName(string originalName)
    {
        var seed = string.IsNullOrWhiteSpace(originalName) ? "Rule" : originalName.Trim();
        var candidate = $"{seed} Copy";
        var suffix = 2;
        while (Items.Any(item => item.RuleName.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{seed} Copy {suffix}";
            suffix++;
        }

        return candidate;
    }

    public string EnsureUniqueName(string baseName, DataRuleRecord? currentItem)
    {
        var normalized = string.IsNullOrWhiteSpace(baseName) ? "Rule" : baseName.Trim();
        if (!Items.Any(item => !ReferenceEquals(item, currentItem) && item.RuleName.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return normalized;
        }

        var suffix = 2;
        while (true)
        {
            var candidate = $"{normalized} ({suffix})";
            if (!Items.Any(item => !ReferenceEquals(item, currentItem) && item.RuleName.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }

            suffix++;
        }
    }
}
