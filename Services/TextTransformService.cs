using System.Text.RegularExpressions;
using Clippy.Models;

namespace Clippy.Services;

public sealed class TextTransformService
{
    public string Apply(string text, TextOperation operation) => operation.Kind switch
    {
        TextOperationKind.PlainText => PlainTextConverter.FromText(text),
        TextOperationKind.NormalizeNewlines => NormalizeNewlines(text, operation.Value),
        TextOperationKind.CollapseBlankLines => CollapseBlankLines(text),
        TextOperationKind.ReplaceText => Replace(text, operation),
        _ => text
    };

    public string ApplyMacro(string text, TextMacro macro)
    {
        foreach (var operation in macro.Operations)
            text = Apply(text, operation);
        return text;
    }

    public static string NormalizeNewlines(string text, string mode)
    {
        var newline = mode switch { "LF" => "\n", "CR" => "\r", _ => "\r\n" };
        return Regex.Replace(text, "\\r\\n|\\r|\\n", _ => newline);
    }

    public static string CollapseBlankLines(string text) =>
        Regex.Replace(text, "(?:\\r\\n|\\r|\\n)[ \\t]*(?:(?:\\r\\n|\\r|\\n)[ \\t]*){2,}",
            Environment.NewLine + Environment.NewLine);

    private static string Replace(string text, TextOperation operation)
    {
        if (string.IsNullOrEmpty(operation.Value)) return text;
        if (operation.UseRegularExpression)
        {
            var options = (operation.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None) |
                          (operation.Multiline ? RegexOptions.Multiline : RegexOptions.None);
            return Regex.Replace(text, operation.Value, operation.Replacement, options, TimeSpan.FromSeconds(2));
        }
        if (operation.IgnoreCase)
            return new Regex(Regex.Escape(operation.Value),
                RegexOptions.IgnoreCase | (operation.Multiline ? RegexOptions.Multiline : RegexOptions.None),
                TimeSpan.FromSeconds(2))
                .Replace(text, _ => operation.Replacement);
        return text.Replace(operation.Value, operation.Replacement, StringComparison.Ordinal);
    }
}
