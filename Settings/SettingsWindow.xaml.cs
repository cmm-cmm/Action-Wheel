using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Action_Wheel.Services;
using Action_Wheel.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Storage.Pickers;
using InfoBarSeverity = Action_Wheel.ViewModels.StatusLevel;

namespace Action_Wheel.Settings
{
    /// <summary>
    /// The settings window: edits the nine radial-menu buttons and writes them back to actions.json.
    /// </summary>
    /// <remarks>
    /// All of that is decided by <see cref="SettingsViewModel"/>. What is left here is the work that
    /// genuinely needs a window: sizing it, the custom title bar, flyouts anchored to a particular
    /// button, a file picker that needs an owner HWND, the routed-key plumbing behind shortcut
    /// recording, and drawing the preview onto a Canvas. Each of those gathers an input and hands it
    /// to the view model; none of them decides what it means.
    ///
    /// Saving only rewrites the file - <see cref="ActionsSaved"/> tells the app to ask
    /// <see cref="LauncherService"/> to reload, so the next menu picks the changes up. The menu that
    /// may already be on screen keeps the configuration it was opened with.
    /// </remarks>
    public sealed partial class SettingsWindow : Window
    {
        // Held in a field because AddHandler/RemoveHandler have to be given the same delegate
        // instance; a fresh method group conversion each time would never unsubscribe.
        private readonly KeyEventHandler _recordKeyHandler;

        /// <summary>
        /// Guards every ContentDialog this window opens: a second one in the same XamlRoot throws.
        /// </summary>
        /// <remarks>
        /// One flag for all of them rather than one each. Two flags would each correctly stop their
        /// own dialog reopening and neither would stop the close prompt appearing on top of the
        /// key-name list - which is reachable, because the window can be closed while that list is
        /// up.
        /// </remarks>
        private bool _dialogOpen;

        /// <summary>Set once the user has answered the unsaved-changes prompt, or on app exit.</summary>
        private bool _forceClose;

        /// <summary>Raised after actions.json has been written successfully.</summary>
        public event EventHandler? ActionsSaved;

        public SettingsWindow(SettingsViewModel viewModel)
        {
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();

            _recordKeyHandler = OnRecordKeyDown;

            Title = "Action Wheel — Settings";
            AppIcon.ApplyTo(this);

            // Extend Mica into the caption and designate the transparent strip as the drag region.
            // Its Image points to the actual icon.ico shipped with the app.
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBarArea);

            // Window is not a FrameworkElement in WinUI 3 and has no DataContext of its own, so the
            // binding root is the content element.
            RootGrid.DataContext = ViewModel;
            ActionsHost.ItemsSource = ViewModel.Items;

            // The bool is "replay the opening animation" - see SettingsViewModel.PreviewInvalidated.
            ViewModel.PreviewInvalidated += (s, animate) => RingPreview.Render(
                PreviewCanvas, ViewModel.Items, ViewModel.Appearance,
                item => ActionsHost.SelectedItem = item, animate);
            ViewModel.ActionsSaved += (s, e) => ActionsSaved?.Invoke(this, EventArgs.Empty);

            RootGrid.Loaded += RootGrid_Loaded;
            AppWindow.Closing += OnClosing;
            Closed += (s, e) =>
            {
                StopRecording();
                ViewModel.Dispose();
            };
        }


        public SettingsViewModel ViewModel { get; }

        /// <summary>
        /// Sizes and centres the window once the content exists, so the DPI scale is known.
        /// AppWindow works in physical pixels while the layout above is in DIPs.
        /// </summary>
        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            WindowSizing.SizeAndCentre(this, RootGrid, 1400, 900, "the Settings window");

