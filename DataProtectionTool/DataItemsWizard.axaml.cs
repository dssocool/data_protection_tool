using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataProtectionTool;

public partial class DataItemsWizard : UserControl
{
    private readonly List<ConnectionSelectionRow> _connectionRows = [];
    private readonly List<ConnectionDataItemsGroup> _groups = [];
    private bool _isStepTwo;
    private bool _isInitializing;

    public DataItemsWizard()
    {
        _isInitializing = true;
        InitializeComponent();
        _isInitializing = false;
        LoadConnections();
        UpdateTopActionState();
        SetStatus(string.Empty, isError: false);
    }

    private async void OnTopActionButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (_isStepTwo)
        {
            SaveSelections();
            return;
        }

        var selectedConnections = _connectionRows.Where(row => row.IsSelected).ToList();
        if (selectedConnections.Count == 0)
        {
            SetStatus("Select at least one connection before continuing.", isError: true);
            UpdateTopActionState();
            return;
        }

        TopActionButton.IsEnabled = false;
        SetStatus("Fetching data items...", isError: false);
        try
        {
            await FetchDataItemsForSelectedConnectionsAsync(selectedConnections);
            _isStepTwo = true;
            ConnectionSelectionPanel.IsVisible = false;
            DataItemsPanel.IsVisible = true;
            StepDescriptionText.Text = "Select one or more data items for each connection.";
            UpdateTopActionState();
            SetStatus("Data item fetch completed. Select items and click Save.", isError: false);
        }
        finally
        {
            TopActionButton.IsEnabled = true;
        }
    }

    private void OnSelectAllConnectionsChanged(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing || _isStepTwo)
        {
            return;
        }

        var selectAll = SelectAllConnectionsCheckBox.IsChecked == true;
        foreach (var row in _connectionRows)
        {
            row.IsSelected = selectAll;
            row.CheckBox.IsChecked = selectAll;
        }

        UpdateTopActionState();
    }

    private void LoadConnections()
    {
        _connectionRows.Clear();
        var entries = ConnectionConfigurationStore.Load();
        foreach (var profile in entries)
        {
            var row = CreateConnectionRow(profile);
            _connectionRows.Add(row);
        }

        ConnectionsItemsControl.ItemsSource = _connectionRows.Select(static row => row.RowControl).ToList();
        if (_connectionRows.Count == 0)
        {
            SetStatus("No saved connections found. Create and save connections in the Connections wizard first.", isError: true);
        }
    }

    private ConnectionSelectionRow CreateConnectionRow(ConnectionProfile profile)
    {
        var checkBox = new CheckBox
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        checkBox.IsCheckedChanged += OnConnectionRowSelectionChanged;

        var nameText = new TextBlock
        {
            Text = BuildConnectionDisplayName(profile),
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#21314A")),
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
        };

        var typeText = new TextBlock
        {
            Text = profile.ConnectionType,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#21314A")),
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
        };

        var savedText = new TextBlock
        {
            Text = profile.SavedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#21314A")),
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
        };

        var rowGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,2.2*,1.3*,1.1*"),
            Margin = new Thickness(10, 6, 10, 6)
        };
        rowGrid.Children.Add(checkBox);
        rowGrid.Children.Add(nameText);
        Grid.SetColumn(nameText, 1);
        rowGrid.Children.Add(typeText);
        Grid.SetColumn(typeText, 2);
        rowGrid.Children.Add(savedText);
        Grid.SetColumn(savedText, 3);

        var rowBorder = new Border
        {
            BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#ECF1F8")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = rowGrid
        };

        return new ConnectionSelectionRow
        {
            Profile = profile,
            CheckBox = checkBox,
            RowControl = rowBorder
        };
    }

    private async Task FetchDataItemsForSelectedConnectionsAsync(IReadOnlyList<ConnectionSelectionRow> selectedConnections)
    {
        _groups.Clear();
        var groupControls = new List<Control>(selectedConnections.Count);

        foreach (var selected in selectedConnections)
        {
            var group = new ConnectionDataItemsGroup
            {
                Profile = selected.Profile,
                ConnectionDisplayName = BuildConnectionDisplayName(selected.Profile)
            };

            var discovery = await DiscoverDataItemsAsync(selected.Profile);
            group.ErrorMessage = discovery.ErrorMessage;

            foreach (var itemName in discovery.ItemNames)
            {
                var itemCheckBox = new CheckBox
                {
                    Content = itemName,
                    Margin = new Thickness(0, 4, 0, 4)
                };
                itemCheckBox.IsCheckedChanged += OnDataItemSelectionChanged;
                group.Items.Add(new DataItemChoice
                {
                    Name = itemName,
                    CheckBox = itemCheckBox
                });
            }

            _groups.Add(group);
            groupControls.Add(BuildGroupControl(group));
        }

        ConnectionGroupsItemsControl.ItemsSource = groupControls;
    }

    private async Task<DataItemDiscoveryResult> DiscoverDataItemsAsync(ConnectionProfile profile)
    {
        try
        {
            return profile.ConnectionType switch
            {
                "Microsoft SQL Server" => await DiscoverSqlDataItemsAsync(profile),
                "Azure Fabric" => await DiscoverFabricDataItemsAsync(profile),
                "Azure Blob Storage" => await DiscoverBlobDataItemsAsync(profile),
                _ => new DataItemDiscoveryResult([], $"Unsupported connection type: {profile.ConnectionType}")
            };
        }
        catch (Exception ex)
        {
            return new DataItemDiscoveryResult([], ex.Message);
        }
    }

    private async Task<DataItemDiscoveryResult> DiscoverSqlDataItemsAsync(ConnectionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.SqlServerName) || string.IsNullOrWhiteSpace(profile.SqlDatabase))
        {
            return new DataItemDiscoveryResult([], "Missing SQL Server or database information.");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = profile.SqlServerName,
            InitialCatalog = profile.SqlDatabase,
            ConnectTimeout = 12,
            Encrypt = true,
            TrustServerCertificate = true
        };

        if (string.Equals(profile.SqlAuthenticationType, "Entra", StringComparison.OrdinalIgnoreCase))
        {
            builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(profile.SqlUserName) || string.IsNullOrWhiteSpace(profile.SqlPassword))
            {
                return new DataItemDiscoveryResult([], "Missing SQL Server user/password credentials.");
            }

            builder.Authentication = SqlAuthenticationMethod.SqlPassword;
            builder.UserID = profile.SqlUserName;
            builder.Password = profile.SqlPassword;
        }

        return await DiscoverRelationalColumnsAsync(builder.ConnectionString);
    }

    private async Task<DataItemDiscoveryResult> DiscoverFabricDataItemsAsync(ConnectionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.FabricConnectionString))
        {
            return new DataItemDiscoveryResult([], "Missing Fabric connection string.");
        }

        return await DiscoverRelationalColumnsAsync(profile.FabricConnectionString);
    }

    private async Task<DataItemDiscoveryResult> DiscoverRelationalColumnsAsync(string connectionString)
    {
        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = 20
        };

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            var column = reader.GetString(2);
            names.Add($"{schema}.{table}.{column}");
        }

        if (names.Count == 0)
        {
            return new DataItemDiscoveryResult([], "No data items found.");
        }

        return new DataItemDiscoveryResult(names, string.Empty);
    }

    private async Task<DataItemDiscoveryResult> DiscoverBlobDataItemsAsync(ConnectionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.BlobStorageAccount)
            || string.IsNullOrWhiteSpace(profile.BlobContainer)
            || string.IsNullOrWhiteSpace(profile.BlobAccessKey))
        {
            return new DataItemDiscoveryResult([], "Missing blob account, container, or access key.");
        }

        var credential = new StorageSharedKeyCredential(profile.BlobStorageAccount, profile.BlobAccessKey);
        var containerUri = new Uri($"https://{profile.BlobStorageAccount}.blob.core.windows.net/{profile.BlobContainer}");
        var containerClient = new BlobContainerClient(containerUri, credential);

        var names = new List<string>();
        await foreach (var blob in containerClient.GetBlobsAsync())
        {
            names.Add(blob.Name);
            if (names.Count >= 1000)
            {
                break;
            }
        }

        if (names.Count == 0)
        {
            return new DataItemDiscoveryResult([], "No blobs found in the container.");
        }

        return new DataItemDiscoveryResult(names, string.Empty);
    }

    private Control BuildGroupControl(ConnectionDataItemsGroup group)
    {
        var title = new TextBlock
        {
            Text = group.ConnectionDisplayName,
            FontSize = 16,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0B2D5C"))
        };

        var subtitle = new TextBlock
        {
            Text = group.Profile.ConnectionType,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4C6388"))
        };

        var stack = new StackPanel
        {
            Spacing = 4
        };
        stack.Children.Add(title);
        stack.Children.Add(subtitle);

        if (!string.IsNullOrWhiteSpace(group.ErrorMessage))
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"Fetch failed: {group.ErrorMessage}",
                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#B42318")),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });
        }
        else
        {
            foreach (var item in group.Items)
            {
                stack.Children.Add(item.CheckBox);
            }
        }

        return new Border
        {
            BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#DFE8F4")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(12),
            Child = stack
        };
    }

    private void OnConnectionRowSelectionChanged(object? sender, RoutedEventArgs e)
    {
        UpdateSelectAllConnectionsState();
        UpdateTopActionState();
    }

    private void OnDataItemSelectionChanged(object? sender, RoutedEventArgs e)
    {
        UpdateTopActionState();
    }

    private void SaveSelections()
    {
        var selectedRecords = new List<DataItemSelectionRecord>();
        foreach (var group in _groups)
        {
            var selectedNames = group.Items
                .Where(item => item.CheckBox.IsChecked == true)
                .Select(item => item.Name)
                .ToList();

            if (selectedNames.Count == 0)
            {
                continue;
            }

            selectedRecords.Add(new DataItemSelectionRecord
            {
                SavedAtUtc = DateTime.UtcNow,
                ConnectionType = group.Profile.ConnectionType,
                ConnectionDisplayName = group.ConnectionDisplayName,
                ItemNames = selectedNames
            });
        }

        if (selectedRecords.Count == 0)
        {
            SetStatus("Select at least one data item before saving.", isError: true);
            UpdateTopActionState();
            return;
        }

        DataItemSelectionStore.SaveBatch(selectedRecords);
        SetStatus($"Saved {selectedRecords.Sum(r => r.ItemNames.Count)} selected data items.", isError: false);
    }

    private void UpdateTopActionState()
    {
        if (!_isStepTwo)
        {
            TopActionButton.Content = "Next";
            TopActionButton.IsEnabled = _connectionRows.Any(row => row.IsSelected);
            return;
        }

        TopActionButton.Content = "Save";
        TopActionButton.IsEnabled = _groups.Any(group => group.Items.Any(item => item.CheckBox.IsChecked == true));
    }

    private void UpdateSelectAllConnectionsState()
    {
        if (_connectionRows.Count == 0)
        {
            SelectAllConnectionsCheckBox.IsChecked = false;
            return;
        }

        var selectedCount = _connectionRows.Count(row => row.IsSelected);
        SelectAllConnectionsCheckBox.IsChecked = selectedCount == _connectionRows.Count;
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#B42318"))
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4C6388"));
    }

    private static string BuildConnectionDisplayName(ConnectionProfile profile)
    {
        return profile.ConnectionType switch
        {
            "Microsoft SQL Server" => $"{profile.SqlServerName} / {profile.SqlDatabase}",
            "Azure Fabric" => ShortenConnectionString(profile.FabricConnectionString),
            "Azure Blob Storage" => $"{profile.BlobStorageAccount} / {profile.BlobContainer}",
            _ => profile.ConnectionType
        };
    }

    private static string ShortenConnectionString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(empty connection string)";
        }

        return value.Length <= 60 ? value : $"{value[..57]}...";
    }

    private sealed class ConnectionSelectionRow
    {
        public required ConnectionProfile Profile { get; init; }
        public required CheckBox CheckBox { get; init; }
        public required Border RowControl { get; init; }

        public bool IsSelected
        {
            get => CheckBox.IsChecked == true;
            set => CheckBox.IsChecked = value;
        }
    }

    private sealed class ConnectionDataItemsGroup
    {
        public required ConnectionProfile Profile { get; init; }
        public required string ConnectionDisplayName { get; init; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<DataItemChoice> Items { get; } = [];
    }

    private sealed class DataItemChoice
    {
        public required string Name { get; init; }
        public required CheckBox CheckBox { get; init; }
    }
}
