using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Action_Wheel.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Action_Wheel.Overlay
{
    /// <summary>
    /// Runs one AI task: confirm, ask, show the answer. Built in code, like the settings window's
    /// dialogs, since it is a handful of controls in a few states. See Doc/ai-assistants.md, phase 2.
    /// </summary>
    /// <remarks>
    /// Order matters. The selection is copied <em>before</em> this window exists. Once it is
    /// activated it has the focus, and the Ctrl+C would go to it instead of the app the user
    /// selected text in.
    /// </remarks>
    public sealed class AiTaskWindow : Window
    {
        private const double WidthDip = 620;
        private const double HeightDip = 460;

        private readonly AiTaskRequest _request;
        private readonly Grid _root = new() { Padding = new Thickness(20), RowSpacing = 12 };
        private readonly TextBlock _title = new() { FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        private readonly ContentControl _body = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        private readonly CancellationTokenSource _cancel = new();
        private bool _closed;

        private AiTaskWindow(AiTaskRequest request, string title)
        {
            _request = request;
            Title = "Action Wheel — AI";
            AppIcon.ApplyTo(this);
            SystemBackdrop = new MicaBackdrop();

            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _title.Text = title;
            Grid.SetRow(_title, 0);
            Grid.SetRow(_body, 1);
            Grid.SetRow(_buttons, 2);
            _root.Children.Add(_title);
            _root.Children.Add(_body);
            _root.Children.Add(_buttons);
            Content = _root;

            // Esc closes from any state. handledEventsToo, because the focused button or text box
            // marks key events handled before they bubble up here.
            _root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Escape)
                    Close();
            }), handledEventsToo: true);

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                // Above the app it was opened over, which is where the user's attention is.
                presenter.IsAlwaysOnTop = true;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
            }

            _root.Loaded += (s, e) => WindowSizing.SizeAndCentre(this, _root, WidthDip, HeightDip, "the AI window");
            Closed += (s, e) =>
            {
                _closed = true;
                _cancel.Cancel();
            };
        }

        /// <summary>Runs <paramref name="request"/>. Call on the UI thread. Never throws.</summary>
        public static async Task StartAsync(AiTaskRequest request)
        {
            AiTaskWindow? window = null;
            try
            {
                var tool = AiTools.Find(request.ToolId);
                var template = AiTaskTemplates.Find(AiTaskTemplates.Load(), request.TemplateId);
                string title = tool != null && template != null ? $"{template.Name} with {tool.Name}" : "AI task";

                // Nothing is on screen yet. See the class remarks for why.
                var capture = await Task.Run(() => SelectionCapture.Capture(request.TargetWindow));

                window = new AiTaskWindow(request, title);
                window.Activate();
                await window.RunAsync(tool, template, capture);
            }
            catch (Exception ex)
            {
                window?.ShowError($"Something went wrong: {ex.Message}");
            }
        }

        private async Task RunAsync(AiTool? tool, AiTaskTemplate? template, SelectionCapture.Result capture)
        {
            if (tool == null || !tool.SupportsTasks)
            {
                ShowError("This button names an AI tool that cannot run tasks. Pick another function for it in Settings.");
                return;
            }

            if (template == null)
            {
                ShowError($"The prompt template '{_request.TemplateId}' no longer exists. Pick another one for this button in Settings, under AI assistants.");
                return;
            }

            if (!AiAssistantSettings.IsEnabled(tool.Id))
            {
                ShowError($"{tool.Name} is switched off in Settings, under AI assistants.");
                return;
            }

            var found = await Task.Run(() => AiToolDetector.Detect(tool));
            if (!found.IsInstalled)
            {
                ShowError($"{tool.Name} was not found on this computer.");
                return;
            }

            string text = capture.Text;
            if (text.Trim().Length == 0)
            {
                ShowError("Nothing was selected, and the clipboard holds no text. Select some text first, then use the button again.");
                return;
            }

            if (text.Length > AiTaskTemplates.MaxSelectionLength)
            {
                ShowError($"The text is {text.Length:N0} characters long, more than the {AiTaskTemplates.MaxSelectionLength:N0} a task sends. Select less of it.");
                return;
            }

            // Always asked when the text did not come from a selection: the clipboard may hold
            // something the user copied long ago and would not want sent.
            if (template.Confirm || !capture.FromSelection)
            {
                if (!await ConfirmAsync(tool, text, capture.FromSelection))
                {
                    Close();
                    return;
                }
            }

            ShowBusy($"Asking {tool.Name}…");
            var result = await AiCliRunner.RunAsync(tool, found.Path, AiTaskTemplates.BuildPrompt(template, text), _cancel.Token);
            if (_closed)
                return;

            if (result.Succeeded)
                ShowAnswer(result.Answer, canReplace: capture.FromSelection);
            else
                ShowError(result.Error);
        }

        private Task<bool> ConfirmAsync(AiTool tool, string text, bool fromSelection)
        {
            var answer = new TaskCompletionSource<bool>();
            Closed += (s, e) => answer.TrySetResult(false);

            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = (fromSelection
                    ? $"This selected text will be sent to {tool.Name}"
                    : $"Nothing was selected, so this text from the clipboard will be sent to {tool.Name}")
                    + ", through your own sign-in with it.",
            });
            panel.Children.Add(PreviewBox(text.Length > 4000 ? text[..4000] + "…" : text, 220));
            panel.Children.Add(new TextBlock
            {
                Text = $"{text.Length:N0} characters",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            _body.Content = panel;

            var send = new Button { Content = "Send", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
            send.Click += (s, e) => answer.TrySetResult(true);
            var cancel = new Button { Content = "Cancel" };
            cancel.Click += (s, e) => answer.TrySetResult(false);
            SetButtons(send, cancel);
            send.Focus(FocusState.Programmatic);

            return answer.Task;
        }

        private void ShowBusy(string message)
        {
            var panel = new StackPanel { Spacing = 12, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(new ProgressRing { IsActive = true, Width = 40, Height = 40 });
            panel.Children.Add(new TextBlock { Text = message, HorizontalAlignment = HorizontalAlignment.Center });
            _body.Content = panel;

            var cancel = new Button { Content = "Cancel" };
            cancel.Click += (s, e) => Close();
            SetButtons(cancel);
        }

        private void ShowAnswer(string answer, bool canReplace)
        {
            _body.Content = PreviewBox(answer, double.NaN);

            var copy = new Button { Content = "Copy" };
            copy.Click += (s, e) => copy.Content = ShellCommands.CopyText(answer, out _) ? "Copied" : "Could not copy";

            var close = new Button { Content = "Close" };
            close.Click += (s, e) => Close();

            if (canReplace)
            {
                var replace = new Button { Content = "Replace selection", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
                replace.Click += (s, e) => ReplaceSelection(answer);
                SetButtons(replace, copy, close);
            }
            else
            {
                SetButtons(copy, close);
            }
        }

        private void ShowError(string message)
        {
            if (_closed)
                return;

            _body.Content = PreviewBox(message, double.NaN);
            var close = new Button { Content = "Close" };
            close.Click += (s, e) => Close();
            SetButtons(close);
        }

        /// <summary>
        /// Puts the answer where the selection was: onto the clipboard, back to the original window,
        /// then Ctrl+V. The clipboard keeps the answer afterwards. It already held the selection,
        /// from the copy this task started with.
        /// </summary>
        private void ReplaceSelection(string answer)
        {
            if (!ShellCommands.CopyText(answer, out string error))
            {
                ShowError($"The answer could not be put on the clipboard: {error}");
                return;
            }

            IntPtr target = _request.TargetWindow;
            Close();

            // This window was in front, so Windows lets it hand the foreground back.
            SetForegroundWindow(target);
            _ = Task.Run(() =>
            {
                Thread.Sleep(150);
                ActionDispatcher.SendKeys("Ctrl+V", "Paste the AI answer");
            });
        }

        private static TextBox PreviewBox(string text, double height)
        {
            var box = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
            if (!double.IsNaN(height))
                box.MaxHeight = height;
            return box;
        }

        private void SetButtons(params Button[] buttons)
        {
            _buttons.Children.Clear();
            foreach (var button in buttons)
                _buttons.Children.Add(button);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr window);
    }
}
