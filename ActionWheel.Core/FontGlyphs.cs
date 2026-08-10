using System;
using System.Collections.Generic;
using System.IO;

namespace Action_Wheel.Core
{
    /// <summary>
    /// Reports which characters a font file can draw, by reading its character map directly.
    /// </summary>
    /// <remarks>
    /// The obvious alternative, GDI's <c>GetFontUnicodeRanges</c>, has two disqualifying problems
    /// here. It only sees fonts the system has installed, and this app embeds its icon font rather
    /// than installing it. And it reports ranges as 16-bit values, so anything
    /// above U+FFFF comes back as surrogate halves - which would hide the 6,896 icons this font
    /// keeps in plane 15, two thirds of everything it has.
    ///
    /// Only the parts of the format needed to answer that question are parsed: the table directory,
    /// then the character map in either of the two encodings a modern font actually uses. Everything
    /// is big-endian, which is why nothing here uses BitConverter.
    /// </remarks>
    public static class FontGlyphs
    {
        /// <summary>
        /// Every character the font at <paramref name="path"/> can draw, ascending and distinct.
        /// Returns an empty list rather than throwing: a missing or malformed font is a condition to
        /// report in the picker, not a reason to bring the settings window down.
        /// </summary>
        public static IReadOnlyList<int> Read(string path)
        {
            try
            {
                return Parse(File.ReadAllBytes(path));
            }
            catch (Exception)
            {
                return Array.Empty<int>();
            }
        }

        /// <summary>Reads a font supplied as a resource stream, including from a single-file app.</summary>
        public static IReadOnlyList<int> Read(Stream stream)
        {
            try
            {
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return Parse(buffer.ToArray());
            }
            catch (Exception)
            {
                return Array.Empty<int>();
            }
        }

        private static IReadOnlyList<int> Parse(byte[] font)
        {
            if (!TryFindTable(font, "cmap", out int cmap))
                return Array.Empty<int>();

            if (!TryFindBestSubtable(font, cmap, out int subtable))
                return Array.Empty<int>();

            int format = ReadUInt16(font, subtable);
            var codePoints = format switch
            {
                4 => ReadFormat4(font, subtable),
                12 => ReadFormat12(font, subtable),
                _ => new List<int>(),
            };

            codePoints.Sort();
            return codePoints;
        }

        private static bool TryFindTable(byte[] font, string tag, out int offset)
        {
            offset = 0;
            if (font.Length < 12)
                return false;

            int count = ReadUInt16(font, 4);
            for (int i = 0; i < count; i++)
            {
                int record = 12 + i * 16;
                if (record + 16 > font.Length)
                    return false;

                if (font[record] == tag[0] && font[record + 1] == tag[1]
                    && font[record + 2] == tag[2] && font[record + 3] == tag[3])
                {
                    offset = (int)ReadUInt32(font, record + 8);
                    return offset > 0 && offset < font.Length;
                }
            }

            return false;
        }

        /// <summary>
        /// Picks the character map to read. Format 12 is preferred wherever the font offers it,
        /// because it is the only one that can express a character above U+FFFF.
        /// </summary>
        private static bool TryFindBestSubtable(byte[] font, int cmap, out int subtable)
        {
            subtable = 0;
            int best = -1;
            int count = ReadUInt16(font, cmap + 2);

            for (int i = 0; i < count; i++)
            {
                int record = cmap + 4 + i * 8;
                if (record + 8 > font.Length)
                    break;

                int platform = ReadUInt16(font, record);
                int encoding = ReadUInt16(font, record + 2);
                int offset = cmap + (int)ReadUInt32(font, record + 4);
                if (offset < 0 || offset + 2 > font.Length)
                    continue;

                int format = ReadUInt16(font, offset);

                // Windows full Unicode, Windows BMP, then the Unicode platform's equivalents.
                int rank = (platform, encoding, format) switch
                {
                    (3, 10, 12) => 5,
                    (0, _, 12) => 4,
                    (3, 1, 4) => 3,
                    (0, _, 4) => 2,
                    _ => 0,
                };

                if (rank > best)
                {
                    best = rank;
                    subtable = offset;
                }
            }

            return best > 0;
        }

        private static List<int> ReadFormat4(byte[] font, int start)
        {
            var result = new List<int>();
            int segCountX2 = ReadUInt16(font, start + 6);
            int segCount = segCountX2 / 2;

            int endCodes = start + 14;
            int startCodes = endCodes + segCountX2 + 2;   // +2 for the reserved padding
            int idDeltas = startCodes + segCountX2;
            int idRangeOffsets = idDeltas + segCountX2;

            for (int s = 0; s < segCount; s++)
            {
                int end = ReadUInt16(font, endCodes + s * 2);
                int first = ReadUInt16(font, startCodes + s * 2);
                if (first > end || first == 0xFFFF)
                    continue;

                int delta = ReadUInt16(font, idDeltas + s * 2);
                int rangeOffsetAt = idRangeOffsets + s * 2;
                int rangeOffset = ReadUInt16(font, rangeOffsetAt);

                for (int c = first; c <= end && c != 0xFFFF; c++)
                {
                    int glyph;
                    if (rangeOffset == 0)
                    {
                        glyph = (c + delta) & 0xFFFF;
                    }
                    else
                    {
                        int at = rangeOffsetAt + rangeOffset + (c - first) * 2;
                        if (at + 2 > font.Length)
                            continue;

                        glyph = ReadUInt16(font, at);
                        if (glyph != 0)
                            glyph = (glyph + delta) & 0xFFFF;
                    }

                    // Glyph 0 is ".notdef" - the box drawn for a character the font does not have.
                    if (glyph != 0)
                        result.Add(c);
                }
            }

            return result;
        }

        private static List<int> ReadFormat12(byte[] font, int start)
        {
            var result = new List<int>();
            long groups = ReadUInt32(font, start + 12);

            for (long g = 0; g < groups; g++)
            {
                int record = start + 16 + (int)(g * 12);
                if (record + 12 > font.Length)
                    break;

                long first = ReadUInt32(font, record);
                long end = ReadUInt32(font, record + 4);
                long firstGlyph = ReadUInt32(font, record + 8);

                if (first > end || end > 0x10FFFF)
                    continue;

                // A group spanning the whole space would be a malformed font, and expanding it would
                // allocate until the machine gave up.
                if (end - first > 0x20000)
                    continue;

                for (long c = first; c <= end; c++)
                {
                    if (firstGlyph + (c - first) != 0)
                        result.Add((int)c);
                }
            }

            return result;
        }

        private static int ReadUInt16(byte[] data, int offset) =>
            (data[offset] << 8) | data[offset + 1];

        private static uint ReadUInt32(byte[] data, int offset) =>
            ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16)
            | ((uint)data[offset + 2] << 8) | data[offset + 3];
    }
}
