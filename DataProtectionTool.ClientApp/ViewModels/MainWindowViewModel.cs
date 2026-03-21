using DataProtectionTool.ClientApp.Models;
using DataProtectionTool.ClientApp.Services;
using System.Collections.ObjectModel;

namespace DataProtectionTool.ClientApp.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    public ObservableCollection<FlowListItem> Flows { get; } = [];

    public void LoadSavedFlows()
    {
        Flows.Clear();
        foreach (var flow in FlowConfigurationStore.Load())
        {
            Flows.Add(flow);
        }
    }

    public void SaveFlows()
    {
        FlowConfigurationStore.Save(Flows);
    }
}
