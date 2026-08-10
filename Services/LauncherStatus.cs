namespace Action_Wheel.Services
{
    /// <summary>Whether the global hooks are actually installed, and why not when they are not.</summary>
    /// <remarks>
    /// The two hooks are reported separately because they fail for different reasons and the
    /// distinction is the first thing worth knowing: a mouse hook that will not install is almost
    /// always security software blocking <c>SetWindowsHookEx</c>, while both failing at once usually
    /// means the process is running under something that forbids hooks outright.
    /// </remarks>
    public sealed record LauncherStatus(bool MouseHookInstalled, bool KeyboardHookInstalled, string Error)
    {
        public static readonly LauncherStatus NotStarted = new(false, false, string.Empty);

        /// <summary>True only when the app can do its whole job: open the menu and dismiss it.</summary>
        public bool IsRunning => MouseHookInstalled && KeyboardHookInstalled;
    }
}
