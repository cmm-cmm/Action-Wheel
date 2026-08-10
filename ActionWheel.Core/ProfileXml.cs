using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Action_Wheel.Core
{
    /// <summary>Imports and exports a portable, versioned Action Wheel profile.</summary>
    public static class ProfileXml
    {
        private const string RootName = "ActionWheelProfile";
        private const string CurrentVersion = "1";

        public static bool Save(string path, IEnumerable<ActionItem> actions, out string error)
        {
            error = string.Empty;
            string? temporaryPath = null;

            try
            {
                var actionList = actions.OrderBy(action => action.Tag).ToList();
                if (!ActionConfig.TryValidate(actionList, out error))
                    return false;

                var document = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    new XElement(RootName,
                        new XAttribute("version", CurrentVersion),
                        actionList.Select(action => new XElement("Action",
                            new XAttribute("tag", action.Tag),
                            new XAttribute("type", ActionValueCodec.KindToString(action.Kind)),
                            new XElement("Label", action.Label),
                            new XElement("Value", action.Value),
                            new XElement("Arguments", action.Arguments),
                            new XElement("Glyph", action.Glyph),
                            new XElement("IconPath", action.IconPath),
                            new XElement("IconScale", action.IconScale),
                            new XElement("IconOffsetX", action.IconOffsetX),
                            new XElement("IconOffsetY", action.IconOffsetY),
                            new XElement("IconTint", action.IconTint),
                            new XElement("Foreground", action.Foreground),
                            new XElement("Background", action.Background),
                            new XElement("Shadow", action.Shadow),
                            new XElement("ShadowEnabled", action.ShadowEnabled),
                            new XElement("ShadowOpacity", action.ShadowOpacity),
                            new XElement("ShadowBlur", action.ShadowBlur),
                            new XElement("ShadowOffsetX", action.ShadowOffsetX),
                            new XElement("ShadowOffsetY", action.ShadowOffsetY)))));

                string fullPath = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

                var settings = new XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    Indent = true,
                    NewLineChars = Environment.NewLine,
                };

                using (var writer = XmlWriter.Create(temporaryPath, settings))
                    document.Save(writer);

                File.Move(temporaryPath, fullPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (temporaryPath != null)
                {
                    try { File.Delete(temporaryPath); } catch { }
                }
            }
        }

        public static bool TryLoad(string path, out IReadOnlyList<ActionItem> actions, out string error)
        {
            actions = Array.Empty<ActionItem>();
            error = string.Empty;

            try
            {
                var readerSettings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                };

                XDocument document;
                using (var reader = XmlReader.Create(path, readerSettings))
                    document = XDocument.Load(reader, LoadOptions.None);

                var root = document.Root;
                if (root?.Name.LocalName != RootName)
                {
                    error = $"The XML root must be <{RootName}>.";
                    return false;
                }

                string version = (string?)root.Attribute("version") ?? string.Empty;
                if (version != CurrentVersion)
                {
                    error = $"Profile version '{version}' is not supported. Expected version {CurrentVersion}.";
                    return false;
                }

                var parsed = new List<ActionItem>();
                foreach (var element in root.Elements("Action"))
                {
                    if (!int.TryParse((string?)element.Attribute("tag"), out int tag))
                    {
                        error = "An <Action> has a missing or invalid tag attribute.";
                        return false;
                    }

                    if (!ActionValueCodec.TryParseKind((string?)element.Attribute("type"), out ActionKind kind))
                    {
                        error = $"Action {tag} has an unsupported type.";
                        return false;
                    }

                    parsed.Add(new ActionItem
                    {
                        Tag = tag,
                        Kind = kind,
                        Label = ValueOf(element, "Label"),
                        Value = ValueOf(element, "Value"),
                        Arguments = ValueOf(element, "Arguments"),
                        Glyph = ValueOf(element, "Glyph"),
                        IconPath = ValueOf(element, "IconPath"),
                        IconScale = DoubleValueOf(element, "IconScale", ActionItem.DefaultIconScale),
                        IconOffsetX = DoubleValueOf(element, "IconOffsetX", 0),
                        IconOffsetY = DoubleValueOf(element, "IconOffsetY", 0),
                        IconTint = BoolValueOf(element, "IconTint", ActionItem.DefaultIconTint),
                        Foreground = ValueOf(element, "Foreground"),
                        Background = ValueOf(element, "Background"),
                        Shadow = ValueOf(element, "Shadow"),
                        ShadowEnabled = BoolValueOf(element, "ShadowEnabled", ActionItem.DefaultShadowEnabled),
                        ShadowOpacity = DoubleValueOf(element, "ShadowOpacity", ActionItem.DefaultShadowOpacity),
                        ShadowBlur = DoubleValueOf(element, "ShadowBlur", ActionItem.DefaultShadowBlur),
                        ShadowOffsetX = DoubleValueOf(element, "ShadowOffsetX", ActionItem.DefaultShadowOffsetX),
                        ShadowOffsetY = DoubleValueOf(element, "ShadowOffsetY", ActionItem.DefaultShadowOffsetY),
                    });
                }

                if (!ActionConfig.TryValidate(parsed, out error))
                    return false;

                actions = parsed.OrderBy(action => action.Tag).ToList();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string ValueOf(XElement parent, string name) =>
            parent.Element(name)?.Value ?? string.Empty;

        private static double DoubleValueOf(XElement parent, string name, double fallback) =>
            double.TryParse(parent.Element(name)?.Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double value) ? value : fallback;

        private static bool BoolValueOf(XElement parent, string name, bool fallback) =>
            bool.TryParse(parent.Element(name)?.Value, out bool value) ? value : fallback;

    }
}
