using System;
using System.Text.RegularExpressions;

namespace Action_Wheel.Core
{
    /// <summary>
    /// Rewrites an SVG document so every painted shape uses one colour.
    /// </summary>
    /// <remarks>
    /// There is no way to recolour an SVG from the outside: <c>SvgImageSource</c> renders the
    /// document exactly as authored, and a <c>Foreground</c> on the hosting element reaches nothing
    /// inside it. The document itself has to change, which is why this returns text rather than
    /// configuring anything.
    ///
    /// The rewrite is deliberately textual rather than a full XML round-trip. An SVG in the wild is
    /// often not well-formed enough for XDocument (stray entities, mismatched namespaces from
    /// export tools), and failing to draw an icon at all is a worse outcome than tinting one
    /// imperfectly - <see cref="Apply"/> therefore never throws and returns the original text when
    /// it cannot do better.
    /// </remarks>
    public static class SvgTint
    {
        // "fill" / "stroke" / "stop-color" as XML attributes, capturing the quoted value.
        private static readonly Regex PaintAttribute = new(
            @"\b(fill|stroke|stop-color|flood-color|lighting-color)\s*=\s*(""[^""]*""|'[^']*')",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // The same properties inside a style="..." attribute or a <style> block.
        private static readonly Regex PaintProperty = new(
            @"\b(fill|stroke|stop-color|flood-color|lighting-color)\s*:\s*([^;""'}]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex OpeningSvgTag = new(
            @"<svg\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Returns <paramref name="svg"/> with every paint recoloured to <paramref name="color"/>.
        /// </summary>
        /// <param name="color">The target colour. Alpha is dropped: SVG paints are opaque, and
        /// transparency belongs to the element drawing the icon, not the document.</param>
        public static string Apply(string svg, ColorValue color)
        {
            if (string.IsNullOrEmpty(svg))
                return svg;

            try
            {
                string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

                string result = PaintAttribute.Replace(svg, match =>
                {
                    string value = match.Groups[2].Value;
                    char quote = value.Length > 0 ? value[0] : '"';
                    string inner = value.Length >= 2 ? value[1..^1] : string.Empty;
                    return KeepsOwnPaint(inner)
                        ? match.Value
                        : $"{match.Groups[1].Value}={quote}{hex}{quote}";
                });

                result = PaintProperty.Replace(result, match =>
                    KeepsOwnPaint(match.Groups[2].Value)
                        ? match.Value
                        : $"{match.Groups[1].Value}:{hex}");

                return AddRootPaint(result, hex);
            }
            catch (Exception)
            {
                // A pathological document that makes the regex engine give up is still drawable in
                // its original colours; that is strictly better than no icon.
                return svg;
            }
        }

        /// <summary>
        /// Paints that must survive untouched: "none" is what makes an outline-only icon an
        /// outline, and a url(#...) reference points at a gradient or pattern the caller may have
        /// authored deliberately. Overwriting either turns the icon into a solid blob.
        /// </summary>
        private static bool KeepsOwnPaint(string value)
        {
            var trimmed = value.Trim();
            return trimmed.Length == 0
                || trimmed.Equals("none", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("transparent", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("url(", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Shapes that name no paint at all default to black, and "currentColor" resolves against
        /// the inherited CSS color. Neither is reached by the substitutions above, so the root
        /// element carries both as a fallback for anything the document left unsaid.
        /// </summary>
        private static string AddRootPaint(string svg, string hex)
        {
            var match = OpeningSvgTag.Match(svg);
            if (!match.Success)
                return svg;

            string tag = match.Value;
            string additions = string.Empty;

            if (!Regex.IsMatch(tag, @"\bfill\s*=", RegexOptions.IgnoreCase))
                additions += $" fill=\"{hex}\"";
            if (!Regex.IsMatch(tag, @"\bcolor\s*=", RegexOptions.IgnoreCase))
                additions += $" color=\"{hex}\"";

            if (additions.Length == 0)
                return svg;

            // Insert before the tag's own closing bracket, keeping a self-closing "/>" intact.
            int insertAt = tag.EndsWith("/>", StringComparison.Ordinal) ? tag.Length - 2 : tag.Length - 1;
            string replacement = tag[..insertAt] + additions + tag[insertAt..];

            return svg[..match.Index] + replacement + svg[(match.Index + tag.Length)..];
        }
    }
}
