using System;
using System.IO;

namespace Action_Wheel.Core
{
    /// <summary>Rules about the icon files an action may point at.</summary>
    public static class IconFile
    {
        /// <summary>
        /// Vector: drawn at whatever size the monitor's scale factor asks for, and the only kind
        /// that can be recoloured (see <see cref="ActionItem.IconTint"/>).
        /// </summary>
        public static bool IsVector(string? path) => HasExtension(path, ".svg");

        /// <summary>
        /// Raster. Authored at a fixed resolution, so it softens when the ring is drawn larger than
        /// the file - which is why icons extracted from an application are written at 256px.
        /// </summary>
        public static bool IsRaster(string? path) =>
            HasExtension(path, ".png") || HasExtension(path, ".ico");

        /// <summary>True if the path points at a file this app can actually draw.</summary>
        public static bool IsUsable(string? path) =>
            !string.IsNullOrWhiteSpace(path)
            && (IsVector(path) || IsRaster(path))
            && File.Exists(path);

        /// <summary>The formats accepted, for a message that has to name them.</summary>
        public const string SupportedDescription = ".svg, .png or .ico";

        private static bool HasExtension(string? path, string extension) =>
            !string.IsNullOrWhiteSpace(path)
            && string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);
    }
}
