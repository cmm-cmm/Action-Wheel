using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace Action_Wheel.Services
{
    /// <summary>
    /// Copies the icon Windows shows for a program or file into a PNG the ring can draw.
    /// </summary>
    /// <remarks>
    /// The image comes from the shell rather than from the executable's resources, so a .lnk
    /// resolves to its target's icon, a folder gets the folder icon, and a document gets the icon of
    /// whichever application is registered for it - none of which ExtractIconEx on the path gives.
    ///
    /// It goes through the system image list rather than IShellItemImageFactory, which reads better
    /// but returns E_PENDING on every call here - measured, on both an STA and an MTA thread, and it
    /// never resolves without a message pump the caller does not have. The image list is synchronous
    /// and has no such condition.
    ///
    /// The result is written to disk instead of being held in memory because actions.json has to
    /// survive a restart, and it stores an icon as a path.
    /// </remarks>
    public static class AppIconExtractor
    {
        private sealed record PixelReadResult(
            bool Success, string SourcePath, byte[] Pixels, int Width, int Height, string Error);

        /// <summary>
        /// Writes the icon for <paramref name="target"/> into the icons folder and returns its path.
        /// </summary>
        public static async Task<(bool Success, string IconPath, string Error)> TryExtractAsync(string target)
        {
            try
            {
                // Resolution can touch PATH, a network location and third-party shell extensions.
                // Keep all of that synchronous Shell/GDI work off the WinUI thread.
                var read = await Task.Run(() => ReadPixels(target));
                if (!read.Success)
                    return (false, string.Empty, read.Error);

                string destination = DestinationFor(read.SourcePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                using (var file = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var encoder = await BitmapEncoder.CreateAsync(
                        BitmapEncoder.PngEncoderId, file.AsRandomAccessStream());

                    // Straight, not premultiplied. Confirmed by measurement: icons come back with
                    // colour channels above their own alpha, which premultiplied data cannot have.
                    // Naming the wrong mode here washes out every anti-aliased edge.
                    encoder.SetPixelData(
                        BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
                        (uint)read.Width, (uint)read.Height, 96, 96, read.Pixels);

                    await encoder.FlushAsync();
                }

                return (true, destination, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, ex.Message);
            }
        }

        private static PixelReadResult ReadPixels(string target)
        {
            string path = Normalise(target);
            if (path.Length == 0)
                return Failed("There is no program or file to take an icon from.");

            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile)
                return Failed("A web address has no icon to copy.");

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                // A bare command such as "notepad.exe" is a legitimate launch target; it just has
                // to be resolved to a real file before the shell can be asked about it.
                string? resolved = ResolveOnPath(path);
                if (resolved == null)
                    return Failed($"'{path}' was not found, so its icon could not be read.");
                path = resolved;
            }

            return TryGetPixels(path, out var pixels, out int width, out int height)
                ? new PixelReadResult(true, path, pixels, width, height, string.Empty)
                : Failed("Windows did not return an icon for this target.");
        }

        private static PixelReadResult Failed(string error) =>
            new(false, string.Empty, Array.Empty<byte>(), 0, 0, error);

        /// <summary>
        /// Removes extracted PNGs that neither actions.json nor any readable saved profile uses.
        /// Intended for startup, when no icon extraction can be in flight.
        /// </summary>
        public static Task CleanupUnusedAsync() => Task.Run(CleanupUnused);

        private static void CleanupUnused()
        {
            try
            {
                string directory = AppDataPaths.IconsDirectoryPath;
                if (!Directory.Exists(directory))
                    return;

                var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var config = ActionConfig.LoadDetailed();
                if (config.Status == ConfigStatus.Rejected)
                    return;
                AddReferences(config.Actions, referenced);

                var profiles = new ProfileLibrary();
                if (!profiles.TryList(out var names, out _))
                    return;

                // Be conservative: one unreadable profile may be the only owner of an icon.
                foreach (string name in names)
                {
                    if (!profiles.TryLoad(name, out var actions, out _))
                        return;
                    AddReferences(actions, referenced);
                }

                foreach (string path in Directory.EnumerateFiles(directory, "*.png"))
                {
                    if (IsExtractedIconName(path) && !referenced.Contains(Path.GetFullPath(path)))
                    {
                        try { File.Delete(path); }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
            }
            catch (Exception)
            {
                // Maintenance is best-effort and must never prevent startup.
            }
        }

        private static void AddReferences(IEnumerable<ActionItem> actions, HashSet<string> referenced)
        {
            foreach (var action in actions)
            {
                if (string.IsNullOrWhiteSpace(action.IconPath))
                    continue;
                try { referenced.Add(Path.GetFullPath(action.IconPath)); }
                catch (Exception) { }
            }
        }

        private static bool IsExtractedIconName(string path)
        {
            string stem = Path.GetFileNameWithoutExtension(path);
            int dash = stem.LastIndexOf('-');
            if (dash < 0 || stem.Length - dash - 1 != 8)
                return false;

            return stem[(dash + 1)..].All(Uri.IsHexDigit);
        }

        /// <summary>Strips the quoting a launch target may carry, and expands %VARIABLES%.</summary>
        private static string Normalise(string target)
        {
            var trimmed = (target ?? string.Empty).Trim().Trim('"');
            return trimmed.Length == 0 ? string.Empty : Environment.ExpandEnvironmentVariables(trimmed);
        }

        private static string? ResolveOnPath(string command)
        {
            if (Path.IsPathRooted(command) || command.Contains(Path.DirectorySeparatorChar))
                return null;

            var directories = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(directories))
                return null;

            var extensions = string.IsNullOrEmpty(Path.GetExtension(command))
                ? new[] { ".exe", ".com", ".bat", ".cmd" }
                : new[] { string.Empty };

            foreach (var directory in directories.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var extension in extensions)
                {
                    try
                    {
                        string candidate = Path.Combine(directory.Trim(), command + extension);
                        if (File.Exists(candidate))
                            return candidate;
                    }
                    catch (ArgumentException)
                    {
                        // A malformed PATH entry - skip it rather than give up on the rest.
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// A stable, readable file name. The hash keeps two programs with the same file name - two
        /// copies of code.exe, say - from overwriting each other's icon, while re-picking the same
        /// program reuses the same file rather than filling the folder with near-duplicates.
        /// </summary>
        private static string DestinationFor(string sourcePath)
        {
            string name = Path.GetFileNameWithoutExtension(sourcePath);
            var safe = new StringBuilder(name.Length);
            foreach (char c in name)
                safe.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);

            uint hash = 2166136261;
            foreach (char c in sourcePath.ToLowerInvariant())
                hash = (hash ^ c) * 16777619;

            string stem = safe.Length == 0 ? "icon" : safe.ToString();
            return Path.Combine(AppDataPaths.IconsDirectoryPath, $"{stem}-{hash:x8}.png");
        }

        #region Shell and GDI

        private static bool TryGetPixels(string path, out byte[] pixels, out int width, out int height)
        {
            pixels = Array.Empty<byte>();
            width = height = 0;

            var info = new SHFILEINFO();
            if (SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_SYSICONINDEX) == IntPtr.Zero)
                return false;

            // Largest first. A program that only ships a 32px icon still comes back at the requested
            // size, scaled by the shell, which is better than the ring scaling it later.
            foreach (int listSize in new[] { SHIL_JUMBO, SHIL_EXTRALARGE, SHIL_LARGE })
            {
                if (TryGetIcon(listSize, info.iIcon, out IntPtr icon))
                {
                    try
                    {
                        if (TryReadIcon(icon, out pixels, out width, out height))
                            return true;
                    }
                    finally
                    {
                        DestroyIcon(icon);
                    }
                }
            }

            return false;
        }

        private static bool TryGetIcon(int listSize, int index, out IntPtr icon)
        {
            icon = IntPtr.Zero;
            var imageListId = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

            if (SHGetImageList(listSize, ref imageListId, out var list) != 0 || list == null)
                return false;

            try
            {
                return list.GetIcon(index, ILD_TRANSPARENT, out icon) == 0 && icon != IntPtr.Zero;
            }
            finally
            {
                Marshal.ReleaseComObject(list);
            }
        }

        private static bool TryReadIcon(IntPtr icon, out byte[] pixels, out int width, out int height)
        {
            pixels = Array.Empty<byte>();
            width = height = 0;

            if (!GetIconInfo(icon, out var iconInfo))
                return false;

            try
            {
                var bitmap = new BITMAP();
                if (GetObject(iconInfo.hbmColor, Marshal.SizeOf<BITMAP>(), ref bitmap) == 0
                    || bitmap.bmWidth <= 0 || bitmap.bmHeight <= 0)
                {
                    return false;
                }

                width = bitmap.bmWidth;
                height = bitmap.bmHeight;

                if (!TryReadBgra(iconInfo.hbmColor, width, height, out var colour))
                    return false;

                if (!HasAnyAlpha(colour))
                    ApplyMaskAsAlpha(iconInfo.hbmMask, colour, width, height);

                pixels = CropToArtwork(colour, ref width, ref height);
                return true;
            }
            finally
            {
                if (iconInfo.hbmColor != IntPtr.Zero) DeleteObject(iconInfo.hbmColor);
                if (iconInfo.hbmMask != IntPtr.Zero) DeleteObject(iconInfo.hbmMask);
            }
        }

        private static bool TryReadBgra(IntPtr bitmap, int width, int height, out byte[] buffer)
        {
            buffer = new byte[width * height * 4];

            IntPtr screen = GetDC(IntPtr.Zero);
            if (screen == IntPtr.Zero)
                return false;

            try
            {
                // A negative height asks for the rows top-down, which is the order the PNG encoder
                // wants. Without it the icon comes out vertically mirrored.
                var header = new BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BI_RGB,
                };

                return GetDIBits(screen, bitmap, 0, (uint)height, buffer, ref header, DIB_RGB_COLORS) != 0;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screen);
            }
        }

        /// <summary>
        /// Trims the fully transparent border, so the saved PNG is artwork edge to edge.
        /// </summary>
        /// <remarks>
        /// How much padding an icon carries is up to whoever drew it - Windows' own icons use very
        /// little, plenty of third-party ones use a lot - and the ring lays an image out by its
        /// canvas, not by what is drawn on it. Left alone, the same button would come out a
        /// different size for every application, and consistently smaller than a glyph, which is
        /// measured from its ink. Cropping here makes the stored file mean one thing, so the drawing
        /// code does not have to guess.
        /// </remarks>
        private static byte[] CropToArtwork(byte[] bgra, ref int width, ref int height)
        {
            // Not zero: anti-aliased edges fade to almost nothing, and a stray pixel at alpha 1
            // would defeat the crop entirely.
            const byte Threshold = 8;

            int left = width, right = -1, top = height, bottom = -1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (bgra[(y * width + x) * 4 + 3] < Threshold)
                        continue;

                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            }

            if (right < left || bottom < top)
                return bgra;

            int newWidth = right - left + 1;
            int newHeight = bottom - top + 1;
            if (newWidth == width && newHeight == height)
                return bgra;

            var cropped = new byte[newWidth * newHeight * 4];
            for (int y = 0; y < newHeight; y++)
            {
                Buffer.BlockCopy(
                    bgra, ((y + top) * width + left) * 4,
                    cropped, y * newWidth * 4,
                    newWidth * 4);
            }

            width = newWidth;
            height = newHeight;
            return cropped;
        }

        private static bool HasAnyAlpha(byte[] bgra)
        {
            for (int i = 3; i < bgra.Length; i += 4)
            {
                if (bgra[i] != 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Rebuilds transparency for an icon whose colour bitmap carries no alpha - the pre-XP
        /// format, still used by a few installers. Without this such an icon saves as a fully
        /// transparent PNG, which on a button is indistinguishable from the feature not working.
        /// </summary>
        private static void ApplyMaskAsAlpha(IntPtr mask, byte[] bgra, int width, int height)
        {
            if (mask == IntPtr.Zero || !TryReadBgra(mask, width, height, out var maskPixels))
            {
                for (int i = 3; i < bgra.Length; i += 4)
                    bgra[i] = 255;
                return;
            }

            // In an icon mask a white pixel means "let the background through".
            for (int i = 0; i < bgra.Length; i += 4)
                bgra[i + 3] = maskPixels[i] != 0 ? (byte)0 : (byte)255;
        }

        private const uint SHGFI_SYSICONINDEX = 0x4000;
        private const int SHIL_LARGE = 0;
        private const int SHIL_EXTRALARGE = 2;
        private const int SHIL_JUMBO = 4;
        private const int ILD_TRANSPARENT = 1;
        private const int BI_RGB = 0;
        private const uint DIB_RGB_COLORS = 0;

        [ComImport]
        [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IImageList
        {
            // Only GetIcon is called, but every earlier slot has to be declared so the vtable
            // offsets line up.
            [PreserveSig] int Add(IntPtr image, IntPtr mask, ref int index);
            [PreserveSig] int ReplaceIcon(int index, IntPtr icon, ref int newIndex);
            [PreserveSig] int SetOverlayImage(int image, int overlay);
            [PreserveSig] int Replace(int index, IntPtr image, IntPtr mask);
            [PreserveSig] int AddMasked(IntPtr image, int mask, ref int index);
            [PreserveSig] int Draw(IntPtr drawParameters);
            [PreserveSig] int Remove(int index);
            [PreserveSig] int GetIcon(int index, int flags, out IntPtr icon);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string path, uint fileAttributes,
            ref SHFILEINFO info, uint size, uint flags);

        [DllImport("shell32.dll")]
        private static extern int SHGetImageList(int imageList, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IImageList list);

        [DllImport("user32.dll")]
        private static extern bool GetIconInfo(IntPtr icon, out ICONINFO info);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr icon);

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr handle, int size, ref BITMAP target);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint startScan, uint scanLines,
            byte[] bits, ref BITMAPINFOHEADER info, uint usage);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr dc);

        #endregion
    }
}
