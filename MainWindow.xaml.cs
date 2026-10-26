using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.RegularExpressions;
using Clippy.Models;
using Clippy.Services;
using Windows.ApplicationModel.DataTransfer;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace Clippy;

public partial class MainWindow : Window
{
    private readonly SettingsStore settingsStore = new();
    private readonly StartupRegistration startupRegistration = new();
    private readonly TextTransformService transforms = new();
    private readonly OllamaTranslationService translation = new(new HttpClient { Timeout = TimeSpan.FromMinutes(3) });
    private readonly ObservableCollection<TextMacro> macros = [];
    private AppSettings settings;
    private TextMacro? selectedMacro;
    private bool macroChangesPending;
    private bool updatingMacroName;
    private WindowState lastNonMinimizedState = WindowState.Normal;

    public string ConfiguredHotkey => settings.ActivationHotkey;
    public bool PreferCaretForWindowPlacement => settings.WindowPlacement != "Mouse";

    public MainWindow()
    {
        InitializeComponent();
        settings = settingsStore.LoadSettings();
        LocalizationManager.Apply(settings.UiLanguage);
        ThemeManager.Apply(settings.Theme);
        PopulateSettingsControls();
        foreach (var macro in settingsStore.LoadMacros()) macros.Add(macro);
        MacroList.ItemsSource = macros;
        Loaded += async (_, _) => await RefreshHistoryAsync();
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();
    }

    public void StartInTray()
    {
        ShowInTaskbar = false;
        Show();
        Hide();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            HideToTray();
            return;
        }

