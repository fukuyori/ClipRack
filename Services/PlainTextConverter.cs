using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Windows.Documents;
using Windows.ApplicationModel.DataTransfer;

namespace Clippy.Services;

public static partial class PlainTextConverter
{
    public static string FromRtf(string rtf)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(rtf));
        var document = new FlowDocument(new Paragraph());
        var range = new TextRange(document.ContentStart, document.ContentEnd);
        range.Load(input, System.Windows.DataFormats.Rtf);
        return range.Text.TrimEnd('\r', '\n');
    }

    public static string FromHtml(string html)
    {
        try
        {
            var fragment = HtmlFormatHelper.GetStaticFragment(html);
            if (!string.IsNullOrWhiteSpace(fragment)) html = fragment;
        }
        catch { /* The input may be a standalone HTML document rather than clipboard HTML. */ }

        html = Regex.Replace(html, "<(script|style)\\b[^>]*>.*?</\\1\\s*>", "",
            RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
        html = Regex.Replace(html, "<!--.*?-->", "", RegexOptions.Singleline);
        html = Regex.Replace(html, "<br\\b[^>]*>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, "</(?:p|div|li|tr|h[1-6]|blockquote|pre)\\s*>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, "<[^>]*>", "");
        return WebUtility.HtmlDecode(html).Replace("\r\n", "\n").Replace('\r', '\n');
    }

    public static string FromXml(string xml)
    {
        try
        {
            var root = XDocument.Parse(xml, LoadOptions.PreserveWhitespace).Root;
            if (root is null) return xml;
            var output = new StringBuilder();
            AppendXmlText(root, output, root);
            return output.ToString().TrimEnd('\r', '\n');
        }
        catch { return xml; }
    }

    private static void AppendXmlText(XElement element, StringBuilder output, XElement root)
    {
        foreach (var node in element.Nodes())
        {
            if (node is XText text)
                output.Append(text.Value);
            else if (node is XElement child)
            {
                AppendXmlText(child, output, root);
                if (IsBlockElement(child.Name.LocalName) || ReferenceEquals(element, root))
                    output.AppendLine();
            }
        }
    }

    private static bool IsBlockElement(string name) => name.ToLowerInvariant() is
        "p" or "div" or "li" or "tr" or "br" or "section" or "article" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6";

    public static string FromText(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith('<'))
        {
            if (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) || LooksLikeXml(trimmed))
                return FromXml(text);
            if (Regex.IsMatch(trimmed, "</?[a-z][a-z0-9:-]*(?:\\s|/?>)", RegexOptions.IgnoreCase))
                return FromHtml(text);
        }
        return text;
    }

    private static bool LooksLikeXml(string value)
    {
        try { _ = XDocument.Parse(value); return true; }
        catch { return false; }
    }
}
