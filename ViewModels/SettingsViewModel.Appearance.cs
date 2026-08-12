using System;
using Action_Wheel.Services;

namespace Action_Wheel.ViewModels
{
    /// <summary>
    /// The ring's size and opening animation: the part of the settings screen that is not about the
    /// nine buttons.
    /// </summary>
    /// <remarks>
    /// A partial of <see cref="SettingsViewModel"/> rather than a class of its own, following
    /// SettingsViewModel.Profiles.cs. These properties are bound directly by the window and share
    /// the base class's Set/Raise and the preview timer, so extracting them into a separate object
    /// would mean either exposing that plumbing or re-forwarding every notification through it. The
    /// split here is for reading, not for ownership - which is why it is a file and not a type.
    /// </remarks>
    public sealed partial class SettingsViewModel
    {
        #region Ring appearance

        private RingAppearance _appearance = RingAppearance.Default;

        /// <summary>The size and animation the preview draws and a save will write.</summary>
        public RingAppearance Appearance => _appearance;

        /// <summary>
        /// The order the animations are offered in, which is deliberately not the enum's order.
        /// </summary>
        /// <remarks>
        /// The enum's numbers go in preferences.json and are append-only, so the two sweeps ended up
        /// at opposite ends of it - and a picker that separates "Clockwise sweep" from
        /// "Counter-clockwise sweep" is a bad picker. Going through this array is what lets the list
        /// be grouped sensibly without renumbering anyone's saved setting. It must stay in step with
        /// the ComboBoxItems in SettingsWindow.xaml.
        /// </remarks>
        private static readonly RingAnimation[] AnimationOrder =
        {
            RingAnimation.None,
            RingAnimation.Fade,
            RingAnimation.Pop,
            RingAnimation.Radiate,
            RingAnimation.Converge,
            RingAnimation.Sweep,
            RingAnimation.CounterSweep,
        };

        /// <summary>Bound to the ComboBox's SelectedIndex, mapped through <see cref="AnimationOrder"/>.</summary>
        public int RingAnimationIndex
        {
            get => Math.Max(0, Array.IndexOf(AnimationOrder, _appearance.Animation));
            set => ApplyAppearance(
                _appearance with
                {
                    Animation = value >= 0 && value < AnimationOrder.Length
                        ? AnimationOrder[value] : RingAnimation.Fade,
                },
                nameof(RingAnimationIndex),
                // The only edit on this card that replays the preview by itself. Picking from a
                // list is a single deliberate act with an answer the user is waiting to see; the
                // two size boxes are not, and replaying on each click of a spinner - or on each
                // digit of a typed number - would be a card that will not sit still. The play
                // button beside the picker is how a size change is reviewed.
                animate: true);
        }

        public double ButtonSize
        {
            get => _appearance.ButtonSize;
            set => ApplyAppearance(_appearance with { ButtonSize = value }, nameof(ButtonSize));
        }

        public double OrbitRadius
        {
            get => _appearance.OrbitRadius;
            set => ApplyAppearance(_appearance with { OrbitRadius = value }, nameof(OrbitRadius));
        }

        /// <summary>
        /// How long the opening animation takes, as a percentage of its tuned timings. Not on the
        /// animation-picker card's replay path: like <see cref="ButtonSize"/> and
        /// <see cref="OrbitRadius"/> it is a number typed or spun rather than a single deliberate
        /// pick, so it does not replay the preview by itself - the Play button is how it is reviewed.
        /// </summary>
        public double AnimationDurationPercent
        {
            get => _appearance.AnimationDurationPercent;
            set => ApplyAppearance(
                _appearance with { AnimationDurationPercent = value }, nameof(AnimationDurationPercent));
        }

        public double MinAnimationDurationPercent => RingAppearance.MinAnimationDurationPercent;
        public double MaxAnimationDurationPercent => RingAppearance.MaxAnimationDurationPercent;

        public double MinButtonSize => RingGeometry.MinButtonSizeDip;
        public double MaxButtonSize => RingGeometry.MaxButtonSizeDip;

        /// <summary>
        /// The orbit's limits for the button size currently chosen. Bound to the NumberBox rather
        /// than written into the XAML as constants, because they are not constants: bigger buttons
        /// need a wider orbit before they stop touching each other, and leave less room before the
        /// ring runs out of window. A fixed range would let the user pick a pair that overlaps.
        /// </summary>
        public double MinOrbitRadius => RingGeometry.MinOrbitFor(_appearance.ButtonSize);

        public double MaxOrbitRadius => RingGeometry.MaxOrbitFor(_appearance.ButtonSize);

        public string RingAnimationDescription => _appearance.Animation switch
        {
            RingAnimation.None => "No animation. The ring is fully drawn on its first frame - the fastest option.",
            RingAnimation.Pop => "Each button springs up from a quarter of its size, overshooting on the way.",
            RingAnimation.Radiate => "The buttons start on the centre and fly out to their places, clockwise from the top.",
            RingAnimation.Converge => "The buttons come in from outside and close around the centre, which appears last.",
            RingAnimation.Sweep => "The ring turns one button-slot clockwise into place, icons staying upright.",
            RingAnimation.CounterSweep => "The ring turns one button-slot anticlockwise into place, icons staying upright.",
            _ => "The ring fades and scales up while the buttons arrive one after another.",
        };

        /// <summary>
        /// Stores a normalised appearance and tells the UI what moved.
        /// </summary>
        /// <remarks>
        /// The else branch is the one that is easy to leave out. A NumberBox reports NaN the moment
        /// its text is empty; Normalised refuses that and keeps the old number, so the record
        /// compares equal, so nothing is raised - and the box then stays blank for the rest of the
        /// session while the model quietly holds a value the user cannot see. Raising the property
        /// on a refused edit is what puts the real number back in the box.
        /// </remarks>
        private void ApplyAppearance(RingAppearance requested, string propertyName, bool animate = false)
        {
            var next = requested.Normalised();
            if (next == _appearance)
            {
                Raise(propertyName);
                return;
            }

            _appearance = next;

            // All of them, not just the one that was edited: clamping the button size moves the
            // orbit's limits, which can in turn move the orbit itself.
            //
            // The limits go FIRST, and that order is load-bearing. A NumberBox coerces its Value
            // into its own Minimum/Maximum and writes the coerced number back through the two-way
            // binding. Raising OrbitRadius while the box still holds the previous size's Minimum
            // therefore does not just display the wrong number - the box overwrites the model with
            // it. Restore defaults with a 64 DIP ring showed exactly that: the size went back to 44
            // and the orbit stuck at 92, the old minimum, instead of 88.
            Raise(nameof(MinOrbitRadius));
            Raise(nameof(MaxOrbitRadius));
            Raise(nameof(RingAnimationIndex));
            Raise(nameof(RingAnimationDescription));
            Raise(nameof(ButtonSize));
            Raise(nameof(OrbitRadius));
            Raise(nameof(AnimationDurationPercent));
            Raise(nameof(Appearance));
            ScheduleRefresh(redraw: true, animate: animate);
        }

        #endregion
    }
}
