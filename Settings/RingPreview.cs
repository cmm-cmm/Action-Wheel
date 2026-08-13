using System;
using System.Collections.Generic;
using System.Linq;
using Action_Wheel.Services;
using Action_Wheel.Overlay;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Action_Wheel.Settings
{
    /// <summary>
    /// Draws the ring the way the overlay will, from the rows currently being edited, and plays the
    /// chosen opening animation over it on request.
    /// </summary>
    /// <remarks>
    /// A scaled-down copy of the overlay's layout rather than a shared control: the overlay's buttons
    /// are a WinUI <c>Button</c> with a hand-written template, pointer states and a composition
    /// shadow, none of which a static preview needs. What must stay in step is the geometry (eight
    /// buttons 45 degrees apart, clockwise from the top) and the colours, and the colours come from
    /// <see cref="IconFactory"/> so they cannot drift.
    ///
    /// The animation is the exception: it is not copied here at all. The same
    /// <see cref="RingOpenAnimation"/> that the overlay runs is handed this canvas and these borders,
    /// so what the preview plays is the storyboard itself rather than an imitation of it. A second
    /// implementation would be the one part of the preview that could silently disagree with the
    /// real thing, because there is nothing static to compare against - a wrong colour is visible in
    /// a screenshot, a wrong easing curve is not.
    /// </remarks>
    public static class RingPreview
    {
        /// <summary>
        /// The storyboard currently playing over the preview, and the canvas it is playing on.
        /// </summary>
        /// <remarks>
        /// Static because there is exactly one preview canvas at a time: <c>App.ShowSettings</c>
        /// allows a single settings window. Stopping the previous run is what keeps a burst of
        /// redraws - the user spinning the size box while an animation plays - from leaving the
        /// canvas parked at whatever opacity or rotation the abandoned storyboard had reached.
        /// Stop() restores the base values, which for elements built here are the fully visible ones.
        ///
        /// The canvas is remembered alongside it because a static field outlives the window. Closing
        /// Settings mid-animation and reopening it would otherwise have the first draw of the new
        /// window stop a storyboard belonging to a visual tree that no longer exists - from inside a
        /// Loaded handler, where anything thrown takes the window down with it.
        /// </remarks>
        private static Storyboard? _playing;

        private static Canvas? _playingOn;

        /// <param name="animate">
        /// Replays the opening animation over the ring just drawn. Off for ordinary redraws: the
        /// preview is repainted on every keystroke in a label, and animating those would be a card
        /// that never stops moving while the user types.
        /// </param>
        public static void Render(Canvas canvas, IEnumerable<ActionEditModel> items,
            RingAppearance appearance, Action<ActionEditModel>? selected = null, bool animate = false)
        {
            if (ReferenceEquals(_playingOn, canvas))
                _playing?.Stop();

            _playing = null;
            _playingOn = null;

            canvas.Children.Clear();

            double size = canvas.Width;
            double centre = size / 2.0;

            // At the default 44/88 the ring needs 220 DIP and the canvas has 290, so this is 1 and
            // the preview is the live geometry 1:1 - which is the point, and why it is not scaled
            // down from the overlay's 400 DIP surface. It only drops below 1 at the larger sizes,
            // where the ring genuinely does not fit; without it those simply draw past the card.
            double needed = 2.0 * (appearance.OrbitRadius + appearance.ButtonSize / 2.0);
            double scale = Math.Min(1.0, (size - 8.0) / needed);

            double orbit = appearance.OrbitRadius * scale;
            double outer = appearance.ButtonSize * scale;
            double inner = appearance.CenterButtonSize * scale;
            double iconSize = appearance.IconSize * scale;

            // Tag order, so index 0 is the centre and 1-8 run clockwise - what RingOpenAnimation
            // indexes by. The rows are already sorted that way below; this just keeps the result.
            var hosts = new List<UIElement>(9);

            foreach (var item in items.OrderBy(i => i.Tag))
            {
                double diameter = item.Tag == 0 ? inner : outer;
                double x = centre;
                double y = centre;

                if (item.Tag != 0)
                {
                    // Tag 1 is straight up; each following tag is 45 degrees further clockwise.
                    double radians = (item.Tag - 1) * 45.0 * Math.PI / 180.0;
                    x += orbit * Math.Sin(radians);
                    y -= orbit * Math.Cos(radians);
                }

                var bubble = new Border
                {
                    Width = diameter,
                    Height = diameter,
                    CornerRadius = new CornerRadius(diameter / 2),
                    Background = new SolidColorBrush(item.BackgroundColor),
                    BorderThickness = new Thickness(item.Tag == 0 ? 0 : 2),
                    BorderBrush = new SolidColorBrush(IconFactory.Darken(item.BackgroundColor, 0.10)),
                    Child = IconFactory.CreateIcon(item.ToActionItem(), iconSize,
                        new SolidColorBrush(item.ForegroundColor)),
                };

                ToolTipService.SetToolTip(bubble, $"{item.PositionName} — {item.Label}");

                var shadowHost = new Border { Width = diameter, Height = diameter };
                var host = new Grid { Width = diameter, Height = diameter };
                host.Children.Add(shadowHost);
                host.Children.Add(bubble);

                // A static hint rather than a working simulation of the group actually opening:
                // this preview draws Borders, not the real Buttons RadialMenu.ExpandGroup adds and
                // removes at runtime, so reusing that logic here would mean a second implementation
                // of the same feature rather than a preview of the one that exists. The badge is
                // what tells the user this button is a group at all before they open the real ring.
                if (item.IsGroup)
                {
                    double badgeSize = Math.Max(14, diameter * 0.32);
                    var badge = new Border
                    {
                        Width = badgeSize,
                        Height = badgeSize,
                        CornerRadius = new CornerRadius(badgeSize / 2),
                        Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                        BorderThickness = new Thickness(1.5),
                        BorderBrush = new SolidColorBrush(item.BackgroundColor),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Child = new FontIcon
                        {
                            FontFamily = IconFont.Family,
                            Glyph = "",
                            FontSize = badgeSize * 0.55,
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                    };
                    ToolTipService.SetToolTip(badge, $"Group of {item.GroupChildren.Count} button(s)");
                    host.Children.Add(badge);
                }

                if (item.ShadowEnabled)
                {
                    // Blur and offset scale with everything else, or a shrunken ring keeps a shadow
                    // sized for the full-size one and the preview stops matching the overlay.
                    host.Loaded += (s, e) => ButtonShadow.Apply(shadowHost, diameter, item.ShadowColor,
                        item.ShadowOpacity, item.ShadowBlur * scale,
                        item.ShadowOffsetX * scale, item.ShadowOffsetY * scale);
                }
                host.PointerPressed += (s, e) =>
                {
                    selected?.Invoke(item);
                    e.Handled = true;
                };

                Canvas.SetLeft(host, x - diameter / 2);
                Canvas.SetTop(host, y - diameter / 2);
                canvas.Children.Add(host);
                hosts.Add(host);
            }

            if (!animate || hosts.Count == 0)
                return;

            // The ring as drawn here, not as it will be on screen: everything above is already
            // multiplied by scale, so an animation given the real DIPs would move the buttons by
            // the wrong distance. The surface is the canvas for the same reason.
            var metrics = new RingMetrics(orbit, outer, size, appearance.AnimationDurationMs / RingOpenAnimation.BudgetMs);

            var storyboard = RingOpenAnimation.Build(appearance.Animation, canvas, hosts, metrics);
            if (storyboard == null)
                return;

            _playing = storyboard;
            _playingOn = canvas;
            storyboard.Completed += (s, e) =>
            {
                if (!ReferenceEquals(_playing, storyboard))
                    return;

                _playing = null;
                _playingOn = null;
            };
            storyboard.Begin();
        }
    }
}
