using System;
using System.Diagnostics;
using System.IO;
using Windows.ApplicationModel.DataTransfer;

namespace Action_Wheel.Services
{
    /// <summary>
    /// The two ways this app hands something to the rest of Windows: opening a folder in Explorer
    /// and putting text on the clipboard. Wrapped so the view models can call them without a
    /// try/catch each and without a reference to Process or the clipboard API.
    /// </summary>
    public static class ShellCommands
    {
        /// <summary>Opens a file, URI or other shell target with its registered application.</summary>
        public static bool OpenPath(string path, out string error)
        {
            error = string.Empty;

            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Opens a folder in Explorer, creating it first if it does not exist yet.</summary>
        public static bool OpenFolder(string path, out string error)
        {
            error = string.Empty;

            try
            {
                Directory.CreateDirectory(path);

                return OpenPath(path, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool CopyText(string text, out string error)
        {
            error = string.Empty;

            try
            {
                var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                package.SetText(text);
                Clipboard.SetContent(package);

                // Without Flush the content is dropped when this process exits, which for an app
                // people quit from the tray straight after copying is most of the time.
                Clipboard.Flush();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
