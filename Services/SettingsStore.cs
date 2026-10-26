using System.IO;
using System.Text.Json;
using Clippy.Models;

namespace Clippy.Services;

public sealed class SettingsStore
{
    private readonly string directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clippy");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string MacroFilePath => Path.Combine(directory, "macros.json");

    public AppSettings LoadSettings()
    {
        var path = Path.Combine(directory, "settings.json");
        if (!File.Exists(path)) return new AppSettings();
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings(); }
        catch (JsonException) { return new AppSettings(); }
    }

    public void SaveSettings(AppSettings settings)
    {
        SaveJson(Path.Combine(directory, "settings.json"), JsonSerializer.Serialize(settings, JsonOptions));
    }

    public List<TextMacro> LoadMacros()
    {
        if (!File.Exists(MacroFilePath)) return [];
        try { return JsonSerializer.Deserialize<List<TextMacro>>(File.ReadAllText(MacroFilePath)) ?? []; }
        catch (JsonException) { return []; }
    }

    public void SaveMacros(IEnumerable<TextMacro> macros)
    {
        SaveJson(MacroFilePath, JsonSerializer.Serialize(macros, JsonOptions));
    }

    private void SaveJson(string path, string contents)
    {
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, contents);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
