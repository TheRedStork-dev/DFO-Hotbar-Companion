using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace DfoHotbarCompanion;

public partial class MainWindow : Window
{
    readonly HotbarInstaller installer;
    readonly CompanionSettings settings;
    readonly Dictionary<string, DungeonLocation> liveDungeons = new();
    readonly DfoDevToolsBridge dfoBridge = new();
    readonly System.Windows.Threading.DispatcherTimer bridgeTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    LiveBridgeServer? liveServer;
    GameData? gameData;
    GameData? classicData;
    double? playerX, playerZ;
    bool loaded;
    bool busy;

    readonly string[] weaponMaterials = { "Any material","Iron","Steel","Silver","Elven","Dwarven","Mithril","Adamantium","Ebony","Orcish","Daedric" };
    readonly string[] armourMaterials = { "Any material","Leather","Chain","Iron","Steel","Silver","Elven","Dwarven","Mithril","Adamantium","Ebony","Orcish","Daedric" };
    readonly string[] enchantedBases = { "Any enchanted base item","King's Mark / Mark jewellery","Amulet jewellery","Bracer jewellery","Ring jewellery","Bracelet jewellery","Torc jewellery","Cloth amulet jewellery","Wand jewellery" };

    public MainWindow()
    {
        InitializeComponent();
        installer = new HotbarInstaller(AppendLog);
        settings = CompanionSettings.Load();
        ScannerEnabledBox.IsChecked = settings.ScannerEnabled;
        HotbarEnabledBox.IsChecked = settings.HotbarEnabled;
        PathBox.Text = settings.Arena2Path;
        CategoryBox.ItemsSource = GameDefinitions.Categories;
        CategoryBox.SelectedIndex = 0;
        bridgeTimer.Tick += async (_, _) => await TryAttachBridge();
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => StopScanner();
        AppendLog("Ready. Only the modules marked Enabled will run.");
    }

