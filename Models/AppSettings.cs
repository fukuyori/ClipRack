namespace Clippy.Models;

public sealed class AppSettings
{
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "";
    public string SourceLanguage { get; set; } = "Auto-detect";
    public string TargetLanguage { get; set; } = "日本語";
    public string ActivationHotkey { get; set; } = "";
    public string WindowPlacement { get; set; } = "Caret";
    public string Theme { get; set; } = "System";
    public string UiLanguage { get; set; } = "System";
}
