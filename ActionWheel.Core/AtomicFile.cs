using System;
using System.IO;
using System.Text;

namespace Action_Wheel.Core
{
    /// <summary>Writes a small configuration file without exposing a partially written result.</summary>
    public static class AtomicFile
    {
        public static bool TryWriteText(string path, string content, out string error)
        {
            error = string.Empty;
            string? directory = Path.GetDirectoryName(path);

            // GUID-suffixed, not a fixed ".tmp" - see ActionConfig.SaveTo, which does the same
            // atomic-write job for actions.json. A fixed name lets two overlapping saves to the same
            // path (a settings save racing a preferences reload, say) collide on the same temp file.
            string temporary = $"{path}.{Guid.NewGuid():N}.tmp";

            try
            {
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(content);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(path))
                    File.Copy(path, path + ".bak", overwrite: true);
                File.Move(temporary, path, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                try { File.Delete(temporary); } catch { }
                return false;
            }
        }
    }
}
