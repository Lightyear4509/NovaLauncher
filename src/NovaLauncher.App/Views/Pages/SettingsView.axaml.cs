using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace NovaLauncher.App.Views.Pages;

public sealed partial class SettingsView : NovaPage
{
    public SettingsView() => AvaloniaXamlLoader.Load(this);

    private async void OnApplyTheme(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.ApplySelectedThemeAsync(LifetimeToken));
    private async void OnApplyMotionPreference(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.ApplyMotionPreferenceAsync(LifetimeToken));
    private void OnApplySteamGridDbKey(object? sender, RoutedEventArgs e) => Workspace.ApplySteamGridDbApiKey();
    private async void OnSaveTailscalePeer(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.ConfigureTailscalePeerAsync(LifetimeToken));
    private async void OnRetrySaveSyncListener(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RetrySaveSyncListenerAsync(LifetimeToken));
    private async void OnGeneratePairingCode(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.GeneratePairingCodeAsync(LifetimeToken));
    private async void OnApplyPairingCode(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.ApplyPairingCodeAsync(LifetimeToken));
    private async void OnRevokePairedDevice(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RevokePairedDeviceAsync(LifetimeToken));
    private async void OnRenameTrustedPeer(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RenameSelectedTrustedPeerAsync(LifetimeToken));
    private async void OnToggleTrustedPeer(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.ToggleSelectedTrustedPeerAsync(LifetimeToken));
    private async void OnRevokeTrustedPeer(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RevokeSelectedTrustedPeerAsync(LifetimeToken));
    private async void OnCheckForUpdates(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.CheckForUpdatesAsync(LifetimeToken));
    private async void OnStageUpdate(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.StageAvailableUpdateAsync(LifetimeToken));
    private async void OnLaunchStagedUpdate(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.LaunchStagedUpdateAsync(LifetimeToken));
    private async void OnRollbackUpdate(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RollbackUpdateAsync(LifetimeToken));

    private void OnRefreshPairingStatus(object? sender, RoutedEventArgs e)
    {
        Workspace.RefreshSaveSyncPairingState();
        Workspace.ReportInvitationClipboardStatus("Pairing status refreshed from local trusted-device state.");
    }

    private async void OnCopyInvitationCode(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null || string.IsNullOrWhiteSpace(Workspace.PairingCode)) { Workspace.ReportInvitationClipboardStatus("Generate an invitation code before copying it."); return; }
        var code = Workspace.PairingCode;
        await clipboard.SetTextAsync(code);
        Workspace.ReportInvitationClipboardStatus("Invitation code copied. It will be cleared after 60 seconds if unchanged.");
        _ = ClearInvitationClipboardAsync(clipboard, code, LifetimeToken);
    }

    private async void OnPasteInvitationCode(object? sender, RoutedEventArgs e)
    {
        var value = await (TopLevel.GetTopLevel(this)?.Clipboard?.TryGetTextAsync() ?? Task.FromResult<string?>(null));
        if (string.IsNullOrWhiteSpace(value)) { Workspace.ReportInvitationClipboardStatus("The clipboard does not contain an invitation code."); return; }
        Workspace.PairingCode = value;
        Workspace.ReportInvitationClipboardStatus("Invitation code pasted. Choose Accept invitation to validate it.");
    }

    private async void OnExportDiagnostics(object? sender, RoutedEventArgs e)
    {
        var file = await Storage.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Export sanitized NovaLauncher diagnostics", SuggestedFileName = $"NovaLauncher-Diagnostics-{DateTime.UtcNow:yyyyMMdd}.zip", DefaultExtension = "zip", FileTypeChoices = [new FilePickerFileType("ZIP archive") { Patterns = ["*.zip"] }] });
        var path = file?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path)) await ExecuteAsync(() => Workspace.ExportDiagnosticsAsync(path, LifetimeToken));
    }

    private static async Task ClearInvitationClipboardAsync(IClipboard clipboard, string expected, CancellationToken token)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(60), token); if (string.Equals(await clipboard.TryGetTextAsync(), expected, StringComparison.Ordinal)) await clipboard.ClearAsync(); }
        catch (OperationCanceledException) { }
    }
}
