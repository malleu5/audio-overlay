using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Collections.Specialized;
using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Vorbis;

namespace AudioOverlayApp;

public partial class MainWindow : Window
{
    private const int HOTKEY_ID = 9000;
    private uint currentModifiers = 0x0002 | 0x0004; // MOD_CTRL | MOD_SHIFT
    private uint currentKey = 0x51; // VK_Q
    private const uint MOD_CTRL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_ALT = 0x0001;
    
    private string settingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AudioOverlay", "settings.txt");
    
    private string hotkeyFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AudioOverlay", "hotkey.txt");
    
    private string currentFolderPath = "";
    private List<string> audioExtensions = new List<string> { ".mp3", ".wav", ".ogg", ".flac", ".m4a", ".aac", ".wma", ".opus", ".aiff", ".alac" };
    private List<string> allAudioFiles = new List<string>();
    private bool isSettingsPanelVisible = false;
    private IWavePlayer? waveOutDevice = null;
    private WaveStream? audioFileReader = null;
    private Button? currentPlayButton = null;
    private bool isPlaying = false;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public MainWindow()
    {
        InitializeComponent();
        InitializeUI();
        LoadSettings();
        LoadHotkeySettings();
        
        Loaded += (s, e) =>
        {
            var helper = new WindowInteropHelper(this);
            RegisterHotKey(helper.Handle, HOTKEY_ID, currentModifiers, currentKey);

            // Load audio files after window is shown to improve startup time
            if (!string.IsNullOrEmpty(currentFolderPath) && Directory.Exists(currentFolderPath))
                LoadAudioFiles();
        };

        Closing += (s, e) =>
        {
            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID);
            
            // Cleanup audio resources
            waveOutDevice?.Stop();
            waveOutDevice?.Dispose();
            audioFileReader?.Dispose();
        };
    }

    private void InitializeUI()
    {
        Width = 320;
        Height = 450;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Transparent;
        Topmost = true;
        AllowsTransparency = true;

        var mainGrid = new Grid();
        Content = mainGrid;

        var border = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromRgb(47, 49, 54)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(32, 34, 37)),
            BorderThickness = new Thickness(1)
        };
        mainGrid.Children.Add(border);

        var contentGrid = new Grid { Margin = new Thickness(0) };
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        border.Child = contentGrid;

        var headerBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(32, 34, 37)),
            CornerRadius = new CornerRadius(12, 12, 0, 0),
            Cursor = Cursors.SizeAll
        };
        Grid.SetRow(headerBorder, 0);
        contentGrid.Children.Add(headerBorder);

        headerBorder.MouseLeftButtonDown += (s, e) => DragMove();

        var headerGrid = new Grid
        {
            Margin = new Thickness(15, 0, 15, 0)
        };
        headerBorder.Child = headerGrid;

        var titleText = new TextBlock
        {
            Text = "Audio Overlay",
            Foreground = Brushes.White,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        headerGrid.Children.Add(titleText);

        var buttonStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        headerGrid.Children.Add(buttonStack);

        var settingsButton = new Button
        {
            Content = "⚙",
            Width = 30,
            Height = 30,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(163, 166, 170)),
            BorderThickness = new Thickness(0),
            FontSize = 16,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 5, 0),
            ToolTip = "Settings"
        };
        settingsButton.Style = CreateIconButtonStyle();
        settingsButton.Click += ToggleSettings_Click;
        buttonStack.Children.Add(settingsButton);

        var hideButton = new Button
        {
            Content = "−",
            Width = 30,
            Height = 30,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(163, 166, 170)),
            BorderThickness = new Thickness(0),
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 5, 0),
            ToolTip = "Hide (Use hotkey to show again)"
        };
        hideButton.Style = CreateIconButtonStyle();
        hideButton.Click += (s, e) => Visibility = Visibility.Collapsed;
        buttonStack.Children.Add(hideButton);

        var closeButton = new Button
        {
            Content = "✕",
            Width = 30,
            Height = 30,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(163, 166, 170)),
            BorderThickness = new Thickness(0),
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Cursor = Cursors.Hand,
            ToolTip = "Quit Application"
        };
        closeButton.Style = CreateCloseButtonStyle();
        closeButton.Click += (s, e) => Application.Current.Shutdown();
        buttonStack.Children.Add(closeButton);

        var selectButton = new Button
        {
            Content = "Select Folder",
            Margin = new Thickness(15, 10, 15, 10),
            Padding = new Thickness(15, 8, 15, 8),
            Background = new SolidColorBrush(Color.FromRgb(88, 101, 242)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize = 13,
            FontWeight = FontWeights.Medium,
            Cursor = Cursors.Hand
        };
        selectButton.Style = CreateButtonStyle();
        selectButton.Click += SelectFolder_Click;
        Grid.SetRow(selectButton, 1);
        contentGrid.Children.Add(selectButton);

        var settingsPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(40, 43, 48)),
            Padding = new Thickness(15, 10, 15, 10),
            Margin = new Thickness(15, 0, 15, 10),
            CornerRadius = new CornerRadius(6),
            Visibility = Visibility.Collapsed,
            MaxHeight = 300
        };
        Grid.SetRow(settingsPanel, 2);
        contentGrid.Children.Add(settingsPanel);
        RegisterName("SettingsPanel", settingsPanel);

        var settingsScrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        settingsPanel.Child = settingsScrollViewer;

        var settingsStack = new StackPanel();
        settingsScrollViewer.Content = settingsStack;

        var settingsTitle = new TextBlock
        {
            Text = "Settings",
            Foreground = Brushes.White,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        settingsStack.Children.Add(settingsTitle);

        var autoStartPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 5, 0, 5)
        };
        settingsStack.Children.Add(autoStartPanel);

        var autoStartCheck = new CheckBox
        {
            IsChecked = IsAutoStartEnabled(),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        autoStartCheck.Checked += (s, e) => SetAutoStart(true);
        autoStartCheck.Unchecked += (s, e) => SetAutoStart(false);
        autoStartPanel.Children.Add(autoStartCheck);

        var autoStartLabel = new TextBlock
        {
            Text = "Start with Windows",
            Foreground = new SolidColorBrush(Color.FromRgb(220, 221, 222)),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        autoStartPanel.Children.Add(autoStartLabel);

        var hotkeyPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 5)
        };
        settingsStack.Children.Add(hotkeyPanel);

        var hotkeyLabel = new TextBlock
        {
            Text = "Hotkey:",
            Foreground = new SolidColorBrush(Color.FromRgb(220, 221, 222)),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        hotkeyPanel.Children.Add(hotkeyLabel);

        var hotkeyButton = new Button
        {
            Content = GetHotkeyDisplayText(),
            Padding = new Thickness(12, 6, 12, 6),
            Background = new SolidColorBrush(Color.FromRgb(64, 68, 75)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize = 11,
            Cursor = Cursors.Hand
        };
        hotkeyButton.Style = CreateButtonStyle();
        hotkeyButton.Click += HotkeyButton_Click;
        hotkeyPanel.Children.Add(hotkeyButton);
        RegisterName("HotkeyButton", hotkeyButton);

        var hotkeyHint = new TextBlock
        {
            Text = "Click to change hotkey, then press your desired key combination",
            Foreground = new SolidColorBrush(Color.FromRgb(163, 166, 170)),
            FontSize = 10,
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        settingsStack.Children.Add(hotkeyHint);

        var hotkeyInfo = new TextBlock
        {
            Text = "Press Escape to cancel",
            Foreground = new SolidColorBrush(Color.FromRgb(163, 166, 170)),
            FontSize = 10,
            Margin = new Thickness(0, 3, 0, 0)
        };
        settingsStack.Children.Add(hotkeyInfo);

        var searchBox = new TextBox
        {
            Margin = new Thickness(15, 0, 15, 10),
            Padding = new Thickness(10, 8, 10, 8),
            Background = new SolidColorBrush(Color.FromRgb(64, 68, 75)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize = 12,
            Text = "Search audio files..."
        };
        searchBox.Style = CreateTextBoxStyle();
        searchBox.GotFocus += (s, e) =>
        {
            if (searchBox.Text == "Search audio files...")
            {
                searchBox.Text = "";
                searchBox.Foreground = Brushes.White;
            }
        };
        searchBox.LostFocus += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(searchBox.Text))
            {
                searchBox.Text = "Search audio files...";
                searchBox.Foreground = new SolidColorBrush(Color.FromRgb(163, 166, 170));
            }
        };
        searchBox.TextChanged += (s, e) => FilterAudioFiles(searchBox.Text);
        searchBox.Foreground = new SolidColorBrush(Color.FromRgb(163, 166, 170));
        Grid.SetRow(searchBox, 3);
        contentGrid.Children.Add(searchBox);

        RegisterName("SearchBox", searchBox);

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(10, 10, 10, 10)
        };
        Grid.SetRow(scrollViewer, 4);
        contentGrid.Children.Add(scrollViewer);

        var audioFilesPanel = new StackPanel
        {
            Name = "AudioFilesPanel"
        };
        scrollViewer.Content = audioFilesPanel;

        RegisterName("AudioFilesPanel", audioFilesPanel);
    }

    private Style CreateButtonStyle()
    {
        var style = new Style(typeof(Button));
        
        var template = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.Name = "border";
        factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        factory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(contentFactory);

        template.VisualTree = factory;

        var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, 
            new SolidColorBrush(Color.FromRgb(71, 82, 196)), "border"));
        template.Triggers.Add(hoverTrigger);

        style.Setters.Add(new Setter(Button.TemplateProperty, template));
        return style;
    }

    private Style CreatePlayButtonStyle()
    {
        var style = new Style(typeof(Button));
        
        var template = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.Name = "border";
        factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        factory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(contentFactory);

        template.VisualTree = factory;

        var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, 
            new SolidColorBrush(Color.FromRgb(71, 82, 196)), "border"));
        template.Triggers.Add(hoverTrigger);

        style.Setters.Add(new Setter(Button.TemplateProperty, template));
        return style;
    }

    private Style CreateQuitButtonStyle()
    {
        var style = new Style(typeof(Button));
        
        var template = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.Name = "border";
        factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        factory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(contentFactory);

        template.VisualTree = factory;

        var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, 
            new SolidColorBrush(Color.FromRgb(200, 50, 53)), "border"));
        template.Triggers.Add(hoverTrigger);

        style.Setters.Add(new Setter(Button.TemplateProperty, template));
        return style;
    }

    private Style CreateCloseButtonStyle()
    {
        var style = new Style(typeof(Button));
        
        var template = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.Name = "border";
        factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(contentFactory);

        template.VisualTree = factory;

        var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, 
            new SolidColorBrush(Color.FromRgb(220, 53, 69)), "border"));
        hoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
        template.Triggers.Add(hoverTrigger);

        style.Setters.Add(new Setter(Button.TemplateProperty, template));
        return style;
    }

    private Style CreateIconButtonStyle()
    {
        var style = new Style(typeof(Button));
        
        var template = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.Name = "border";
        factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(contentFactory);

        template.VisualTree = factory;

        var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, 
            new SolidColorBrush(Color.FromRgb(79, 84, 92)), "border"));
        hoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
        template.Triggers.Add(hoverTrigger);

        style.Setters.Add(new Setter(Button.TemplateProperty, template));
        return style;
    }

    private Style CreateTextBoxStyle()
    {
        var style = new Style(typeof(TextBox));
        
        var template = new ControlTemplate(typeof(TextBox));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.Name = "border";
        factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(TextBox.BackgroundProperty));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        factory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(TextBox.PaddingProperty));

        var scrollFactory = new FrameworkElementFactory(typeof(ScrollViewer));
        scrollFactory.Name = "PART_ContentHost";
        scrollFactory.SetValue(ScrollViewer.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(scrollFactory);

        template.VisualTree = factory;
        style.Setters.Add(new Setter(TextBox.TemplateProperty, template));
        return style;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = PresentationSource.FromVisual(this) as HwndSource;
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            Visibility = Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            if (Visibility == Visibility.Visible)
                Activate();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ToggleSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsPanel = FindName("SettingsPanel") as Border;
        if (settingsPanel == null) return;

        isSettingsPanelVisible = !isSettingsPanelVisible;
        settingsPanel.Visibility = isSettingsPanelVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a folder containing audio files",
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            currentFolderPath = dialog.SelectedPath;
            SaveSettings();
            LoadAudioFiles();
        }
    }

    private void PlayAudio_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button?.Tag is string filePath && File.Exists(filePath))
        {
            try
            {
                // If clicking the same button that's currently playing, pause/resume
                if (button == currentPlayButton && waveOutDevice != null)
                {
                    if (isPlaying)
                    {
                        waveOutDevice.Pause();
                        button.Content = "▶";
                        button.Background = new SolidColorBrush(Color.FromRgb(88, 101, 242));
                        isPlaying = false;
                    }
                    else
                    {
                        waveOutDevice.Play();
                        button.Content = "⏸";
                        button.Background = new SolidColorBrush(Color.FromRgb(67, 181, 129));
                        isPlaying = true;
                    }
                    return;
                }

                // Reset previous button immediately
                if (currentPlayButton != null && currentPlayButton != button)
                {
                    currentPlayButton.Content = "▶";
                    currentPlayButton.Background = new SolidColorBrush(Color.FromRgb(88, 101, 242));
                }

                // Stop and dispose current playback
                waveOutDevice?.Stop();
                waveOutDevice?.Dispose();
                audioFileReader?.Dispose();

                var ext = Path.GetExtension(filePath).ToLower();

                // OPUS: Open with default media player (VLC, etc.)
                if (ext == ".opus")
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                    
                    button.Background = new SolidColorBrush(Color.FromRgb(67, 181, 129));
                    button.ToolTip = "Playing in external player";
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(1)
                    };
                    timer.Tick += (s, args) =>
                    {
                        button.Background = new SolidColorBrush(Color.FromRgb(88, 101, 242));
                        button.ToolTip = "Play audio";
                        timer.Stop();
                    };
                    timer.Start();
                    return;
                }

                // All other formats play in-app
                if (ext == ".ogg")
                {
                    audioFileReader = new VorbisWaveReader(filePath);
                }
                else if (ext == ".mp3" || ext == ".wav")
                {
                    audioFileReader = new AudioFileReader(filePath);
                }
                else
                {
                    // Try MediaFoundation for other formats (FLAC, M4A, AAC, WMA, etc.)
                    audioFileReader = new MediaFoundationReader(filePath);
                }

                waveOutDevice = new WaveOutEvent();
                waveOutDevice.Init(audioFileReader);
                waveOutDevice.Play();
                
                currentPlayButton = button;
                isPlaying = true;
                button.Content = "⏸";
                button.Background = new SolidColorBrush(Color.FromRgb(67, 181, 129));
                
                // Listen for playback stopped event
                waveOutDevice.PlaybackStopped += (s, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (button == currentPlayButton)
                        {
                            button.Content = "▶";
                            button.Background = new SolidColorBrush(Color.FromRgb(88, 101, 242));
                            isPlaying = false;
                        }
                        // Dispose resources to allow replaying the same sound
                        waveOutDevice?.Dispose();
                        audioFileReader?.Dispose();
                        waveOutDevice = null;
                        audioFileReader = null;
                    });
                };
            }
            catch (Exception ex)
            {
                button.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69));
                button.Content = "✕";
                button.ToolTip = $"Playback error: {ex.Message}";
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                timer.Tick += (s, args) =>
                {
                    button.Content = "▶";
                    button.Background = new SolidColorBrush(Color.FromRgb(88, 101, 242));
                    button.ToolTip = "Play audio";
                    timer.Stop();
                };
                timer.Start();
            }
        }
    }

    private void LoadAudioFiles()
    {
        var panel = FindName("AudioFilesPanel") as StackPanel;
        if (panel == null) return;

        panel.Children.Clear();

        if (string.IsNullOrEmpty(currentFolderPath) || !Directory.Exists(currentFolderPath))
        {
            var noFilesText = new TextBlock
            {
                Text = "No folder selected",
                Foreground = new SolidColorBrush(Color.FromRgb(163, 166, 170)),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            panel.Children.Add(noFilesText);
            allAudioFiles.Clear();
            return;
        }

        allAudioFiles = Directory.GetFiles(currentFolderPath)
            .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLower()))
            .OrderBy(f => Path.GetFileName(f))
            .ToList();

        if (allAudioFiles.Count == 0)
        {
            var noFilesText = new TextBlock
            {
                Text = "No audio files found",
                Foreground = new SolidColorBrush(Color.FromRgb(163, 166, 170)),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            panel.Children.Add(noFilesText);
            return;
        }

        DisplayAudioFiles(allAudioFiles);
    }

    private void FilterAudioFiles(string searchText)
    {
        if (searchText == "Search audio files..." || string.IsNullOrWhiteSpace(searchText))
        {
            DisplayAudioFiles(allAudioFiles);
            return;
        }

        var filtered = allAudioFiles
            .Where(f => Path.GetFileName(f).ToLower().Contains(searchText.ToLower()))
            .ToList();

        DisplayAudioFiles(filtered);
    }

    private void DisplayAudioFiles(List<string> audioFiles)
    {
        var panel = FindName("AudioFilesPanel") as StackPanel;
        if (panel == null) return;

        panel.Children.Clear();

        if (audioFiles.Count == 0)
        {
            var noFilesText = new TextBlock
            {
                Text = "No matching files",
                Foreground = new SolidColorBrush(Color.FromRgb(163, 166, 170)),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            panel.Children.Add(noFilesText);
            return;
        }

        foreach (var filePath in audioFiles)
        {
            var fileName = Path.GetFileName(filePath);
            
            var fileGrid = new Grid();
            fileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fileGrid.Margin = new Thickness(5, 3, 5, 3);
            
            var button = new Button
            {
                Content = fileName,
                Padding = new Thickness(12, 8, 12, 8),
                Background = new SolidColorBrush(Color.FromRgb(64, 68, 75)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 12,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Tag = filePath
            };
            button.Style = CreateFileButtonStyle();
            button.Click += AudioFile_Click;
            Grid.SetColumn(button, 0);
            fileGrid.Children.Add(button);

            var playButton = new Button
            {
                Content = "▶",
                Width = 35,
                Height = 35,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(88, 101, 242)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                Cursor = Cursors.Hand,
                Margin = new Thickness(5, 0, 0, 0),
                Tag = filePath,
                ToolTip = "Play audio"
            };
            playButton.Style = CreatePlayButtonStyle();
            playButton.Click += PlayAudio_Click;
            Grid.SetColumn(playButton, 1);
            fileGrid.Children.Add(playButton);

            panel.Children.Add(fileGrid);
        }
    }

    private Style CreateFileButtonStyle()
    {
        var style = new Style(typeof(Button));
        
        var template = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.Name = "border";
        factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        factory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, 
            new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(contentFactory);

        template.VisualTree = factory;

        var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, 
            new SolidColorBrush(Color.FromRgb(79, 84, 92)), "border"));
        template.Triggers.Add(hoverTrigger);

        style.Setters.Add(new Setter(Button.TemplateProperty, template));
        return style;
    }



    private void AudioFile_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button?.Tag is string filePath && File.Exists(filePath))
        {
            CopyFileToClipboard(filePath);
            
            var originalBg = button.Background;
            button.Background = new SolidColorBrush(Color.FromRgb(67, 181, 129));
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            timer.Tick += (s, args) =>
            {
                button.Background = originalBg;
                timer.Stop();
            };
            timer.Start();
        }
    }

    private void CopyFileToClipboard(string filePath)
    {
        var fileDropList = new StringCollection();
        fileDropList.Add(filePath);
        Clipboard.Clear();
        Clipboard.SetFileDropList(fileDropList);
    }

    private void SaveSettings()
    {
        try
        {
            var dir = Path.GetDirectoryName(settingsFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(settingsFile, currentFolderPath);
        }
        catch { }
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(settingsFile))
            {
                currentFolderPath = File.ReadAllText(settingsFile).Trim();
                // Delay LoadAudioFiles to after window is loaded
            }
        }
        catch { }
    }

    private string GetHotkeyDisplayText()
    {
        var parts = new List<string>();
        if ((currentModifiers & MOD_CTRL) != 0) parts.Add("Ctrl");
        if ((currentModifiers & MOD_ALT) != 0) parts.Add("Alt");
        if ((currentModifiers & MOD_SHIFT) != 0) parts.Add("Shift");
        
        var keyName = GetKeyName(currentKey);
        parts.Add(keyName);
        
        return string.Join(" + ", parts);
    }

    private string GetKeyName(uint vkCode)
    {
        if (vkCode >= 0x41 && vkCode <= 0x5A) return ((char)vkCode).ToString(); // A-Z
        if (vkCode >= 0x30 && vkCode <= 0x39) return ((char)vkCode).ToString(); // 0-9
        if (vkCode >= 0x70 && vkCode <= 0x87) return "F" + (vkCode - 0x6F); // F1-F24
        
        return vkCode switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Esc",
            0x20 => "Space",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2D => "Insert",
            0x2E => "Delete",
            _ => $"Key{vkCode}"
        };
    }

    private void HotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button == null) return;

        button.Content = "Press key combination...";
        button.Background = new SolidColorBrush(Color.FromRgb(88, 101, 242));
        
        KeyEventHandler? handler = null;
        handler = (s, args) =>
        {
            if (args.Key == Key.Escape)
            {
                if (handler != null) KeyDown -= handler;
                button.Content = GetHotkeyDisplayText();
                button.Background = new SolidColorBrush(Color.FromRgb(64, 68, 75));
                return;
            }

            if (args.Key == Key.LeftCtrl || args.Key == Key.RightCtrl ||
                args.Key == Key.LeftAlt || args.Key == Key.RightAlt ||
                args.Key == Key.LeftShift || args.Key == Key.RightShift ||
                args.Key == Key.System)
            {
                return;
            }

            uint newModifiers = 0;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) newModifiers |= MOD_CTRL;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) newModifiers |= MOD_ALT;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) newModifiers |= MOD_SHIFT;

            if (newModifiers == 0)
            {
                button.Content = "Must include Ctrl, Alt, or Shift";
                button.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69));
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (ts, te) =>
                {
                    button.Content = GetHotkeyDisplayText();
                    button.Background = new SolidColorBrush(Color.FromRgb(64, 68, 75));
                    timer.Stop();
                };
                timer.Start();
                if (handler != null) KeyDown -= handler;
                return;
            }

            uint newKey = (uint)KeyInterop.VirtualKeyFromKey(args.Key);
            
            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID);
            
            if (RegisterHotKey(helper.Handle, HOTKEY_ID, newModifiers, newKey))
            {
                currentModifiers = newModifiers;
                currentKey = newKey;
                SaveHotkeySettings();
                button.Content = GetHotkeyDisplayText();
                button.Background = new SolidColorBrush(Color.FromRgb(67, 181, 129));
                
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                timer.Tick += (ts, te) =>
                {
                    button.Background = new SolidColorBrush(Color.FromRgb(64, 68, 75));
                    timer.Stop();
                };
                timer.Start();
            }
            else
            {
                RegisterHotKey(helper.Handle, HOTKEY_ID, currentModifiers, currentKey);
                button.Content = "Hotkey already in use";
                button.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69));
                
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (ts, te) =>
                {
                    button.Content = GetHotkeyDisplayText();
                    button.Background = new SolidColorBrush(Color.FromRgb(64, 68, 75));
                    timer.Stop();
                };
                timer.Start();
            }

            if (handler != null) KeyDown -= handler;
            args.Handled = true;
        };
        
        KeyDown += handler;
    }

    private void SaveHotkeySettings()
    {
        try
        {
            var dir = Path.GetDirectoryName(hotkeyFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(hotkeyFile, $"{currentModifiers},{currentKey}");
        }
        catch { }
    }

    private void LoadHotkeySettings()
    {
        try
        {
            if (File.Exists(hotkeyFile))
            {
                var data = File.ReadAllText(hotkeyFile).Trim().Split(',');
                if (data.Length == 2)
                {
                    if (uint.TryParse(data[0], out uint mods) && uint.TryParse(data[1], out uint key))
                    {
                        currentModifiers = mods;
                        currentKey = key;
                    }
                }
            }
        }
        catch { }
    }

    private bool IsAutoStartEnabled()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
            {
                return key?.GetValue("AudioOverlay") != null;
            }
        }
        catch
        {
            return false;
        }
    }

    private void SetAutoStart(bool enable)
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (enable)
                {
                    var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    if (exePath.EndsWith(".dll"))
                    {
                        exePath = exePath.Replace(".dll", ".exe");
                    }
                    key?.SetValue("AudioOverlay", $"\"{exePath}\"");
                }
                else
                {
                    key?.DeleteValue("AudioOverlay", false);
                }
            }
        }
        catch { }
    }
}