    async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        loaded = true;
        ApplyModuleVisuals();
        if (ScannerEnabledBox.IsChecked == true) await StartScanner();
    }

    async void ModuleToggle_Click(object sender, RoutedEventArgs e)
    {
        if (!loaded || busy) return;
        settings.ScannerEnabled = ScannerEnabledBox.IsChecked == true;
        settings.HotbarEnabled = HotbarEnabledBox.IsChecked == true;
        settings.Save();
        ApplyModuleVisuals();
        if (settings.ScannerEnabled) await StartScanner(); else StopScanner();
    }

    void ApplyModuleVisuals()
    {
        bool scanner = ScannerEnabledBox.IsChecked == true;
        bool hotbar = HotbarEnabledBox.IsChecked == true;
        ScannerModulePanel.IsEnabled = scanner;
        HotbarModulePanel.IsEnabled = hotbar;
        ScannerModuleStatus.Text = scanner ? (liveServer == null ? "Enabled — starting local bridge" : "Enabled — listening for DFO") : "Off — no listener or game connection";
        HotbarModuleStatus.Text = hotbar ? "Enabled — update before launch" : "Off — patch update will be skipped";
        LaunchButton.Content = hotbar ? "UPDATE & LAUNCH DFO" : "LAUNCH DFO";
        StatusTitle.Text = hotbar ? "Hotbar module ready" : "Hotbar module disabled";
        StatusDetail.Text = hotbar
            ? "The current DFO release will be checked and the four-page hotbar applied safely."
            : "Launching will skip all hotbar downloading, building, and patching.";
        UpdateDataStatus();
    }

    async Task StartScanner()
    {
        if (liveServer != null) return;
        if (classicData == null) LoadGameData();
        try
        {
            liveServer = new LiveBridgeServer();
            liveServer.Message += m => Dispatcher.InvokeAsync(() => ApplyLiveMessage(m));
            liveServer.Error += ex => Dispatcher.InvokeAsync(() => BridgeStatus.Text = $"Scanner bridge error: {ex.Message}");
            liveServer.Start();
            bridgeTimer.Start();
            BridgeStatus.Text = "Bridge ready. Launch or connect DFO to begin live scanning.";
            ScannerModuleStatus.Text = "Enabled — listening for DFO";
            await TryAttachBridge();
        }
        catch (Exception ex)
        {
            liveServer?.Dispose();
            liveServer = null;
            bridgeTimer.Stop();
            BridgeStatus.Text = $"Scanner could not start: {ex.Message}";
            ScannerModuleStatus.Text = "Enabled, but the local bridge could not start";
        }
    }

    void StopScanner()
    {
        bridgeTimer.Stop();
        liveServer?.Dispose();
        liveServer = null;
        liveDungeons.Clear();
        RebuildGameData();
        if (loaded)
        {
            BridgeStatus.Text = "Scanner disabled.";
            ScannerModuleStatus.Text = "Off — no listener or game connection";
            Find();
        }
    }

    async void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        bool hotbar = HotbarEnabledBox.IsChecked == true;
        bool scanner = ScannerEnabledBox.IsChecked == true;
        SetBusy(true, hotbar ? "Preparing the current DFO release…" : "Launching DFO…");
        try
        {
            if (scanner) await StartScanner();
            if (hotbar)
            {
                var result = await installer.UpdateAsync();
                StatusTitle.Text = "Hotbar companion is ready";
                StatusDetail.Text = $"Four pages installed for DFO {result.Version}. Launching now.";
            }
            installer.LaunchDfo(scanner);
            AppendLog($"Launched DFO with {(scanner ? "the dungeon scanner" : "scanner disabled")} and {(hotbar ? "the hotbar module enabled" : "hotbar updates skipped")}.");
            if (scanner)
            {
                ModuleTabs.SelectedIndex = 0;
                BridgeStatus.Text = "DFO is starting. The scanner will connect automatically.";
            }
        }
        catch (Exception ex)
        {
            StatusTitle.Text = "The launch was stopped safely";
            StatusDetail.Text = ex.Message;
            AppendLog($"STOPPED: {ex.Message}");
            System.Windows.MessageBox.Show(this, ex.Message, "DFO Companion", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetBusy(false); }
    }

    void AppendLog(string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            LogText.Text += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
            if (LogText.Text.Length > 60_000) LogText.Text = LogText.Text[^50_000..];
            LogScroll.ScrollToEnd();
        });
    }

    void SetBusy(bool value, string? detail = null)
    {
        busy = value;
        LaunchButton.IsEnabled = !value;
        ScannerEnabledBox.IsEnabled = !value;
        HotbarEnabledBox.IsEnabled = !value;
        if (value) StatusTitle.Text = "Working…";
        if (detail != null) StatusDetail.Text = detail;
    }

    void LoadGameData()
    {
        try
        {
            classicData = GameDataReader.Load(PathBox.Text.Trim());
            settings.Arena2Path = PathBox.Text.Trim();
            settings.Save();
            RebuildGameData();
            RegionBox.ItemsSource = new[] { "All regions" }.Concat(classicData.Dungeons.Select(x => x.Region).Distinct().OrderBy(x => x)).ToArray();
            RegionBox.SelectedIndex = 0;
            RefreshItems();
            Find();
        }
        catch (Exception ex)
        {
            gameData = classicData = null;
            DataStatus.Text = "Classic game data not loaded. " + ex.Message;
            ResultsGrid.ItemsSource = null;
            BridgeStatus.Text = ex.Message;
        }
    }

    void RebuildGameData()
    {
        if (classicData != null) gameData = new(classicData.Dungeons.Concat(liveDungeons.Values).ToList(), classicData.MagicTemplates);
    }

    void UpdateDataStatus()
    {
        if (ScannerEnabledBox.IsChecked != true)
        {
            DataStatus.Text = "Dungeon Scanner off • Hotbar " + (HotbarEnabledBox.IsChecked == true ? "on" : "off");
            return;
        }
        int elite = liveDungeons.Values.Count(x => x.Tier == "Elite");
        string position = playerX.HasValue ? $" • player map {Math.Floor(playerX.Value / 32768):N0}, {499 - Math.Floor(playerZ!.Value / 32768):N0}" : "";
        string classic = classicData == null ? "" : $" • {classicData.Dungeons.Count:N0} classic reference";
        DataStatus.Text = $"{liveDungeons.Count:N0} live dungeons ({elite:N0} Elite){position}{classic} • Hotbar {(HotbarEnabledBox.IsChecked == true ? "on" : "off")}";
    }

    void ApplyLiveMessage(LiveDungeonMessage m)
    {
        string action = m.Action ?? "";
        if (action.Equals("player", StringComparison.OrdinalIgnoreCase))
        {
            playerX = m.PlayerX; playerZ = m.PlayerZ; UpdateDataStatus(); Find(); return;
        }
        if (action.Equals("reset", StringComparison.OrdinalIgnoreCase)) liveDungeons.Clear();
        else if (!string.IsNullOrWhiteSpace(m.Key) && action.Equals("remove", StringComparison.OrdinalIgnoreCase)) liveDungeons.Remove(m.Key);
        else if (!string.IsNullOrWhiteSpace(m.Key) && m.DungeonType >= 0 && m.DungeonType < GameDefinitions.DungeonTypeNames.Length)
        {
            string region = m.RegionName ?? "Online wilderness";
            liveDungeons[m.Key] = new(m.Name ?? $"Live dungeon ({m.X},{m.Y})", region, m.RegionIndex, m.MapId,
                (byte)m.DungeonType, GameDefinitions.DungeonTypeNames[m.DungeonType], GameDefinitions.DungeonTables[m.DungeonType],
                m.X * 128, (499 - m.Y) * 128, true, m.Blocks, 1, 1, m.Elite ? "Elite" : "Regular", "Live now");
        }
        RebuildGameData();
        UpdateDataStatus();
        Find();
    }

    async Task TryAttachBridge()
    {
        if (ScannerEnabledBox.IsChecked != true || liveServer == null) return;
        if (await dfoBridge.TryInjectAsync())
        {
            BridgeStatus.Text = "Connected — live DFO dungeons are being received.";
            ScannerModuleStatus.Text = "Enabled — connected to DFO";
        }
    }

    async void ConnectDfo_Click(object sender, RoutedEventArgs e)
    {
        if (ScannerEnabledBox.IsChecked != true) return;
        await StartScanner();
        if (await dfoBridge.TryInjectAsync()) { BridgeStatus.Text = "Connected — live DFO dungeons are being received."; return; }
        if (dfoBridge.IsDfoRunning())
        {
            System.Windows.MessageBox.Show(this, "Close every DFO Online window, then click Connect again. DFO must start with its local scanner connection enabled.", "Restart DFO Online", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!dfoBridge.Launch())
        {
            System.Windows.MessageBox.Show(this, $"DFO Online was not found at:\n{DfoDevToolsBridge.InstalledExe}", "DFO Online not found", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        BridgeStatus.Text = "Starting DFO with the dungeon scanner…";
        for (int i = 0; i < 15; i++)
        {
            await Task.Delay(1000);
            if (await dfoBridge.TryInjectAsync()) { BridgeStatus.Text = "Connected — live DFO dungeons are being received."; return; }
        }
        BridgeStatus.Text = "DFO started, but the scanner has not attached yet. Leave DFO open and try Connect again.";
    }

    void RefreshItems()
    {
        if (CategoryBox.SelectedItem is not LootCategory c) return;
        bool enchanted = c.Key == "MI";
        ItemLabel.Content = enchanted ? "Enchanted base item" : "Specific item";
        EnchantmentLabel.Visibility = EnchantmentBox.Visibility = enchanted ? Visibility.Visible : Visibility.Collapsed;
        if (enchanted && gameData != null)
        {
            ItemBox.ItemsSource = enchantedBases;
            EnchantmentBox.ItemsSource = new[] { "Any enchantment" }.Concat(gameData.MagicTemplates.Select(x => x.DisplayName)).ToArray();
            EnchantmentBox.SelectedIndex = 0;
        }
        else ItemBox.ItemsSource = new[] { "Any item in this category" }.Concat(c.Items.Where(x => !x.StartsWith("Any "))).ToArray();
        ItemBox.SelectedIndex = 0;
        MaterialBox.ItemsSource = c.Key == "WP" ? weaponMaterials : c.Key == "AM" ? armourMaterials : new[] { "Any material" };
        MaterialBox.SelectedIndex = 0;
        MaterialBox.IsEnabled = c.SupportsMaterial;
    }

    void Find()
    {
        if (gameData == null || CategoryBox.SelectedItem is not LootCategory c || ItemBox.SelectedItem is not string item) return;
        string enchantment = EnchantmentBox.SelectedItem as string ?? "Any enchantment";
        string material = MaterialBox.SelectedItem as string ?? "Any material";
        string? region = RegionBox.SelectedIndex <= 0 ? null : RegionBox.SelectedItem as string;
        bool liveOnly = LiveOnly.IsChecked == true;
        IReadOnlyList<LootResult> results = LootEngine.Rank(gameData, c, item, enchantment, material, (int)LevelSlider.Value,
            region, DiscoveredOnly.IsChecked == true, liveOnly, playerX, playerZ);
        string search = DungeonSearchBox?.Text.Trim() ?? "";
        if (search.Length > 0) results = results.Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        ResultsGrid.ItemsSource = results;
        BestLabel.Text = liveOnly ? "NEAREST CONFIRMED DUNGEON" : "BEST LOOT DESTINATION";
        if (results.Count > 0)
        {
            var b = results[0];
            BestName.Text = b.Name;
            BestDetails.Text = $"{b.Region}  •  {b.DungeonType}  •  {b.Tier}, {b.Size} ({b.Blocks} blocks)  •  map {b.Coordinates}";
            BestChance.Text = liveOnly ? $"{b.Direction}  {b.Distance}  •  {b.Chance} loot" : b.Chance;
        }
        else
        {
            BestName.Text = liveOnly ? "No live dungeons scanned yet" : "No matching dungeons";
            BestDetails.Text = liveOnly ? "Launch DFO with Scanner enabled, then enter the online world." : "Try another name, region, or include hidden locations.";
            BestChance.Text = "—";
        }
    }

    void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var d = new Forms.FolderBrowserDialog { Description = "Choose your Daggerfall ARENA2 folder", SelectedPath = PathBox.Text, UseDescriptionForTitle = true };
        if (d.ShowDialog() == Forms.DialogResult.OK) { PathBox.Text = d.SelectedPath; LoadGameData(); }
    }

    void CategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshItems();
    void LevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (LevelText != null) LevelText.Text = ((int)e.NewValue).ToString(); }
    void Find_Click(object sender, RoutedEventArgs e) => Find();
    void ScannerFilter_Click(object sender, RoutedEventArgs e) => Find();
    void DungeonSearchBox_TextChanged(object sender, TextChangedEventArgs e) { if (IsLoaded) Find(); }
    void ClearSearch_Click(object sender, RoutedEventArgs e) { DungeonSearchBox.Clear(); DungeonSearchBox.Focus(); }
}


