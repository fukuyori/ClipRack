using System.Text.Json.Serialization;
using Clippy.Services;

namespace Clippy.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TextOperationKind
{
    PlainText,
    NormalizeNewlines,
    CollapseBlankLines,
    ReplaceText
}

public sealed class TextOperation
{
    public TextOperationKind Kind { get; set; }
    public string Value { get; set; } = "";
    public string Replacement { get; set; } = "";
    public bool UseRegularExpression { get; set; }
    public bool IgnoreCase { get; set; }
    public bool Multiline { get; set; }

    public override string ToString() => Kind switch
    {
        TextOperationKind.PlainText => LocalizationManager.T("PlainText"),
        TextOperationKind.NormalizeNewlines => LocalizationManager.Format("NewlinesConverted", Value),
        TextOperationKind.CollapseBlankLines => LocalizationManager.T("CollapseBlankLines"),
        TextOperationKind.ReplaceText => $"{LocalizationManager.T("Replace")}{(UseRegularExpression ? $" ({LocalizationManager.T("Regex")})" : "")}: {Value} → {Replacement}",
        _ => Kind.ToString()
    };
}

public sealed class TextMacro
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Macro";
    public List<TextOperation> Operations { get; set; } = [];

    public override string ToString() => Name;
}
