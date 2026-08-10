using System;
using System.Numerics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Action_Wheel.Services;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace Action_Wheel.Overlay
{
    /// <summary>
    /// Puts a soft circular drop shadow behind a radial-menu button.
    /// </summary>
    /// <remarks>
    /// Done with a composition <c>DropShadow</c> rather than XAML's <c>ThemeShadow</c>: ThemeShadow
    /// is cast onto shadow *receivers* elsewhere in the tree, and the overlay is fully transparent -
    /// there is nothing behind the buttons to receive anything, so it would render nothing at all.
    ///
    /// A DropShadow with no mask takes the shape of its visual, which would be a square shadow under
    /// a round button. The mask here is a black circle rendered into a <c>CompositionVisualSurface</c>,
    /// which gives the shadow the button's actual silhouette.
    /// </remarks>
    internal static class ButtonShadow
    {
        // All buttons in a window share one compositor. The circular mask depends only on diameter,
        // so rebuilding nine geometry/surface pipelines every time the ring opens is wasted GPU and
        // UI-thread work. Two cached masks cover the centre and outer button sizes.
        private static readonly ConditionalWeakTable<Compositor, Dictionary<float, CompositionBrush>> Masks = new();

        /// <summary>
        /// Attaches the shadow to <paramref name="host"/> - an empty element sitting behind the
        /// button's filled border, so the shadow is drawn underneath it.
        /// </summary>
        public static void Apply(FrameworkElement host, double diameter, Windows.UI.Color color,
            double opacity = 0.45, double blur = 11, double offsetX = 0, double offsetY = 3)
        {
            try
            {
                var compositor = ElementCompositionPreview.GetElementVisual(host).Compositor;
                float size = (float)diameter;

                var shadow = compositor.CreateDropShadow();
                shadow.Mask = GetMask(compositor, size);
                // Tuned against a plain white window: at 0.32/9 the halo measured only ~9% darker
                // than the background, which is not the "clearly separated" the buttons need.
                shadow.Color = color;
                shadow.BlurRadius = (float)blur;
                shadow.Opacity = (float)opacity;
                shadow.Offset = new Vector3((float)offsetX, (float)offsetY, 0f);

                var sprite = compositor.CreateSpriteVisual();
                sprite.Size = new Vector2(size);
                sprite.Shadow = shadow;

                ElementCompositionPreview.SetElementChildVisual(host, sprite);
            }
            catch (Exception)
            {
                // Purely decorative - never let it stop the menu from opening.
            }
        }

        private static CompositionBrush GetMask(Compositor compositor, float size)
        {
            var masks = Masks.GetOrCreateValue(compositor);
            if (masks.TryGetValue(size, out var cached))
                return cached;

            var circle = compositor.CreateEllipseGeometry();
            circle.Center = new Vector2(size / 2f);
            circle.Radius = new Vector2(size / 2f);
            var shape = compositor.CreateSpriteShape(circle);
            shape.FillBrush = compositor.CreateColorBrush(Colors.Black);
            var visual = compositor.CreateShapeVisual();
            visual.Size = new Vector2(size);
            visual.Shapes.Add(shape);
            var surface = compositor.CreateVisualSurface();
            surface.SourceVisual = visual;
            surface.SourceSize = new Vector2(size);
            var brush = compositor.CreateSurfaceBrush(surface);
            masks[size] = brush;
            return brush;
        }

        /// <summary>
        /// Finds a named element inside an applied ControlTemplate. The buttons are plain
        /// <c>Button</c>s styled from XAML, not a custom control, so there is no GetTemplateChild.
        /// </summary>
        public static FrameworkElement? FindTemplatePart(DependencyObject root, string name)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);

            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is FrameworkElement element && element.Name == name)
                    return element;

                var found = FindTemplatePart(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
