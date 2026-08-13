using System;

namespace Action_Wheel.Core
{
    /// <summary>How the ring animates itself into view.</summary>
    /// <remarks>
    /// The numbers are written to preferences.json, so they are fixed: inserting a value in the
    /// middle would silently change what every existing installation is set to. Append only.
    /// </remarks>
    public enum RingAnimation
    {
        /// <summary>No animation. Every button is at full opacity on the first frame.</summary>
        None = 0,

        /// <summary>The original: the ring fades and scales up while the buttons stagger in.</summary>
        Fade = 1,

        /// <summary>Each button springs up from a fraction of its size, slightly overshooting.</summary>
        Pop = 2,

        /// <summary>The buttons start stacked on the centre and fly out to their orbit.</summary>
        Radiate = 3,

        /// <summary>The ring sweeps one button-slot clockwise into place.</summary>
        Sweep = 4,

        /// <summary>The buttons start outside the ring and fly inward to close around the centre.</summary>
        Converge = 5,

        /// <summary>As <see cref="Sweep"/>, anticlockwise.</summary>
        CounterSweep = 6,
    }

    /// <summary>
    /// The parts of the ring's look that the user controls but that are not per-button: the opening
    /// animation and the two numbers that decide how big the ring is.
    /// </summary>
    /// <remarks>
    /// Deliberately not part of <c>actions.json</c>. Those are the nine buttons and travel between
    /// machines as a profile; this is one person's preference about the pointer under their own hand,
    /// and it belongs with the trigger settings in preferences.json.
    /// </remarks>
    public sealed record RingAppearance
    {
        public static readonly RingAppearance Default = new();

        public RingAnimation Animation { get; init; } = RingAnimation.Fade;

        /// <summary>
        /// How long the opening animation takes, as a percentage of its tuned timings: 100 is every
        /// begin/duration value exactly as <see cref="RingOpenAnimation"/> defines it - the fastest
        /// setting available - and anything above that plays slower.
        /// </summary>
        /// <remarks>
        /// A percentage of the existing begin/duration values rather than a free millisecond figure,
        /// because the presets are not one number each - Fade alone staggers nine begin times against
        /// each other - and a scalar is the only edit that moves all of them together without
        /// disturbing the ratios <see cref="RingOpenAnimation"/>'s remarks describe as "fade fast,
        /// move slowly."
        /// </remarks>
        public double AnimationDurationPercent { get; init; } = 100.0;

        /// <summary>
        /// The floor is the tuned speed itself, not a way to play it faster - the presets are tuned
        /// to fit inside <see cref="RingOpenAnimation.BudgetMs"/> already, so there is no faster
        /// setting to offer.
        /// </summary>
        public const double MinAnimationDurationPercent = 100.0;

        /// <summary>How much slower than tuned the user is allowed to make the opening animation.</summary>
        public const double MaxAnimationDurationPercent = 500.0;

        /// <summary>Diameter of one of the eight outer buttons, in DIPs.</summary>
        public double ButtonSize { get; init; } = RingGeometry.OuterButtonSizeDip;

        /// <summary>Distance from the ring's centre to each outer button's centre, in DIPs.</summary>
        public double OrbitRadius { get; init; } = RingGeometry.OrbitRadiusDip;

        /// <summary>Diameter of the centre button - a fixed fraction of an outer one.</summary>
        public double CenterButtonSize => Math.Round(ButtonSize * RingGeometry.CenterButtonRatio);

        /// <summary>
        /// Glyph size for a button of this diameter. Scaled rather than fixed: a 14 DIP glyph that
        /// looks right inside a 44 DIP button is lost inside a 72 DIP one.
        /// </summary>
        public double IconSize => Math.Round(ButtonSize * RingGeometry.IconSizeRatio);

        /// <summary>Side of the square overlay window this ring needs, in DIPs.</summary>
        public int MenuSize => RingGeometry.MenuSizeFor(ButtonSize, OrbitRadius);

        /// <summary>
        /// The same appearance with both numbers brought inside their legal range. The orbit is
        /// clamped <em>after</em> the button size, because its limits are derived from it - clamping
        /// in the other order can leave a pair that individually pass and together overlap.
        /// </summary>
        public RingAppearance Normalised()
        {
            double button = Math.Clamp(
                double.IsFinite(ButtonSize) ? Math.Round(ButtonSize) : RingGeometry.OuterButtonSizeDip,
                RingGeometry.MinButtonSizeDip, RingGeometry.MaxButtonSizeDip);

            double orbit = double.IsFinite(OrbitRadius)
                ? Math.Round(OrbitRadius) : RingGeometry.OrbitRadiusDip;
            orbit = Math.Clamp(orbit, RingGeometry.MinOrbitFor(button), RingGeometry.MaxOrbitFor(button));

            double duration = Math.Clamp(
                double.IsFinite(AnimationDurationPercent) ? Math.Round(AnimationDurationPercent) : 100.0,
                MinAnimationDurationPercent, MaxAnimationDurationPercent);

            return this with
            {
                Animation = Enum.IsDefined(typeof(RingAnimation), Animation) ? Animation : RingAnimation.Fade,
                ButtonSize = button,
                OrbitRadius = orbit,
                AnimationDurationPercent = duration,
            };
        }
    }
}
