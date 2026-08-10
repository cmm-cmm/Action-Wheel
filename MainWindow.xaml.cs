using System;
using Action_Wheel.Services;
using Action_Wheel.ViewModels;

namespace Action_Wheel
{
    /// <summary>
    /// Main application window that provides information about the Action Wheel application.
    /// The actual radial menu functionality runs in the background via the LauncherService.
    /// </summary>
    /// <remarks>
    /// Nothing here decides anything - that all lives in <see cref="MainViewModel"/>. What is left
    /// is the part that genuinely needs the window: sizing it to its content, and the title bar.
    /// </remarks>
    public sealed partial class MainWindow : Microsoft.UI.Xaml.Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            ViewModel = viewModel;

            InitializeComponent();

            Title = "Action Wheel";
            // Draw the caption over Mica so it follows the window theme instead of leaving a white
            // native strip. The XAML uses the real icon.ico, not the placeholder logo asset.
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBarArea);
            AppIcon.ApplyTo(this);

            // Window has no DataContext in WinUI 3; the content element is the binding root.
            RootScroller.DataContext = viewModel;

            RootScroller.Loaded += (s, e) =>
            {
                SizeToContent();
                ViewModel.RefreshStartup();
                ViewModel.RefreshHookStatus();
            };
        }

        public MainViewModel ViewModel { get; }

        /// <summary>
        /// Sizes the window to the content instead of leaving it at the WinUI default, which is far
        /// wider than the 560px-wide card it contains and leaves most of the window empty.
        /// </summary>
        private void SizeToContent()
            => WindowSizing.SizeAndCentre(this, RootScroller, 640, 820, "the main window");
    }
}