            // The canvas only exists now, so this is the first render that can actually draw.
            ViewModel.RefreshDerivedState();
        }

        #region Icon picker

        /// <summary>
        /// The icon picker. Everything that can put a picture on a button lives in this one flyout -
        /// the font's glyphs, an image file, and the icon of whatever program the button launches -
        /// because those are three answers to one question, and splitting them across the window made
        /// the file-based ones easy to miss entirely.
        /// </summary>
        private void PickGlyph_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not ActionEditModel model)
                return;

            var flyout = new Flyout();

            // Shows what the ring will actually draw, which is the only way to tell a tinted SVG
            // from an untinted one, or to see that a glyph is being overridden by an image file.
            var preview = new Border
            {
                Width = 56,
                Height = 56,
                CornerRadius = new CornerRadius(28),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var sourceNote = new TextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0),
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            };

            void RefreshPreview()
            {
                var item = model.ToActionItem();
                preview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(model.BackgroundColor);
                // Match the 20 px gallery glyphs below. This used to be 24 px, so entering the same
                // hex code made the preview look larger than the glyph the user had just selected.
                preview.Child = IconFactory.CreateIcon(
                    item, 20, new Microsoft.UI.Xaml.Media.SolidColorBrush(model.ForegroundColor));

                sourceNote.Text = model.HasIconFile
                    ? $"Using the file {System.IO.Path.GetFileName(model.IconPath)} - it replaces the glyph."
                    : "Using a glyph from the icon font.";
            }

            var codeBox = new TextBox
            {
                Header = "Icon code (hex)",
                Text = model.Glyph,
                PlaceholderText = "F0C5",
            };
            codeBox.TextChanged += (s, _) =>
            {
                ViewModel.SetGlyph(model, codeBox.Text);
                RefreshPreview();
            };

            var searchBox = new TextBox
            {
                PlaceholderText = "Search by name or code",
                Margin = new Thickness(0, 12, 0, 0),
            };

            var showAll = new CheckBox
            {
                Content = $"Show every glyph in the font ({GlyphChoice.All.Count})",
                Margin = new Thickness(0, 4, 0, 0),
            };

            var gallery = new GridView
            {
                ItemTemplate = (DataTemplate)RootGrid.Resources["GlyphChoiceTemplate"],
                SelectionMode = ListViewSelectionMode.Single,
                IsItemClickEnabled = false,
                Width = 380,
                Height = 260,
                Margin = new Thickness(0, 6, 0, 0),
            };

            void RefreshGallery()
            {
                var source = showAll.IsChecked == true ? GlyphChoice.All : GlyphChoice.Common;
                gallery.ItemsSource = GlyphChoice.Search(source, searchBox.Text);
            }

            searchBox.TextChanged += (s, _) => RefreshGallery();
            showAll.Checked += (s, _) => RefreshGallery();
            showAll.Unchecked += (s, _) => RefreshGallery();

            gallery.SelectionChanged += (s, _) =>
            {
                if (gallery.SelectedItem is not GlyphChoice choice)
                    return;

                ViewModel.SetGlyph(model, choice.Code);
                codeBox.Text = choice.Code;
                RefreshPreview();
            };

            var chooseFile = new Button
            {
                Content = "Use an image file…",
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            chooseFile.Click += async (s, _) =>
            {
                var file = await PickFileAsync(".svg", ".png", ".ico");
                if (file == null)
                    return;

                ViewModel.SetIconPath(model, file);
                RefreshPreview();
            };

            var useAppIcon = new Button
            {
                Content = "Use the app's icon",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 6, 0, 0),
                // Only a launch target has an application behind it to take an icon from.
                IsEnabled = model.CanBrowse && !string.IsNullOrWhiteSpace(model.Value),
            };
            useAppIcon.Click += async (s, _) =>
            {
                await ViewModel.UseAppIconAsync(model, model.Value);
                RefreshPreview();
            };

            var clearFile = new Button
            {
                Content = "Back to the glyph",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 6, 0, 0),
            };
            clearFile.Click += (s, _) =>
            {
                model.IconPath = string.Empty;
                RefreshPreview();
            };

            RefreshPreview();
            RefreshGallery();

            flyout.Content = new StackPanel
            {
                Width = 380,
                Children =
                {
                    preview,
                    sourceNote,
                    codeBox,
                    searchBox,
                    showAll,
                    gallery,
                    chooseFile,
                    useAppIcon,
                    clearFile,
                },
            };

            flyout.ShowAt(button);
        }

        #endregion

        private void FunctionSuggestion_Chosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (sender.DataContext is ActionEditModel model && args.SelectedItem is WindowsFunction function)
            {
                model.Value = function.Id;
                sender.Text = function.Name;
            }
        }

        private void FunctionBox_OpenSuggestions(object sender, RoutedEventArgs e)
        {
            if (sender is not AutoSuggestBox box || box.DataContext is not ActionEditModel model)
                return;

            model.ShowAllFunctionChoices();
            box.IsSuggestionListOpen = true;
        }

        private void TestAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: ActionEditModel model })
                ViewModel.TestAction(model);
        }

        /// <summary>The key names a shortcut can be typed from.</summary>
        /// <remarks>
        /// A dialog rather than anything laid out on the page: this reference is only wanted while
        /// someone types a shortcut by hand, and the row it is opened from is already close to the
        /// window's width with no horizontal scroller above it, so anything that grows there is
        /// clipped rather than scrolled to.
        ///
        /// The names are written out here rather than generated from
        /// <see cref="ShortcutKeys"/>, whose parser is a switch expression with nothing to
        /// enumerate. Adding a name there means adding it here as well. The bracketed aliases were
        /// already accepted by that parser and had never been written down anywhere.
        /// </remarks>
        private async void ShowKeyNames_Click(object sender, RoutedEventArgs e)
        {
            // ContentDialog throws when a second one is opened in the same XamlRoot, and nothing
            // stops the button being clicked again while this one is up.
            if (_dialogOpen || Content is not FrameworkElement root)
                return;

            var sections = new (string Heading, string Names)[]
            {
                ("Modifiers", "Ctrl (Control), Alt, Shift, Win (Windows, LWin)"),
                ("Letters and digits", "A–Z, 0–9"),
                ("Function keys", "F1–F24"),
                ("Editing and navigation",
                    "Esc (Escape), Tab, Enter (Return), Space, Backspace, Delete (Del), "
                    + "Insert (Ins), Home, End, PageUp (PgUp), PageDown (PgDn), Left, Up, Right, "
                    + "Down, PrintScreen (PrtSc)"),
                ("Media and volume",
                    "PlayPause (MediaPlayPause), MediaNext, MediaPrev (MediaPrevious), MediaStop, "
                    + "VolumeUp, VolumeDown, Mute (VolumeMute)"),
            };

            var panel = new StackPanel { Spacing = 12 };

            foreach (var (heading, names) in sections)
            {
                var group = new StackPanel { Spacing = 2 };
                group.Children.Add(new TextBlock
                {
                    Text = heading,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                });
                group.Children.Add(new TextBlock { Text = names, TextWrapping = TextWrapping.Wrap });
                panel.Children.Add(group);
            }

            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Text = "Join keys with +, for example Ctrl+Shift+S. Names are not case sensitive. "
                    + "Recording captures Ctrl, Alt and Shift combinations; the Windows key is taken "
                    + "by the shell before it reaches the app, so type Win+… by hand. Press Esc to "
                    + "cancel recording.",
            });

            var dialog = new ContentDialog
            {
                // Both are required for a dialog built in code: without the XamlRoot it has no
                // tree to open in, and without the theme it ignores the one this window is using.
                XamlRoot = root.XamlRoot,
                RequestedTheme = root.RequestedTheme,
                Title = "Recognised key names",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                Content = new ScrollViewer
                {
                    MaxHeight = 460,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = panel,
                },
            };

            _dialogOpen = true;
            try
            {
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                // A reference list that will not open is not worth taking the window down for,
                // which is what an exception leaving a Click handler does.
                ViewModel.ShowStatus(InfoBarSeverity.Error,
                    "Could not open the key-name list", ex.Message);
            }
            finally
            {
                _dialogOpen = false;
            }
        }

        /// <summary>
        /// Edits a Group button's up to <see cref="ActionItem.MaxGroupChildren"/> children: a plain
        /// label/type/value/glyph row each, read straight off their controls when Save is pressed
        /// rather than bound through a model of their own - this dialog is the only place that ever
        /// looks at them, and it is short-lived, so there is nothing for a live-updating model to buy
        /// here that direct reads do not already give for less code. Saving replaces
        /// <see cref="ActionEditModel.GroupChildren"/> wholesale, the same all-at-once approach
        /// <see cref="ActionEditModel.CopyFrom"/> uses for every other field.
        /// </summary>
        private async void EditGroup_Click(object sender, RoutedEventArgs e)
        {
            if (_dialogOpen || Content is not FrameworkElement root)
                return;
            if (sender is not Button button || button.Tag is not ActionEditModel model)
                return;

            var rowsPanel = new StackPanel { Spacing = 8 };
            var rows = new List<(TextBox Label, ComboBox Kind, TextBox Value, TextBox Glyph, Grid Host)>();
            Button? addButton = null;

            void AddRow(ActionItem? existing)
            {
                var label = new TextBox { PlaceholderText = "Label", Width = 130, Text = existing?.Label ?? string.Empty };
                var kind = new ComboBox
                {
                    Width = 140,
                    // ItemsSource before SelectedIndex, deliberately: object initializers run in the
                    // order written, and a ComboBox with no items yet has Items.Count == 0 - setting
                    // SelectedIndex to anything but -1 against that throws
                    // "Value does not fall within the expected range", which is what crashed the
                    // whole app the moment this dialog's Add button built a second row (the first
                    // ever call to AddRow(null), since a brand-new group has no existing children to
                    // have already hit the same bug when the dialog opened).
                    ItemsSource = new[] { "Do nothing", "Send shortcut", "Open app / file", "Windows function" },
                    SelectedIndex = ActionValueCodec.KindToIndex(existing?.Kind ?? ActionKind.None),
                };
                var value = new TextBox { PlaceholderText = "Value", Width = 170, Text = existing?.Value ?? string.Empty };
                var glyph = new TextBox { PlaceholderText = "Glyph hex", Width = 70, Text = existing?.Glyph ?? string.Empty };
                var remove = new Button
                {
                    Width = 32,
                    Height = 32,
                    Padding = new Thickness(0),
                    Content = new FontIcon
                    {
                        // Application.Current.Resources, not root.Resources: IconFontFamily is
                        // declared once in App.xaml's Application.Resources, not duplicated into
                        // every window's own ResourceDictionary. {StaticResource} in XAML markup
                        // walks up to Application resources automatically when a key is not found
                        // locally; the plain C# indexer on one specific ResourceDictionary does not
                        // - root.Resources["IconFontFamily"] (RootGrid's own dictionary, which does
                        // not have this key) threw KeyNotFoundException here, unhandled, on the UI
                        // thread - the actual crash, on top of the ComboBox one already fixed above,
                        // triggered by the exact same "Add a button" click reported.
                        FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["IconFontFamily"],
                        Glyph = "",
                        FontSize = 12,
                    },
                };

                var host = new Grid { ColumnSpacing = 6 };
                foreach (var width in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto })
                    host.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
                Grid.SetColumn(label, 0);
                Grid.SetColumn(kind, 1);
                Grid.SetColumn(value, 2);
                Grid.SetColumn(glyph, 3);
                Grid.SetColumn(remove, 4);
                host.Children.Add(label);
                host.Children.Add(kind);
                host.Children.Add(value);
                host.Children.Add(glyph);
                host.Children.Add(remove);

                var entry = (label, kind, value, glyph, host);
                rows.Add(entry);
                rowsPanel.Children.Add(host);

                remove.Click += (s2, e2) =>
                {
                    rowsPanel.Children.Remove(host);
                    rows.Remove(entry);
                    if (addButton != null)
                        addButton.IsEnabled = rows.Count < ActionItem.MaxGroupChildren;
                };
            }

            foreach (var child in model.GroupChildren)
                AddRow(child);

            addButton = new Button { Content = "Add a button", IsEnabled = rows.Count < ActionItem.MaxGroupChildren };
            addButton.Click += (s2, e2) =>
            {
                AddRow(null);
                addButton.IsEnabled = rows.Count < ActionItem.MaxGroupChildren;
            };

            var content = new StackPanel { Spacing = 12, Width = 560 };
            content.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Text = "These appear on a second ring around the main one when this button is selected. "
                    + "Glyph is a hex code from the bundled icon font, the same as the main list - leave "
                    + "it blank for no picture.",
            });
            content.Children.Add(rowsPanel);
            content.Children.Add(addButton);

            var dialog = new ContentDialog
            {
                XamlRoot = root.XamlRoot,
                RequestedTheme = root.RequestedTheme,
                Title = "Group buttons",
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = new ScrollViewer
                {
                    MaxHeight = 420,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = content,
                },
            };

            _dialogOpen = true;
            try
            {
                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                    return;

                var children = new List<ActionItem>();
                for (int i = 0; i < rows.Count; i++)
                {
                    var (label, kind, value, glyph, _) = rows[i];
                    children.Add(new ActionItem
                    {
                        Tag = i,
                        Label = label.Text.Trim(),
                        Kind = ActionValueCodec.IndexToKind(kind.SelectedIndex),
                        Value = value.Text.Trim(),
                        Glyph = glyph.Text.Trim().ToUpperInvariant(),
                    });
                }

                model.GroupChildren = children;
            }
            catch (Exception ex)
            {
                ViewModel.ShowStatus(InfoBarSeverity.Error, "Could not open the group editor", ex.Message);
            }
            finally
            {
                _dialogOpen = false;
            }
        }

        #region Closing

        /// <summary>
        /// Closes without asking about unsaved work. For application exit, where the user has
        /// already said what they want and there is no window left to answer a prompt in.
        /// </summary>
        public void ForceClose()
        {
            _forceClose = true;
            Close();
        }

        /// <summary>
        /// Intercepts the close so edits are not thrown away in silence.
        /// </summary>
        /// <remarks>
        /// Cancel-then-reclose rather than answering inline: AppWindowClosingEventArgs is decided
        /// synchronously and a ContentDialog is not, so there is no way to hold the close open while
        /// the question is on screen. The close is refused, the dialog runs on its own, and
        /// <see cref="_forceClose"/> makes the second attempt go straight through.
        /// </remarks>
        private void OnClosing(Microsoft.UI.Windowing.AppWindow sender,
            Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            if (_forceClose || _dialogOpen || !ViewModel.HasUnsavedChanges)
                return;

            args.Cancel = true;
            _ = ConfirmThenCloseAsync();
        }

        private async Task ConfirmThenCloseAsync()
        {
            if (Content is not FrameworkElement root)
            {
                // No tree to host a dialog in. Refusing to close would strand the window.
                ForceClose();
                return;
            }

            _dialogOpen = true;
            try
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = root.XamlRoot,
                    RequestedTheme = root.RequestedTheme,
                    Title = "Save your changes?",
                    Content = "The buttons, the ring size and the trigger settings have all been "
                        + "edited since the last save. Closing now discards those edits.",
                    PrimaryButtonText = "Save and close",
                    SecondaryButtonText = "Discard",
                    CloseButtonText = "Keep editing",
                    DefaultButton = ContentDialogButton.Primary,
                };

                var answer = await dialog.ShowAsync();

                // None is the close button and also what dismissing with Esc returns, which is the
                // right reading of both: the safe answer is the one that changes nothing.
                if (answer == ContentDialogResult.None)
                    return;

                // A refused save has already put its reason on the status bar. Closing anyway would
                // hide the explanation along with the work.
                if (answer == ContentDialogResult.Primary && !ViewModel.TrySave())
                    return;

                _forceClose = true;
            }
            catch (Exception ex)
            {
                ViewModel.ShowStatus(InfoBarSeverity.Error, "Could not confirm closing", ex.Message);
                return;
            }
            finally
            {
                _dialogOpen = false;
            }

            Close();
        }

        #endregion

        #region Colours

        private void PickForeground_Click(object sender, RoutedEventArgs e) =>
            ShowColorPicker(sender, colorTarget: 0);

        private void PickBackground_Click(object sender, RoutedEventArgs e) =>
            ShowColorPicker(sender, colorTarget: 1);

        private void PickShadow_Click(object sender, RoutedEventArgs e) =>
            ShowColorPicker(sender, colorTarget: 2);

        private void ShowColorPicker(object sender, int colorTarget)
        {
            if (sender is not Button button || button.Tag is not ActionEditModel model)
                return;

            var flyout = new Flyout();

            var picker = new ColorPicker
            {
                IsAlphaEnabled = true,
                IsHexInputVisible = true,
                Color = colorTarget switch
                {
                    0 => model.ForegroundColor,
                    1 => model.BackgroundColor,
                    _ => model.ShadowColor,
                },
            };

            picker.ColorChanged += (s, args) =>
            {
                string hex = IconFactory.ToHex(args.NewColor);
                if (colorTarget == 2)
                    ViewModel.SetShadowColor(model, hex);
                else
                    ViewModel.SetColor(model, colorTarget == 0, hex);
            };

            var reset = new Button
            {
                Content = "Use the default colour",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 12, 0, 0),
            };

            reset.Click += (s, args) =>
            {
                if (colorTarget == 2)
                    ViewModel.ResetShadowColor(model);
                else
                    ViewModel.ResetColor(model, colorTarget == 0);
                flyout.Hide();
            };

            flyout.Content = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = colorTarget switch
                        {
                            0 => "Icon colour",
                            1 => "Button colour",
                            _ => "Shadow colour",
                        },
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Margin = new Thickness(0, 0, 0, 8),
                    },
                    picker,
                    reset,
                },
            };

            flyout.ShowAt(button);
        }

        #endregion

        #region Shortcut recording

        private ActionEditModel? _recordingModel;
        private ToggleButton? _recordingButton;

        private void Record_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton toggle || toggle.Tag is not ActionEditModel model)
                return;

            StopRecording();

            _recordingModel = model;
            _recordingButton = toggle;

            // handledEventsToo: the button and the text boxes around it swallow Tab, Space, Enter
            // and the arrow keys for their own navigation, which are exactly the keys people want
            // to bind. Listening after they are handled is the only way to see them.
            toggle.AddHandler(UIElement.KeyDownEvent, _recordKeyHandler, handledEventsToo: true);
            toggle.LostFocus += Record_LostFocus;
            toggle.Focus(FocusState.Programmatic);

            ViewModel.ShowStatus(InfoBarSeverity.Informational, "Recording",
                "Press the shortcut you want. Esc cancels.");
        }

        private void Record_Unchecked(object sender, RoutedEventArgs e) => StopRecording();

        private void Record_LostFocus(object sender, RoutedEventArgs e) => StopRecording();

        private void StopRecording()
        {
            if (_recordingButton != null)
            {
                _recordingButton.RemoveHandler(UIElement.KeyDownEvent, _recordKeyHandler);
                _recordingButton.LostFocus -= Record_LostFocus;

                if (_recordingButton.IsChecked == true)
                    _recordingButton.IsChecked = false;
            }

            _recordingButton = null;
            _recordingModel = null;
        }

        private void OnRecordKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var model = _recordingModel;
            if (model == null)
                return;

            e.Handled = true;

            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                StopRecording();
                ViewModel.ShowStatus(InfoBarSeverity.Informational, "Cancelled",
                    "The shortcut was left unchanged.");
                return;
            }

            // Ctrl/Alt/Shift arrive as their own KeyDown first; keep waiting for the real key.
            if (ShortcutRecorder.IsModifier(e.Key))
                return;

            var shortcut = ShortcutRecorder.Capture(e.Key);
            if (shortcut == null)
            {
                ViewModel.ShowStatus(InfoBarSeverity.Warning, "Key not supported",
                    $"\"{e.Key}\" cannot be sent by this app. Try another key.");
                return;
            }

            StopRecording();
            ViewModel.ApplyRecordedShortcut(model, shortcut);
        }

        #endregion

        #region File pickers

        private async void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not ActionEditModel model)
                return;

            var file = await PickFileAsync("*");
            if (file != null)
                await ViewModel.SetLaunchTargetAsync(model, file);
        }

        private async void BrowseSvg_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not ActionEditModel model)
                return;

            var file = await PickFileAsync(".svg", ".png", ".ico");
            if (file != null)
                ViewModel.SetIconPath(model, file);
        }

        private async void ImportProfile_Click(object sender, RoutedEventArgs e)
        {
            var file = await PickFileAsync(".xml");
            if (file != null)
                ViewModel.ImportProfile(file);
        }

        private async void ExportProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = "ActionWheel-profile",
                };
                picker.FileTypeChoices.Add("Action Wheel XML profile", new List<string> { ".xml" });

                WinRT.Interop.InitializeWithWindow.Initialize(
                    picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                    ViewModel.ExportProfile(file.Path);
            }
            catch (Exception ex)
            {
                ViewModel.ShowStatus(InfoBarSeverity.Error, "Could not open the file picker", ex.Message);
            }
        }

        private async void BackupAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = $"ActionWheel-backup-{DateTime.Now:yyyy-MM-dd}",
                };
                picker.FileTypeChoices.Add("Action Wheel backup", new List<string> { ".zip" });

                WinRT.Interop.InitializeWithWindow.Initialize(
                    picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

                var file = await picker.PickSaveFileAsync();
                if (file == null)
                    return;

                string appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                    ?? string.Empty;

                if (ConfigBackup.TryExport(file.Path, appVersion, out string error))
                    ViewModel.ShowStatus(InfoBarSeverity.Success, "Backup saved", file.Path);
                else
                    ViewModel.ShowStatus(InfoBarSeverity.Error, "Could not create the backup", error);
            }
            catch (Exception ex)
            {
                ViewModel.ShowStatus(InfoBarSeverity.Error, "Could not create the backup", ex.Message);
            }
        }

        /// <summary>
        /// Restores everything from a backup, showing what it contains first - <see cref="ConfigBackup.TryPreview"/>
        /// reads only the manifest, so this can describe a backup without touching the real files at
        /// all if the user cancels.
        /// </summary>
        private async void RestoreAll_Click(object sender, RoutedEventArgs e)
        {
            if (_dialogOpen || Content is not FrameworkElement root)
                return;

            var path = await PickFileAsync(".zip");
            if (path == null)
                return;

            if (!ConfigBackup.TryPreview(path, out var preview, out string previewError) || preview == null)
            {
                ViewModel.ShowStatus(InfoBarSeverity.Error, "Could not read the backup", previewError);
                return;
            }

            var summary = new StackPanel { Spacing = 6 };
            void AddLine(string text) => summary.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });

            AddLine(preview.ExportedAtUtc == DateTime.MinValue
                ? "Made: unknown date"
                : $"Made: {preview.ExportedAtUtc.ToLocalTime():g}"
                    + (string.IsNullOrWhiteSpace(preview.AppVersion) ? string.Empty : $" (Action Wheel {preview.AppVersion})"));
            AddLine(preview.HasActions ? "Includes the active button configuration" : "Does not include a button configuration");
            AddLine(preview.HasPreferences ? "Includes the trigger and ring appearance settings" : "Does not include trigger/appearance settings");
            AddLine($"{preview.ProfileCount} saved profile(s)");
            AddLine($"{preview.IconCount} extracted icon(s)");
            AddLine("Restoring replaces the matching files on this machine.");

            var dialog = new ContentDialog
            {
                XamlRoot = root.XamlRoot,
                RequestedTheme = root.RequestedTheme,
                Title = "Restore this backup?",
                Content = summary,
                PrimaryButtonText = "Restore",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
            };

            _dialogOpen = true;
            try
            {
                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                    return;

                if (ConfigBackup.TryImport(path, out string importError))
                {
                    ViewModel.ReloadFromDisk();
                    ViewModel.ShowStatus(InfoBarSeverity.Success, "Backup restored",
                        "Every row, profile and appearance setting below now reflects the backup.");
                }
                else
                {
                    ViewModel.ShowStatus(InfoBarSeverity.Error, "Could not restore the backup", importError);
                }
            }
            catch (Exception ex)
            {
                ViewModel.ShowStatus(InfoBarSeverity.Error, "Could not restore the backup", ex.Message);
            }
            finally
            {
                _dialogOpen = false;
            }
        }

        /// <summary>Shows the file picker and returns the chosen path, or null.</summary>
        private async System.Threading.Tasks.Task<string?> PickFileAsync(params string[] fileTypes)
        {
            try
            {
                var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
                foreach (var fileType in fileTypes)
                    picker.FileTypeFilter.Add(fileType);

                // An unpackaged app has no implicit window for the picker to parent itself to.
                WinRT.Interop.InitializeWithWindow.Initialize(
                    picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

                var file = await picker.PickSingleFileAsync();
                return file?.Path;
            }
            catch (Exception ex)
            {
                ViewModel.ShowStatus(InfoBarSeverity.Error, "Could not open the file picker", ex.Message);
                return null;
            }
        }

        #endregion
    }
}
