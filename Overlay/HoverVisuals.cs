using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace Action_Wheel.Overlay
{
    /// <summary>A shadow's look, for one state of a button.</summary>
    internal readonly record struct ShadowLook(Color Color, float Opacity, float Blur, Vector2 Offset);

    /// <summary>
    /// The growth and shadow half of a ring button's hover effect (<see cref="RingHoverEffect"/>).
    /// Shared by the overlay's buttons, a group's satellites and the settings preview, so the three
    /// cannot drift apart. Colours are not here: the main ring swaps them, satellites keep their
    /// parent's, and the preview draws Borders, so each caller paints its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Growth.</b> Uses the element's own Scale facade, with a ScaleTransition when the user asked
    /// for a smooth change. It used to be a ScaleTransform set by the button template's
    /// VisualStates. That was instant by construction, and it only ever fired for a real pointer, so
    /// the drag gesture's highlight never grew. The facade animates on the compositor, off the UI
    /// thread. It must be on an element nothing calls ElementCompositionPreview.GetElementVisual on:
    /// the template root here, never the ShadowHost that carries the shadow sprite.
    /// </para>
    /// <para>
    /// <b>Shadow.</b> Changes the DropShadow made by <see cref="ButtonShadow.Apply"/> in place, with
    /// implicit animations for the same smooth duration. Glow and Lift need a shadow even on a button
    /// whose own shadow is switched off; the caller then makes one with a rest opacity of 0, so the
    /// button looks shadowless until hovered.
    /// </para>
    /// </remarks>
    internal sealed class HoverVisuals
    {
        /// <summary>Pressing always shrinks a little, whatever the hover effect; it is the click feedback.</summary>
        private const float PressedScale = 0.94f;

        private readonly UIElement _face;
        private readonly RingAppearance _appearance;
        private DropShadow? _shadow;
        private ShadowLook _rest;
        private Color _glow;

        /// <param name="face">What grows: the button's template root, or the preview's bubble.</param>
        public HoverVisuals(UIElement face, double diameter, RingAppearance appearance)
        {
            _face = face;
            _appearance = appearance;

            float centre = (float)(diameter / 2.0);
            face.CenterPoint = new Vector3(centre, centre, 0f);
            if (appearance.HoverAnimationMs > 0)
                face.ScaleTransition = new Vector3Transition { Duration = TimeSpan.FromMilliseconds(appearance.HoverAnimationMs) };
        }

        /// <summary>Whether this effect needs a shadow on every button, including ones whose own is off.</summary>
        public static bool NeedsShadow(RingAppearance appearance) =>
            appearance.HoverEffect is RingHoverEffect.Glow or RingHoverEffect.Lift;

        /// <summary>Hands over the button's shadow and what it looks like at rest. The glow takes <paramref name="glow"/>.</summary>
        public void AttachShadow(DropShadow? shadow, ShadowLook rest, Color glow)
        {
            _shadow = shadow;
            _rest = rest;
            _glow = glow;

            if (shadow != null && _appearance.HoverAnimationMs > 0)
                shadow.ImplicitAnimations = ImplicitShadowAnimations(shadow.Compositor, _appearance.HoverAnimationMs);
        }

        public void Rest()
        {
            SetScale(1f);
            SetShadow(_rest);
        }

        /// <param name="grow">False for the drag gesture with <see cref="RingAppearance.HoverOnGesture"/> off.</param>
        public void Hover(bool grow = true)
        {
            SetScale(grow ? (float)_appearance.HoverScale : 1f);
            SetShadow(HoverShadow());
        }

        public void Pressed()
        {
            SetScale(PressedScale);
            SetShadow(_rest);
        }

        /// <summary>
        /// The glow is centred, wide and strong, in the button's own colour. A lift keeps the
        /// shadow's colour but deepens it and drops it further. Both start from at least the default
        /// shadow (0.45, blur 11, 3 down), so a button whose shadow is off still lifts visibly.
        /// </summary>
        private ShadowLook HoverShadow() => _appearance.HoverEffect switch
        {
            RingHoverEffect.Glow => new ShadowLook(_glow, 0.85f, Math.Max(18f, _rest.Blur + 6f), Vector2.Zero),
            RingHoverEffect.Lift => new ShadowLook(_rest.Color,
                Math.Min(0.9f, Math.Max(_rest.Opacity, 0.45f) + 0.2f),
                Math.Max(_rest.Blur, 11f) + 9f,
                new Vector2(_rest.Offset.X, Math.Max(_rest.Offset.Y, 3f) + 5f)),
            _ => _rest,
        };

        private void SetScale(float scale)
        {
            try
            {
                _face.Scale = new Vector3(scale, scale, 1f);
            }
            catch (Exception)
            {
                // Purely visual. A teardown race must not break the click that caused it.
            }
        }

        private void SetShadow(ShadowLook look)
        {
            if (_shadow == null)
                return;

            try
            {
                _shadow.Color = look.Color;
                _shadow.Opacity = look.Opacity;
                _shadow.BlurRadius = look.Blur;
                _shadow.Offset = new Vector3(look.Offset, 0f);
            }
            catch (Exception)
            {
                // The compositor may already be gone with a closing window.
            }
        }

        private static ImplicitAnimationCollection ImplicitShadowAnimations(Compositor compositor, double milliseconds)
        {
            var duration = TimeSpan.FromMilliseconds(milliseconds);
            var animations = compositor.CreateImplicitAnimationCollection();

            foreach (string scalar in new[] { nameof(DropShadow.Opacity), nameof(DropShadow.BlurRadius) })
            {
                var animation = compositor.CreateScalarKeyFrameAnimation();
                animation.InsertExpressionKeyFrame(1f, "this.FinalValue");
                animation.Duration = duration;
                animation.Target = scalar;
                animations[scalar] = animation;
            }

            var offset = compositor.CreateVector3KeyFrameAnimation();
            offset.InsertExpressionKeyFrame(1f, "this.FinalValue");
            offset.Duration = duration;
            offset.Target = nameof(DropShadow.Offset);
            animations[nameof(DropShadow.Offset)] = offset;

            var color = compositor.CreateColorKeyFrameAnimation();
            color.InsertExpressionKeyFrame(1f, "this.FinalValue");
            color.Duration = duration;
            color.Target = nameof(DropShadow.Color);
            animations[nameof(DropShadow.Color)] = color;

            return animations;
        }
    }
}
