using Action_Wheel.Core;

// A manual regression check for ChordGate, the two-button "hold one, press the other" mouse
// trigger's swallow decisions - extracted out of MouseHook.cs after a project audit found that a
// completed chord swallowed BOTH buttons' UP events, when only the second button's should be
// swallowed. The first button's DOWN already reached the app before the chord was recognised, so
// its UP has to reach the app too, or the app is left believing that button is still held.
//
// There is deliberately no test project (see CLAUDE.md), so this is the same kind of throwaway
// console tool Tools/RoundTripCheck already is - run it by hand after touching ChordGate, with:
//   dotnet run --project Tools\ChordGateCheck\ChordGateCheck.csproj

int failures = 0;

void Check(string label, bool passed)
{
    Console.WriteLine(passed ? $"  OK    {label}" : $"  FAIL  {label}");
    if (!passed)
        failures++;
}

var epoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

Console.WriteLine("ChordGate regression check");
Console.WriteLine();

// Scenario 1: HoldRightPressLeft, a normal chord - right held, then left pressed and released
// within the window. Right's DOWN was already forwarded before the chord fired, so its UP must
// NOT be swallowed; left's DOWN and UP were both swallowed as the chord itself, so both must be.
{
    var gate = new ChordGate();

    bool rightDownSwallowed = gate.OnRightDown(ChordTrigger.None, epoch, 0, 0, out bool rightChordFired);
    Check("chord 1: right-down (first button) is not swallowed", !rightDownSwallowed);
    Check("chord 1: right-down alone does not fire the chord", !rightChordFired);

    bool leftDownSwallowed = gate.OnLeftDown(
        ChordTrigger.HoldRightPressLeft, epoch.AddMilliseconds(100), 3, -2, out bool leftChordFired);
    Check("chord 1: left-down (second button) is swallowed", leftDownSwallowed);
    Check("chord 1: left-down fires the chord", leftChordFired);

    Check("chord 1: right-up (first button) is NOT swallowed - this is the bug that was fixed",
        !gate.OnRightUp());
    Check("chord 1: left-up (second button) is swallowed", gate.OnLeftUp());
}

// Scenario 2: HoldLeftPressRight - the mirror image of scenario 1.
{
    var gate = new ChordGate();

    bool leftDownSwallowed = gate.OnLeftDown(ChordTrigger.None, epoch, 0, 0, out bool leftChordFired);
    Check("chord 2: left-down (first button) is not swallowed", !leftDownSwallowed);
    Check("chord 2: left-down alone does not fire the chord", !leftChordFired);

    bool rightDownSwallowed = gate.OnRightDown(
        ChordTrigger.HoldLeftPressRight, epoch.AddMilliseconds(100), -1, 4, out bool rightChordFired);
    Check("chord 2: right-down (second button) is swallowed", rightDownSwallowed);
    Check("chord 2: right-down fires the chord", rightChordFired);

    Check("chord 2: left-up (first button) is NOT swallowed - this is the bug that was fixed",
        !gate.OnLeftUp());
    Check("chord 2: right-up (second button) is swallowed", gate.OnRightUp());
}

// Scenario 3: only the first button is ever pressed - no chord should form, and the lone
// down/up pair must pass through untouched.
{
    var gate = new ChordGate();

    bool downSwallowed = gate.OnRightDown(ChordTrigger.None, epoch, 0, 0, out bool chordFired);
    Check("chord 3: a lone right-down is not swallowed", !downSwallowed);
    Check("chord 3: a lone right-down does not fire the chord", !chordFired);
    Check("chord 3: the matching right-up is not swallowed either", !gate.OnRightUp());
}

// Scenario 4: the second button arrives after ChordTimeoutMs - too slow to count as a chord.
{
    var gate = new ChordGate { ChordTimeoutMs = 500 };

    gate.OnRightDown(ChordTrigger.None, epoch, 0, 0, out _);
    bool leftDownSwallowed = gate.OnLeftDown(
        ChordTrigger.HoldRightPressLeft, epoch.AddMilliseconds(501), 0, 0, out bool chordFired);

    Check("chord 4: a too-slow second press does not fire the chord", !chordFired);
    Check("chord 4: a too-slow second press's down is not swallowed", !leftDownSwallowed);
    Check("chord 4: its up is not swallowed either", !gate.OnLeftUp());
}

// Scenario 5: the cursor moves past ChordMovementThreshold between the two downs.
{
    var gate = new ChordGate { ChordMovementThreshold = 10 };

    gate.OnRightDown(ChordTrigger.None, epoch, 0, 0, out _);
    bool leftDownSwallowed = gate.OnLeftDown(
        ChordTrigger.HoldRightPressLeft, epoch.AddMilliseconds(50), 11, 0, out bool chordFired);

    Check("chord 5: a too-far second press does not fire the chord", !chordFired);
    Check("chord 5: a too-far second press's down is not swallowed", !leftDownSwallowed);
    Check("chord 5: its up is not swallowed either", !gate.OnLeftUp());
}

// Scenario 6: the trigger is configured as HoldRightPressLeft, but the physical order is
// reversed (left first, then right). MouseHook only ever passes a non-None ChordTrigger for the
// button that is actually configured as the SECOND one, so a right-down here must not fire a
// HoldLeftPressRight-shaped chord it was never asked to recognise.
{
    var gate = new ChordGate();

    gate.OnLeftDown(ChordTrigger.None, epoch, 0, 0, out _);
    bool rightDownSwallowed = gate.OnRightDown(ChordTrigger.None, epoch.AddMilliseconds(50), 0, 0, out bool chordFired);

    Check("chord 6: the wrong physical order does not fire the chord", !chordFired);
    Check("chord 6: the wrong physical order's down is not swallowed", !rightDownSwallowed);
    Check("chord 6: its up is not swallowed either", !gate.OnRightUp());
    Check("chord 6: the earlier left-up is not swallowed either", !gate.OnLeftUp());
}

Console.WriteLine();
Console.WriteLine(failures == 0
    ? "All ChordGate checks passed."
    : $"{failures} ChordGate check(s) failed.");

return failures == 0 ? 0 : 1;