        lastNonMinimizedState = WindowState;
    }

    public void RestoreFromTray()
    {
        var restoreState = lastNonMinimizedState;
        var anchor = restoreState == WindowState.Maximized
            ? (System.Windows.Point?)null
            : WindowPlacementService.GetAnchor(PreferCaretForWindowPlacement);
        WindowState = restoreState;
        if (anchor is { } position) WindowPlacementService.PlaceNearAnchor(this, position);
        MainTabs.SelectedIndex = 0;
        ShowInTaskbar = true;
        if (!IsVisible) Show();
        Activate();
    }

    public void ReportClipboardNormalizerStatus(string message)
    {
        FooterStatus.Text = message;
    }

    public bool ConfirmPendingMacroChanges()
    {
        if (!macroChangesPending) return true;

        RestoreFromTray();

        var result = MessageBox.Show(
            this,
            LocalizationManager.T("MacroPendingQuestion"),
            LocalizationManager.T("UnsavedMacros"),
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel) return false;
        if (result == MessageBoxResult.No) return true;
        return TrySaveMacros(showSuccess: false);
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (Application.Current is App { IsExiting: true } app)
        {
            app.StopClipboardTextAutoNormalizer();
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private async void RefreshHistory_Click(object sender, RoutedEventArgs e) => await RefreshHistoryAsync();

    private async Task RefreshHistoryAsync()
    {
        HistoryStatus.Text = LocalizationManager.T("HistoryLoading");
        try
        {
            if (!WinClipboard.IsHistoryEnabled())
            {
                HistoryList.ItemsSource = null;
                HistoryStatus.Text = LocalizationManager.T("HistoryDisabled");
                return;
            }

            var result = await WinClipboard.GetHistoryItemsAsync();
            if (result.Status != ClipboardHistoryItemsResultStatus.Success)
            {
                HistoryStatus.Text = LocalizationManager.Format("HistoryFailed", result.Status);
                return;
            }

            var entries = new List<HistoryEntry>();
            foreach (var item in result.Items)
            {
                try
                {
                    var text = await ReadTextAsync(item.Content);
                    if (text is null) continue;
                    entries.Add(new HistoryEntry(item, text));
                }
                catch
                {
                    // One unavailable history item must not prevent the other text entries from loading.
                }
            }
            HistoryList.ItemsSource = entries;
            HistoryStatus.Text = LocalizationManager.Format("HistoryCount", entries.Count);
        }
        catch (Exception ex)
        {
            HistoryStatus.Text = LocalizationManager.Format("HistoryError", ex.Message);
        }
    }

    private static async Task<string?> ReadTextAsync(DataPackageView content)
    {
        if (content.Contains(StandardDataFormats.Rtf))
        {
            try { return PlainTextConverter.FromRtf(await content.GetRtfAsync()); }
            catch { /* Try another advertised text format. */ }
        }
        if (content.Contains(StandardDataFormats.Html))
        {
            try { return PlainTextConverter.FromHtml(await content.GetHtmlFormatAsync()); }
            catch { /* Try the plain text representation. */ }
        }
        if (content.Contains(StandardDataFormats.Text))
            return await content.GetTextAsync();
        return null;
    }

    private async void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is not HistoryEntry entry) return;
        TextEditor.Text = entry.Text;
        EditStatus.Text = LocalizationManager.Format("LoadedHistory", entry.Timestamp.LocalDateTime.ToString("G"));
        await Task.CompletedTask;
    }

    private void HistoryItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem item || item.DataContext is not HistoryEntry entry) return;

        HistoryList.SelectedItem = entry;
        var contextMenu = new ContextMenu();
        var savedMacros = settingsStore.LoadMacros();
        if (savedMacros.Count == 0)
        {
            contextMenu.Items.Add(new MenuItem { Header = LocalizationManager.T("NoMacros"), IsEnabled = false });
        }
        else
        {
            foreach (var macro in savedMacros)
            {
                var menuItem = new MenuItem { Header = macro.Name };
                menuItem.Click += async (_, _) => await ApplyMacroToHistoryAsync(entry, macro);
                contextMenu.Items.Add(menuItem);
            }
        }

        contextMenu.PlacementTarget = item;
        contextMenu.IsOpen = true;
        e.Handled = true;
    }

    private async Task ApplyMacroToHistoryAsync(HistoryEntry entry, TextMacro macro)
    {
        try
        {
            var transformedText = transforms.ApplyMacro(entry.Text, macro);
            var data = new DataPackage();
            data.SetText(transformedText);
            WinClipboard.SetContent(data);
            WinClipboard.Flush();
            HistoryStatus.Text = LocalizationManager.Format("MacroApplied", macro.Name);
            await RefreshHistoryAsync();
        }
        catch (Exception ex)
        {
            HistoryStatus.Text = LocalizationManager.Format("MacroApplyError", macro.Name, ex.Message);
        }
    }

    private void LoadSelectedHistory_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is HistoryEntry entry) TextEditor.Text = entry.Text;
        else EditStatus.Text = LocalizationManager.T("SelectHistory");
    }

    private void PlainText_Click(object sender, RoutedEventArgs e)
    {
        TextEditor.Text = PlainTextConverter.FromText(TextEditor.Text);
        EditStatus.Text = LocalizationManager.T("PlainConverted");
    }

    private void Newline_Click(object sender, RoutedEventArgs e)
    {
        var mode = (NewlineMode.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "CRLF";
        TextEditor.Text = TextTransformService.NormalizeNewlines(TextEditor.Text, mode);
        EditStatus.Text = LocalizationManager.Format("NewlinesConverted", mode);
    }

    private void CollapseBlankLines_Click(object sender, RoutedEventArgs e)
    {
        TextEditor.Text = TextTransformService.CollapseBlankLines(TextEditor.Text);
        EditStatus.Text = LocalizationManager.T("BlankLinesCollapsed");
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var operation = CreateReplaceOperation();
            TextEditor.Text = transforms.Apply(TextEditor.Text, operation);
            EditStatus.Text = LocalizationManager.T(RegexMode.IsChecked == true ? "RegexReplaced" : "TextReplaced");
        }
        catch (RegexMatchTimeoutException)
        {
            EditStatus.Text = LocalizationManager.T("RegexTimeout");
        }
        catch (ArgumentException ex)
        {
            EditStatus.Text = LocalizationManager.Format("RegexInvalid", ex.Message);
        }
    }

    private TextOperation CreateReplaceOperation() => new()
    {
        Kind = TextOperationKind.ReplaceText,
        Value = FindText.Text,
        Replacement = ReplacementText.Text,
        UseRegularExpression = RegexMode.IsChecked == true,
        IgnoreCase = IgnoreCase.IsChecked == true,
        Multiline = RegexMultiline.IsChecked == true
    };

    private async void CopyResult_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var before = TextEditor.Text;
            var data = new DataPackage();
            data.SetText(before);
            WinClipboard.SetContent(data);
            WinClipboard.Flush();
            EditStatus.Text = LocalizationManager.T("ClipboardCopied");
            await Task.Delay(250);
            await RefreshHistoryAsync();
        }
        catch (Exception ex)
        {
            EditStatus.Text = LocalizationManager.Format("ClipboardCopyError", ex.Message);
        }
    }

    private void NewMacro_Click(object sender, RoutedEventArgs e)
    {
        var macro = new TextMacro { Name = LocalizationManager.T("NewMacroName") };
        macros.Add(macro);
        MacroList.SelectedItem = macro;
        MarkMacroChangesPending();
        MacroName.Focus();
        MacroName.SelectAll();
        MacroStatus.Text = LocalizationManager.T("AddStepsPrompt");
    }

    private void MacroList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedMacro = MacroList.SelectedItem as TextMacro;
        updatingMacroName = true;
        MacroName.Text = selectedMacro?.Name ?? "";
        updatingMacroName = false;
        MacroSteps.ItemsSource = selectedMacro?.Operations;
    }

    private void MacroName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (updatingMacroName || selectedMacro is null) return;
        selectedMacro.Name = MacroName.Text;
        MacroList.Items.Refresh();
        MarkMacroChangesPending();
    }

    private void AddPlainStep_Click(object sender, RoutedEventArgs e) => AddOperation(new() { Kind = TextOperationKind.PlainText });
    private void AddNewlineStep_Click(object sender, RoutedEventArgs e)
    {
        var mode = (MacroNewlineMode.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "CRLF";
        AddOperation(new() { Kind = TextOperationKind.NormalizeNewlines, Value = mode });
    }
    private void AddBlankStep_Click(object sender, RoutedEventArgs e) => AddOperation(new() { Kind = TextOperationKind.CollapseBlankLines });
    private void AddReplaceStep_Click(object sender, RoutedEventArgs e) => AddOperation(CreateMacroReplaceOperation());

    private TextOperation CreateMacroReplaceOperation() => new()
    {
        Kind = TextOperationKind.ReplaceText,
        Value = MacroFindText.Text,
        Replacement = MacroReplacementText.Text,
        UseRegularExpression = MacroRegexMode.IsChecked == true,
        IgnoreCase = MacroIgnoreCase.IsChecked == true,
        Multiline = MacroRegexMultiline.IsChecked == true
    };

    private void MacroSteps_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MacroSteps.SelectedItem is not TextOperation operation) return;
        MacroFindText.Text = operation.Kind == TextOperationKind.ReplaceText ? operation.Value : "";
        MacroReplacementText.Text = operation.Kind == TextOperationKind.ReplaceText ? operation.Replacement : "";
        MacroRegexMode.IsChecked = operation.UseRegularExpression;
        MacroIgnoreCase.IsChecked = operation.IgnoreCase;
        MacroRegexMultiline.IsChecked = operation.Multiline;
        if (operation.Kind == TextOperationKind.NormalizeNewlines)
            MacroNewlineMode.SelectedIndex = operation.Value switch { "LF" => 1, "CR" => 2, _ => 0 };
    }

    private void AddOperation(TextOperation operation)
    {
        if (selectedMacro is null)
        {
            MacroStatus.Text = LocalizationManager.T("CreateOrSelectMacro");
            return;
        }
        selectedMacro.Operations.Add(operation);
        MarkMacroChangesPending();
        MacroSteps.Items.Refresh();
        MacroSteps.SelectedIndex = selectedMacro.Operations.Count - 1;
        MacroStatus.Text = LocalizationManager.T("StepAdded");
    }

    private void UpdateStep_Click(object sender, RoutedEventArgs e)
    {
        if (selectedMacro is null || MacroSteps.SelectedItem is not TextOperation operation) return;
        switch (operation.Kind)
        {
            case TextOperationKind.NormalizeNewlines:
                operation.Value = (MacroNewlineMode.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "CRLF";
                break;
            case TextOperationKind.ReplaceText:
                var replacement = CreateMacroReplaceOperation();
                operation.Value = replacement.Value;
                operation.Replacement = replacement.Replacement;
                operation.UseRegularExpression = replacement.UseRegularExpression;
                operation.IgnoreCase = replacement.IgnoreCase;
                operation.Multiline = replacement.Multiline;
                break;
        }
        MarkMacroChangesPending();
        MacroSteps.Items.Refresh();
        MacroStatus.Text = LocalizationManager.T("StepUpdated");
    }

    private void SaveMacro_Click(object sender, RoutedEventArgs e)
    {
        if (selectedMacro is null) { MacroStatus.Text = LocalizationManager.T("SelectMacroToSave"); return; }
        if (string.IsNullOrWhiteSpace(MacroName.Text)) { MacroStatus.Text = LocalizationManager.T("MacroNameRequired"); return; }
        selectedMacro.Name = MacroName.Text.Trim();
        TrySaveMacros(showSuccess: true);
    }

    private bool TrySaveMacros(bool showSuccess)
    {
        if (macros.Any(macro => string.IsNullOrWhiteSpace(macro.Name)))
        {
            MacroStatus.Text = LocalizationManager.T("AllMacroNamesRequired");
            return false;
        }

        try
        {
            settingsStore.SaveMacros(macros);
            macroChangesPending = false;
            MacroList.Items.Refresh();
            if (showSuccess) MacroStatus.Text = LocalizationManager.Format("MacroSaved", selectedMacro?.Name ?? "");
            return true;
        }
        catch (Exception ex)
        {
            MacroStatus.Text = LocalizationManager.Format("MacroSaveError", ex.Message);
            if (!showSuccess)
            {
                MessageBox.Show(this, MacroStatus.Text, LocalizationManager.T("MacroSaveDialogTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return false;
        }
    }

    private void MarkMacroChangesPending()
    {
        macroChangesPending = true;
        MacroStatus.Text = LocalizationManager.T("MacroUnsaved");
    }

    private void DeleteMacro_Click(object sender, RoutedEventArgs e)
    {
        if (selectedMacro is null) return;
        var removed = selectedMacro;
        var name = removed.Name;
        var index = macros.IndexOf(removed);
        macros.RemoveAt(index);
        try
        {
            settingsStore.SaveMacros(macros);
            macroChangesPending = false;
        }
        catch (Exception ex)
        {
            macros.Insert(index, removed);
            MacroList.SelectedItem = removed;
            selectedMacro = removed;
            MacroStatus.Text = LocalizationManager.Format("MacroDeleteError", ex.Message);
            return;
        }

        MacroStatus.Text = LocalizationManager.Format("MacroDeleted", name);
    }

    private void MoveStepUp_Click(object sender, RoutedEventArgs e) => MoveSelectedStep(-1);
    private void MoveStepDown_Click(object sender, RoutedEventArgs e) => MoveSelectedStep(1);

    private void MoveSelectedStep(int delta)
    {
        if (selectedMacro is null) return;
        var index = MacroSteps.SelectedIndex;
        var next = index + delta;
        if (index < 0 || next < 0 || next >= selectedMacro.Operations.Count) return;
        (selectedMacro.Operations[index], selectedMacro.Operations[next]) =
            (selectedMacro.Operations[next], selectedMacro.Operations[index]);
        MarkMacroChangesPending();
        MacroSteps.Items.Refresh();
        MacroSteps.SelectedIndex = next;
    }

    private void RemoveStep_Click(object sender, RoutedEventArgs e)
    {
        if (selectedMacro is null || MacroSteps.SelectedItem is not TextOperation operation) return;
        selectedMacro.Operations.Remove(operation);
        MarkMacroChangesPending();
        MacroSteps.Items.Refresh();
    }

    private void RunMacro_Click(object sender, RoutedEventArgs e)
    {
        if (selectedMacro is null) { MacroStatus.Text = LocalizationManager.T("SelectMacroToRun"); return; }
        try
        {
            TextEditor.Text = transforms.ApplyMacro(TextEditor.Text, selectedMacro);
            MacroStatus.Text = LocalizationManager.Format("MacroRan", selectedMacro.Name);
        }
        catch (Exception ex)
        {
            MacroStatus.Text = LocalizationManager.Format("MacroRunError", ex.Message);
        }
    }

    private async void Translate_Click(object sender, RoutedEventArgs e)
    {
        TranslationStatus.Text = LocalizationManager.T("TranslationWorking");
        try
        {
            var result = await translation.TranslateAsync(TranslationSource.Text,
                EndpointSetting.Text, ModelSetting.Text, SourceLanguageSetting.Text, TargetLanguageSetting.Text);
            TranslationResult.Text = result;
            TranslationStatus.Text = LocalizationManager.T("TranslationDone");
        }
        catch (Exception ex)
        {
            TranslationStatus.Text = LocalizationManager.Format("TranslationError", ex.Message);
        }
    }

    private void LoadSelectedTranslation_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is HistoryEntry entry)
        {
            TranslationSource.Text = entry.Text;
            TranslationStatus.Text = LocalizationManager.T("TranslationLoaded");
        }
        else TranslationStatus.Text = LocalizationManager.T("SelectHistory");
    }

    private void UseTranslation_Click(object sender, RoutedEventArgs e)
    {
        TextEditor.Text = TranslationResult.Text;
        EditStatus.Text = LocalizationManager.T("TranslationToEditor");
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var nextSettings = new AppSettings
        {
            OllamaEndpoint = EndpointSetting.Text.Trim(),
            OllamaModel = ModelSetting.Text.Trim(),
            SourceLanguage = SourceLanguageSetting.Text.Trim(),
            TargetLanguage = TargetLanguageSetting.Text.Trim(),
            Theme = (ThemeSetting.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System",
            UiLanguage = (LanguageSetting.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System",
            ActivationHotkey = HotkeySetting.Text.Trim(),
            WindowPlacement = (DisplayPositionSetting.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Caret"
        };
        try
        {
            var hotkey = HotkeySetting.Text.Trim();
            var shortcutStatus = LocalizationManager.T("SettingsSaved");
            if (Application.Current is App app && !app.ConfigureGlobalHotkey(hotkey, out shortcutStatus))
            {
                FooterStatus.Text = shortcutStatus;
                return;
            }
            settingsStore.SaveSettings(nextSettings);
            startupRegistration.SetEnabled(AutoStartSetting.IsChecked == true);
            settings = nextSettings;
            ThemeManager.Apply(settings.Theme);
            LocalizationManager.Apply(settings.UiLanguage);
            MacroSteps.Items.Refresh();
            if (Application.Current is App currentApp) currentApp.RefreshTrayLocalization();
            FooterStatus.Text = hotkey.Length == 0 ? LocalizationManager.T("SettingsSaved") : shortcutStatus;
        }
        catch (Exception ex)
        {
            if (Application.Current is App app)
                app.ConfigureGlobalHotkey(settings.ActivationHotkey, out _);
            FooterStatus.Text = LocalizationManager.Format("SettingsSaveError", ex.Message);
        }
    }

    private void PopulateSettingsControls()
    {
        EndpointSetting.Text = settings.OllamaEndpoint;
        ModelSetting.Text = settings.OllamaModel;
        SourceLanguageSetting.Text = settings.SourceLanguage == "自動判定" ? LocalizationManager.T("SourceAuto") : settings.SourceLanguage;
        TargetLanguageSetting.Text = settings.TargetLanguage;
        AutoStartSetting.IsChecked = startupRegistration.IsEnabled();
        HotkeySetting.Text = settings.ActivationHotkey;
        DisplayPositionSetting.SelectedIndex = settings.WindowPlacement == "Mouse" ? 1 : 0;
        ThemeSetting.SelectedIndex = settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        LanguageSetting.SelectedIndex = settings.UiLanguage switch { "English" => 1, "Japanese" => 2, _ => 0 };
    }

    private void HotkeySetting_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None)
        {
            FooterStatus.Text = LocalizationManager.T("HotkeyModifierRequired");
            return;
        }

        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        HotkeySetting.Text = string.Join('+', parts);
    }

    private void ClearHotkey_Click(object sender, RoutedEventArgs e) => HotkeySetting.Clear();

    private sealed class HistoryEntry(ClipboardHistoryItem item, string text)
    {
        public ClipboardHistoryItem Item { get; } = item;
        public string Text { get; } = text;
        public DateTimeOffset Timestamp => Item.Timestamp;
        public string DisplayText => $"{Timestamp.LocalDateTime:g}  {MakePreview(Text)}";

        private static string MakePreview(string value)
        {
            var preview = Regex.Replace(value, "\\s+", " ").Trim();
            return preview.Length > 160 ? preview[..160] + "…" : preview;
        }
    }
}
