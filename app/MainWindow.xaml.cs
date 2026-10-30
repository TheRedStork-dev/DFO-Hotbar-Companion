using System.Diagnostics;
using System.Windows;

namespace DfoHotbarCompanion;

public partial class MainWindow : Window
{
    private readonly HotbarInstaller installer;

    public MainWindow()
    {
        InitializeComponent();
        installer = new HotbarInstaller(AppendLog);
        AppendLog("Ready. DFO itself is never modified until a complete patched package has been built.");
    }

    private void AppendLog(string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            LogText.Text += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
            // Keep the status panel fast even after several updates.
            if (LogText.Text.Length > 60_000)
                LogText.Text = LogText.Text[^50_000..];
            LogScroll.ScrollToEnd();
        });
    }

    private async void UpdateAndLaunch_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "Preparing the current DFO release…");
        try
        {
            var result = await installer.UpdateAsync();
            StatusTitle.Text = "Hotbar companion is ready";
            StatusDetail.Text = $"Four pages installed for DFO {result.Version}. Launching now.";
            installer.LaunchDfo();
        }
        catch (Exception ex)
        {
            StatusTitle.Text = "The update was stopped safely";
            StatusDetail.Text = ex.Message;
            AppendLog($"STOPPED: {ex.Message}");
            MessageBox.Show(this, ex.Message, "DFO Hotbar Companion", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetBusy(false); }
    }

    private void LaunchOnly_Click(object sender, RoutedEventArgs e)
    {
        try { installer.LaunchDfo(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "DFO Hotbar Companion", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void SetBusy(bool busy, string? detail = null)
    {
        UpdateButton.IsEnabled = !busy;
        LaunchButton.IsEnabled = !busy;
        if (busy) StatusTitle.Text = "Working…";
        if (detail is not null) StatusDetail.Text = detail;
    }
}

