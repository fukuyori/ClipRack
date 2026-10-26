using System.Globalization;
using System.Windows;

namespace Clippy.Services;

public static class LocalizationManager
{
    private static readonly IReadOnlyDictionary<string, (string English, string Japanese)> Strings =
        new Dictionary<string, (string, string)>
        {
            ["Title"] = ("ClipRack - Clipboard Text Editor", "ClipRack - クリップボードテキスト編集"),
            ["Subtitle"] = ("Edit and transform text from Windows clipboard history.", "Windowsのクリップボード履歴からテキストを編集・変換します。"),
            ["History"] = ("History", "履歴"), ["RefreshHistory"] = ("Refresh", "履歴を更新"),
            ["TextEditing"] = ("Text", "テキスト編集"), ["LoadSelectedHistory"] = ("Load Selected", "選択履歴を読み込む"),
            ["PlainText"] = ("Convert to Plain Text", "平文に変換"), ["Newline"] = ("Newline:", "改行:"),
            ["ConvertNewline"] = ("Convert Newlines", "改行を変換"), ["CollapseBlankLines"] = ("Collapse Blank Lines", "空行を整理"),
            ["Find"] = ("Find:", "検索:"), ["ReplaceWith"] = ("Replace with:", "置換後:"),
            ["Regex"] = ("Regular expression", "正規表現"), ["IgnoreCase"] = ("Ignore case", "大文字小文字を区別しない"),
            ["Multiline"] = ("Multiline", "複数行"), ["Replace"] = ("Replace", "置換"),
            ["CopyResult"] = ("Copy Result to Clipboard", "結果をクリップボードへ"), ["Macros"] = ("Macros", "マクロ"),
            ["New"] = ("New", "新規"), ["Delete"] = ("Delete", "削除"), ["Save"] = ("Save", "保存"),
            ["MacroHelp"] = ("Add steps and run them from top to bottom.", "処理を追加し、上から順に実行します。"),
            ["IgnoreCaseShort"] = ("Ignore case", "大小文字無視"),
            ["AddPlain"] = ("Add Plain Text", "平文化を追加"), ["AddNewline"] = ("Add Newline Conversion", "改行変換を追加"),
            ["AddBlank"] = ("Add Blank Line Cleanup", "空行整理を追加"), ["AddReplace"] = ("Add Replace", "置換を追加"),
            ["MoveUp"] = ("Move Up", "上へ"), ["MoveDown"] = ("Move Down", "下へ"),
            ["UpdateStep"] = ("Update Step", "選択を更新"), ["RemoveStep"] = ("Remove Step", "選択を削除"),
            ["RunMacro"] = ("Run Selected Macro", "選択中のマクロを実行"),
            ["MacroTabHelp"] = ("The macro uses the text in the Text tab. Review the result before copying it to the clipboard.", "実行対象には「テキスト編集」タブの内容を使います。結果を確認してからクリップボードへ反映してください。"),
            ["Translation"] = ("Translation", "翻訳"), ["OllamaTranslation"] = ("Ollama Translation", "Ollama翻訳"),
            ["LoadFromHistory"] = ("Load from History", "履歴から読み込む"), ["Translate"] = ("Translate", "翻訳"),
            ["UseTranslation"] = ("Load Result into Text Editor", "翻訳結果を編集欄へ"), ["Settings"] = ("Settings", "設定"),
            ["AppSettings"] = ("Application Settings", "アプリ設定"), ["Theme"] = ("Theme", "テーマ"),
            ["SystemDefault"] = ("Use Windows setting", "Windowsの設定に従う"),
            ["Light"] = ("Light", "ライト"), ["Dark"] = ("Dark", "ダーク"),
            ["Language"] = ("Language", "使用言語"), ["English"] = ("English", "English"), ["Japanese"] = ("Japanese", "日本語"),
            ["AutoStart"] = ("Start ClipRack when I sign in to Windows", "Windowsへのサインイン時にClipRackを自動起動する"),
            ["Hotkey"] = ("Activation shortcut", "呼び出しキー"), ["Clear"] = ("Clear", "解除"),
            ["HotkeyTip"] = ("Focus here and press a key combination. Leave blank to disable the shortcut.", "ここを選択してキーの組み合わせを押します。空欄なら無効です。"),
            ["WindowPosition"] = ("Window position", "表示位置"), ["NearCaret"] = ("Near text cursor", "テキストカーソル付近"),
            ["NearMouse"] = ("Near mouse pointer", "マウスカーソル付近"), ["OllamaEndpoint"] = ("Ollama endpoint", "Ollama接続先"),
            ["Model"] = ("Model", "モデル名"), ["SourceLanguage"] = ("Source language", "翻訳元言語"),
            ["TargetLanguage"] = ("Target language", "翻訳先言語"), ["SaveSettings"] = ("Save Settings", "設定を保存"),
            ["TrayOpen"] = ("Open", "開く"), ["TrayExit"] = ("Exit", "終了"),
            ["ClipboardTitle"] = ("ClipRack - Clipboard Text Editor", "ClipRack - クリップボードテキスト編集"),
            ["SourceAuto"] = ("Auto-detect", "自動判定")
            , ["MacroPendingQuestion"] = ("There are unsaved macro changes. Save before exiting?", "マクロに未保存の変更があります。保存して終了しますか？")
            , ["UnsavedMacros"] = ("Unsaved Macros", "未保存のマクロ")
            , ["HistoryLoading"] = ("Loading clipboard history…", "履歴を読み込んでいます…")
            , ["HistoryDisabled"] = ("Windows clipboard history is disabled. Enable it in Windows Settings.", "Windowsのクリップボード履歴が無効です。設定で履歴を有効にしてください。")
            , ["HistoryFailed"] = ("Could not retrieve clipboard history ({0}).", "履歴を取得できませんでした（{0}）。")
            , ["HistoryCount"] = ("Showing {0} text history item(s). Non-text items, such as images, are omitted.", "テキスト履歴 {0} 件を表示しています。画像などのテキスト以外の項目は除外しています。")
            , ["HistoryError"] = ("Could not retrieve clipboard history: {0}", "履歴を取得できませんでした: {0}")
            , ["LoadedHistory"] = ("Loaded text from history at {0}. The original history item was not changed.", "履歴 {0} のテキストを読み込みました。元の履歴項目は変更していません。")
            , ["NoMacros"] = ("No saved macros", "保存済みマクロがありません")
            , ["MacroApplied"] = ("Applied '{0}' and added the result to clipboard history. The original item was not changed.", "履歴の内容に「{0}」を適用し、変換結果をクリップボードへ登録しました。元の履歴項目は変更していません。")
            , ["MacroApplyError"] = ("Could not apply '{0}': {1}", "「{0}」を適用できませんでした: {1}")
            , ["SelectHistory"] = ("Select a text item on the History tab.", "履歴タブでテキスト項目を選択してください。")
            , ["PlainConverted"] = ("Converted to plain text. Review the result before copying it to the clipboard.", "平文に変換しました。内容を確認してからクリップボードへ反映してください。")
            , ["NewlinesConverted"] = ("Converted line endings to {0}.", "改行コードを {0} に統一しました。")
            , ["BlankLinesCollapsed"] = ("Collapsed consecutive blank lines.", "連続する空行を整理しました。")
            , ["RegexReplaced"] = ("Replaced text using a regular expression.", "正規表現で置換しました。")
            , ["TextReplaced"] = ("Replaced text.", "置換しました。")
            , ["RegexTimeout"] = ("Regular expression processing timed out. Check the pattern.", "正規表現の処理が時間制限を超えました。パターンを確認してください。")
            , ["RegexInvalid"] = ("Invalid regular expression: {0}", "正規表現が正しくありません: {0}")
            , ["ClipboardCopied"] = ("Copied the result to the clipboard. Check the History tab to confirm it was added to Windows history.", "変換結果をクリップボードへ反映しました。Windows履歴への登録は履歴タブで確認してください。")
            , ["ClipboardCopyError"] = ("Could not copy to the clipboard: {0}", "クリップボードへ反映できませんでした: {0}")
            , ["NewMacroName"] = ("New Macro", "新しいマクロ")
            , ["AddStepsPrompt"] = ("Add steps, name the macro, and save it.", "処理を追加して名前を付け、保存してください。")
            , ["CreateOrSelectMacro"] = ("Create a macro or select an existing one first.", "先にマクロを新規作成するか、既存のマクロを選択してください。")
            , ["StepAdded"] = ("Step added. Select a replace step to edit its pattern and options.", "処理を追加しました。置換ステップを選択すると、そのパターンと設定を編集できます。")
            , ["StepUpdated"] = ("Step updated. Save the macro to keep the change.", "選択した処理の設定を更新しました。マクロを保存してください。")
            , ["SelectMacroToSave"] = ("Select a macro to save.", "保存するマクロを選択してください。")
            , ["MacroNameRequired"] = ("Enter a macro name.", "マクロ名を入力してください。")
            , ["AllMacroNamesRequired"] = ("One or more macros have no name. Name each macro before saving.", "名前が空のマクロがあります。各マクロに名前を付けてから保存してください。")
            , ["MacroSaved"] = ("Saved '{0}'.", "「{0}」を保存しました。")
            , ["MacroSaveError"] = ("Could not save macros. Check the save location and access permissions: {0}", "マクロを保存できませんでした。保存先とアクセス権を確認してください: {0}")
            , ["MacroSaveDialogTitle"] = ("Could Not Save Macros", "マクロを保存できません")
            , ["MacroUnsaved"] = ("Macro changes are unsaved. Save them before continuing.", "マクロに未保存の変更があります。保存してください。")
            , ["MacroDeleteError"] = ("Could not delete the macro. Changes are preserved: {0}", "マクロを削除できませんでした。変更は保持しています: {0}")
            , ["MacroDeleted"] = ("Deleted '{0}'.", "「{0}」を削除しました。")
            , ["SelectMacroToRun"] = ("Select a macro to run.", "実行するマクロを選択してください。")
            , ["MacroRan"] = ("Ran '{0}'. Review the result on the Text tab.", "「{0}」を実行しました。結果はテキスト編集タブで確認できます。")
            , ["MacroRunError"] = ("Could not run the macro: {0}", "マクロを実行できませんでした: {0}")
            , ["TranslationWorking"] = ("Asking Ollama to translate…", "Ollamaに翻訳を依頼しています…")
            , ["TranslationDone"] = ("Translation complete. Review the result.", "翻訳が完了しました。結果を確認してください。")
            , ["TranslationError"] = ("Translation failed: {0}", "翻訳できませんでした: {0}")
            , ["TranslationLoaded"] = ("Loaded the selected history text as the translation source.", "選択した履歴のテキストを翻訳元に読み込みました。")
            , ["TranslationToEditor"] = ("Loaded the translation result into the text editor.", "翻訳結果を編集欄に読み込みました。")
            , ["SettingsSaved"] = ("Settings saved.", "設定を保存しました。")
            , ["SettingsSaveError"] = ("Could not save settings. Check the save location and access permissions: {0}", "設定を保存できませんでした。保存先とアクセス権を確認してください: {0}")
            , ["HotkeyModifierRequired"] = ("Include at least one modifier: Ctrl, Alt, Shift, or Windows.", "ショートカットキーにはCtrl、Alt、Shift、Windowsキーのいずれかを含めてください。")
            , ["ClipboardSkip"] = ("Skipped newline normalization because formatted or unsupported data is present: {0}", "書式付きまたは未対応の形式を含むため、改行補正をスキップしました: {0}")
            , ["ClipboardAlreadyCrLf"] = ("Clipboard text already uses CRLF; no change was needed.", "クリップボードのテキストはCRLFです。改行補正は不要でした。")
            , ["ClipboardNormalized"] = ("Normalized clipboard text line endings to CRLF.", "クリップボードのテキスト改行をCRLFに補正しました。")
            , ["ClipboardNormalizeError"] = ("Could not normalize clipboard line endings. It will be checked again after the next update.", "クリップボードの改行を自動補正できませんでした。次の更新時に再確認します。")
            , ["ClipboardListenerError"] = ("Could not start clipboard monitoring: {0}", "クリップボード監視を開始できませんでした: {0}")
            , ["ShortcutInitError"] = ("Could not initialize the shortcut feature.", "ショートカットキー機能を初期化できませんでした。")
            , ["ShortcutWindowError"] = ("Could not get the window needed to register the shortcut.", "ショートカットキーを登録するウィンドウを取得できませんでした。")
            , ["ShortcutDisabled"] = ("No activation shortcut is set.", "ショートカットキーを設定しません。")
            , ["ShortcutRegistering"] = ("Registering shortcut {0}…", "ショートカットキー {0} を登録しています。")
            , ["ShortcutInvalid"] = ("Could not read the shortcut setting. Re-enter it in Settings.", "ショートカットキーの設定を読み取れません。設定画面で指定し直してください。")
            , ["ShortcutInUse"] = ("This shortcut is already used by another app. Choose a different one.", "このショートカットキーは他のアプリで使用中のため登録できません。別のキーを指定してください。")
            , ["ShortcutRegistered"] = ("Registered shortcut {0}.", "ショートカットキー {0} を登録しました。")
            , ["StartupRegistryError"] = ("Could not open Windows startup settings.", "Windowsの自動起動設定を開けませんでした。")
            , ["ExecutablePathError"] = ("Could not determine the application executable path.", "アプリの実行ファイルパスを取得できませんでした。")
            , ["OllamaModelRequired"] = ("Set an Ollama model name.", "Ollamaのモデル名を設定してください。")
            , ["OllamaEmptyResult"] = ("Ollama returned no translation result.", "Ollamaから翻訳結果が返りませんでした。")
        };

    public static string CurrentLanguage { get; private set; } = "English";

    public static string ResolveLanguage(string preference) => preference switch
    {
        "Japanese" => "Japanese",
        "English" => "English",
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase) ? "Japanese" : "English"
    };

    public static string T(string key)
    {
        if (!Strings.TryGetValue(key, out var value)) return key;
        return CurrentLanguage == "Japanese" ? value.Japanese : value.English;
    }

    public static string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);

    public static void Apply(string preference)
    {
        CurrentLanguage = ResolveLanguage(preference);
        if (Application.Current is null) return;
        foreach (var entry in Strings)
            Application.Current.Resources[$"Text.{entry.Key}"] = CurrentLanguage == "Japanese" ? entry.Value.Japanese : entry.Value.English;
    }
}
