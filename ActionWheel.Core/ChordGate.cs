using System;

namespace Action_Wheel.Core
{
    /// <summary>Which two-button "hold one, press the other" mouse gesture is configured, if any.</summary>
    public enum ChordTrigger
    {
        None,
        HoldRightPressLeft,
        HoldLeftPressRight,
    }

    /// <summary>
    /// Decides, for the two chord triggers, which DOWN/UP mouse events <c>MouseHook</c> must swallow
    /// so the chord opens the menu without either physical button reaching the application
    /// underneath in an unbalanced state.
    /// </summary>
    /// <remarks>
    /// Extracted out of <c>MouseHook</c> so this decision can be driven from a plain object, with no
    /// real Win32 hook installed - see <c>Tools/ChordGateCheck</c>. <c>MouseHook</c> still owns
    /// everything that needs an actual HWND/hook handle; this owns only "should this event be
    /// swallowed", given as plain values (a timestamp and a position, not <c>Windows.Graphics.PointInt32</c>)
    /// so it has no Win32/WinRT dependency to test around. The timestamp is milliseconds on a
    /// monotonic clock (<c>Environment.TickCount64</c>), not wall-clock time. A system clock
    /// change, such as an NTP correction between the two presses, used to make a real chord look
    /// too slow, or a stale first press look recent.
    ///
    /// The button held FIRST is not swallowed. Its DOWN has already gone through to the app before
    /// the chord could be recognised - recognition needs the SECOND button's DOWN to arrive - so its
    /// matching UP must go through too, or the app is left believing that button is still held. Only
    /// the second button's DOWN (which never reaches the app - it is what completes the chord and is
    /// swallowed right here) and its UP are swallowed. A prior version of this logic swallowed both
    /// buttons' UPs, which balanced the second button correctly but desynchronised the first.
    /// </remarks>
    public sealed class ChordGate
    {
        private bool _leftDown;
        private bool _rightDown;
        private bool _swallowLeftUp;
        private bool _swallowRightUp;
        private long _leftDownAtMs;
        private long _rightDownAtMs;
        private (int X, int Y) _leftDownPosition;
        private (int X, int Y) _rightDownPosition;

        /// <summary>How long after the first button goes down the second still counts as a chord.</summary>
        public int ChordTimeoutMs { get; set; } = 800;

        /// <summary>How far the cursor may move between the two downs and still count as a chord.</summary>
        public int ChordMovementThreshold { get; set; } = 12;

        /// <summary>
        /// The left button went down. Returns true if this DOWN must be swallowed (it completed a
        /// <see cref="ChordTrigger.HoldRightPressLeft"/> chord), and sets
        /// <paramref name="chordFired"/> so the caller knows to raise its own open/close events.
        /// </summary>
        public bool OnLeftDown(ChordTrigger trigger, long nowMs, int x, int y, out bool chordFired)
        {
            _leftDown = true;
            _leftDownAtMs = nowMs;
            _leftDownPosition = (x, y);
            chordFired = false;

            if (trigger == ChordTrigger.HoldRightPressLeft && _rightDown
                && IsChordValid(_rightDownAtMs, _rightDownPosition, nowMs, x, y))
            {
                // Only this press's own UP - the right button's DOWN already reached the app before
                // this chord was recognised, so its UP must still reach the app too.
                _swallowLeftUp = true;
                chordFired = true;
                return true;
            }

            return false;
        }

        /// <summary>The left button went up. Returns true if this UP must be swallowed.</summary>
        public bool OnLeftUp()
        {
            _leftDown = false;
            if (!_swallowLeftUp)
                return false;

            _swallowLeftUp = false;
            return true;
        }

        /// <summary>
        /// The right button went down. Returns true if this DOWN must be swallowed (it completed a
        /// <see cref="ChordTrigger.HoldLeftPressRight"/> chord), and sets
        /// <paramref name="chordFired"/> so the caller knows to raise its own open/close events.
        /// </summary>
        public bool OnRightDown(ChordTrigger trigger, long nowMs, int x, int y, out bool chordFired)
        {
            _rightDown = true;
            _rightDownAtMs = nowMs;
            _rightDownPosition = (x, y);
            chordFired = false;

            if (trigger == ChordTrigger.HoldLeftPressRight && _leftDown
                && IsChordValid(_leftDownAtMs, _leftDownPosition, nowMs, x, y))
            {
                _swallowRightUp = true;
                chordFired = true;
                return true;
            }

            return false;
        }

        /// <summary>The right button went up. Returns true if this UP must be swallowed.</summary>
        public bool OnRightUp()
        {
            _rightDown = false;
            if (!_swallowRightUp)
                return false;

            _swallowRightUp = false;
            return true;
        }

        private bool IsChordValid(long firstDownAtMs, (int X, int Y) firstPosition, long nowMs, int x, int y)
        {
            if (nowMs - firstDownAtMs > ChordTimeoutMs)
                return false;

            int dx = x - firstPosition.X;
            int dy = y - firstPosition.Y;
            return dx * dx + dy * dy <= ChordMovementThreshold * ChordMovementThreshold;
        }
    }
}
