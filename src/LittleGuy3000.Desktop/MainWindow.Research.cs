using System.Diagnostics;
using System.Text.RegularExpressions;
using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Services;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow
{
    private ResearchReport? _researchReport;
    private bool _researchSheet;
    private GoogleDriveExport Drive => new(_store.DirectoryPath);
    private CancellationTokenSource? _googleSignIn;
    private bool _savingResearch;

    private static void OpenExternal(Uri uri) => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
    private void InitializeResearch()
    {
        GoogleClientPathBox.Text = Settings.GoogleOAuthClientPath;
        GoogleDriveStatus.Text = Drive.Connected ? "Google Drive connected" : "Google Drive not connected";
    }
    private async Task<string> RunResearchAsync(string request, CancellationToken token)
    {
        _researchReport = null;
        _researchSheet = Regex.IsMatch(request, @"\b(?:sheet|spreadsheet|table)\b", RegexOptions.IgnoreCase);
        OpenExternal(new Uri("https://www.google.com/search?q=" + Uri.EscapeDataString(request)));
        if (!_connected) await ConnectAsync(false, false);
        token.ThrowIfCancellationRequested();
        if (!_connected || _provider is null || !ConfigureTurn()) throw new InvalidOperationException("Connect your ChatGPT account in Settings to research this request.");
        _provider.Compact = false;
        _provider.Effort = Settings.DetailEffort;
        var answer = await _provider.ResearchAsync(request, text => Dispatch(() => { if (!token.IsCancellationRequested) { AnswerText.Text = text; _bubble.SetMessage("Little Guy · researching", "Searching sources and preparing your report…", true); } }), token);
        token.ThrowIfCancellationRequested();
        if (_provider.ResearchSearchCount == 0) throw new InvalidOperationException("No live web search was completed. Little Guy won't label an unsearched answer as research.");
        _researchReport = answer.Research ?? throw new InvalidDataException("The research report was missing. Please try again.");
        _researchReport.Validate();
        string details = _researchReport.Title + "\n\n" + _researchReport.Summary + "\n\n" + string.Join("\n\n", _researchReport.Findings.Select(f => f.Topic + "\n" + f.Finding + "\n" + f.SourceTitle + ": " + f.SourceUrl));
        ResearchResultText.Text = details;
        if (!Drive.Connected) return "Research ready. Connect Google Drive in Settings → Research and ChatGPT, then choose Save report.\n\n" + details;
        var url = await Drive.UploadAsync(_researchReport, _researchSheet, Settings.GoogleOAuthClientPath, token);
        ResearchLink.NavigateUri = url; ResearchLink.Content = "Open saved " + (_researchSheet ? "Google Sheet" : "Google Doc");
        OpenExternal(url);
        return "Saved your research to " + (_researchSheet ? "Google Sheets" : "Google Docs") + ": " + url + "\n\n" + details;
    }
    private async void SelectGoogleClient_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker(); WinRT.Interop.InitializeWithWindow.Initialize(picker, Hwnd); picker.FileTypeFilter.Add(".json");
        var file = await picker.PickSingleFileAsync(); if (file is null) return;
        Settings.GoogleOAuthClientPath = file.Path; GoogleClientPathBox.Text = file.Path; _store.SaveSettings();
    }
    private async void ConnectGoogle_Click(object sender, RoutedEventArgs e)
    {
        if (_googleSignIn is not null) return;
        using var cancel = new CancellationTokenSource(); _googleSignIn = cancel;
        try
        {
            GoogleDriveStatus.Text = "Finish Google sign-in in your browser…";
            await Drive.ConnectAsync(Settings.GoogleOAuthClientPath, OpenExternal, cancel.Token);
            GoogleDriveStatus.Text = "Google Drive connected. Research can now be saved directly.";
        }
        catch (OperationCanceledException) { GoogleDriveStatus.Text = "Google sign-in cancelled."; }
        catch (Exception ex) { GoogleDriveStatus.Text = ex.Message; }
        finally { _googleSignIn = null; }
    }
    private void DisconnectGoogle_Click(object sender, RoutedEventArgs e)
    {
        _googleSignIn?.Cancel(); Drive.Disconnect(); GoogleDriveStatus.Text = "Disconnected on this PC.";
    }
    private void GoogleSetup_Click(object sender, RoutedEventArgs e) => OpenExternal(new Uri("https://console.cloud.google.com/apis/credentials"));
    private async void SaveResearch_Click(object sender, RoutedEventArgs e)
    {
        if (_researchReport is null) { GoogleDriveStatus.Text = "Ask Little Guy to research a topic first."; return; }
        if (_savingResearch) return;
        _savingResearch = true;
        try
        {
            var url = await Drive.UploadAsync(_researchReport, ResearchFormatCombo.SelectedIndex == 1, Settings.GoogleOAuthClientPath, _lifetime.Token);
            ResearchLink.NavigateUri = url; ResearchLink.Content = "Open saved research"; GoogleDriveStatus.Text = "Saved to Google Drive."; OpenExternal(url);
        }
        catch (OperationCanceledException) { GoogleDriveStatus.Text = "Save cancelled. Check Drive before retrying if upload had already started."; }
        catch (Exception ex) { GoogleDriveStatus.Text = ex.Message; }
        finally { _savingResearch = false; }
    }
    private void OpenChatGpt_Click(object sender, RoutedEventArgs e)
    {
        try { OpenExternal(ChatGptHandoff.NewChat()); }
        catch { ShowNotice("ChatGPT couldn't open. Install the Windows ChatGPT app and try again."); }
    }
}
