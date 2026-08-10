using System;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Action_Wheel.Services
{
    public static class WindowSizing
    {
        public static void SizeAndCentre(Window window, FrameworkElement content,
            double widthDip, double heightDip, string context)
        {
            try
            {
                double scale = content.XamlRoot?.RasterizationScale ?? 1.0;
                int width = (int)Math.Round(widthDip * scale);
                int height = (int)Math.Round(heightDip * scale);
                var display = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Nearest);
                if (display != null)
                {
                    width = Math.Min(width, display.WorkArea.Width);
                    height = Math.Min(height, display.WorkArea.Height);
                }

                window.AppWindow.Resize(new SizeInt32(width, height));
                if (display != null)
                    window.AppWindow.Move(new PointInt32(
                        display.WorkArea.X + Math.Max(0, (display.WorkArea.Width - width) / 2),
                        display.WorkArea.Y + Math.Max(0, (display.WorkArea.Height - height) / 2)));
            }
            catch (Exception) { }
        }
    }
}
