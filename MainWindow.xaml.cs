using DaemonElite.Audio;
using DaemonElite.Models;
using DaemonElite.Services;
using Microsoft.Win32;
using NAudio.Wave;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
#region SYSTEM INITIALIZATION : BLACK STAR PROJECT
/// <summary>
/// Core application node for the Black Star Research Facility.
/// </summary>
/// <remarks>
/// <code>
/// ========================================================================
///   ____  _        _    ____ _  __  ____ _____  _    ____  
///  | __ )| |      / \  / ___| |/ / / ___|_   _|/ \  |  _ \ 
///  |  _ \| |     / _ \| |   | ' /  \___ \ | | / _ \ | |_) |
///  | |_) | |___ / ___ \ |___| . \   ___) || |/ ___ \|  _ < 
///  |____/|_____/_/   \_\____|_|\_\ |____/ |_/_/   \_\_| \_\
///                                                          
///              R E S E A R C H   F A C I L I T Y           
///                                                          
///             [ LOCATION: ICELAND ]            
/// ========================================================================
/// </code>
/// </remarks>
#endregion

namespace DaemonElite;

public partial class MainWindow : Window
{
    private AudioManager? _audio;
    private IReadOnlyList<VoicePreset> _presets = [];
    private VoicePreset? _selectedPreset;
    private bool _hasRecording;
    private bool _isClosing;
    private readonly Action<LogEntry>? _logHandler;
    private readonly DispatcherTimer _meterTimer;

    public MainWindow()
    {
        InitializeComponent();
        _logHandler = OnLogEmitted;
        AppLogger.LogEmitted += _logHandler;
        _meterTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(80) };
        _meterTimer.Tick += (_, _) => UpdateSignalMeter();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _audio = new AudioManager();
            _audio.RecordingStopped += Audio_RecordingStopped;
            _audio.PlaybackStopped += Audio_PlaybackStopped;
            Visualizer.AudioSource = _audio;

            _presets = VoicePreset.GetBuiltInPresets();
            PresetList.ItemsSource = _presets;
            PresetList.SelectedIndex = 0;
            PresetCountText.Text = $"{_presets.Count:00}";
            _meterTimer.Start();

            SetStatus("READY", "Microphone channel available.");
            AppLogger.Info("Audio engine initialized at 44.1 kHz / mono capture.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Audio engine initialization failed.", ex);
            SetStatus("ENGINE ERROR", "Unable to initialize the capture device.");
            MessageBox.Show(
                "DaemonElite could not initialize the microphone.\n\nCheck Windows microphone permissions and that an input device is connected.",
                "Audio engine unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PresetList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (PresetList.SelectedItem is not VoicePreset preset) return;
        _selectedPreset = preset;
        SelectedPresetText.Text = preset.Name.ToUpperInvariant();
        EffectText.Text = preset.Description;
        AppLogger.Info($"Profile armed: {preset.Name} / {preset.Category}.");
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureAudio()) return;
        try
        {
            _audio!.StartRecording();
            SetStatus("RECORDING", "Capturing signal from input channel 01.");
            AppLogger.Info("Capture started.");
            UpdateControls();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Capture start failed.", ex);
            ShowAudioError("Recording could not start. Check the selected Windows input device.", ex);
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureAudio()) return;

        bool wasRecording = _audio!.IsRecording;
        bool wasPlaying = _audio.IsPlaying;

        if (wasRecording)
        {
            SetStatus("FINALIZING", "Finishing the WAV container before it can be played or exported.");
            _audio.StopRecording();
        }

        if (wasPlaying)
        {
            _audio.StopPlayback();
            if (!wasRecording)
                SetStatus(_hasRecording ? "CAPTURE STORED" : "READY", "Playback stopped.");
        }

        _hasRecording = _audio.HasValidRecording;
        if (!wasRecording && !wasPlaying)
            SetStatus(_hasRecording ? "CAPTURE STORED" : "READY", "Nothing was active.");

        UpdateControls();
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureAudio() || _selectedPreset is null) return;
        if (!_hasRecording)
        {
            MessageBox.Show("Record a signal before playing.", "Nothing to play", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _audio!.StartPlayback(_selectedPreset);
            SetStatus("PLAYING", "Monitoring processed signal through output.");
            AppLogger.Info("Playback started.");
            UpdateControls();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Playback start failed.", ex);
            ShowAudioError("Playback could not start.", ex);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureAudio() || _selectedPreset is null) return;
        if (!_hasRecording)
        {
            MessageBox.Show("Record a signal before exporting.", "Nothing to export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export DaemonElite WAV",
            Filter = "WAV audio (*.wav)|*.wav",
            FileName = $"daemonelite_{_selectedPreset.Name.ToLowerInvariant().Replace(' ', '_')}_{DateTime.Now:yyyyMMdd_HHmmss}.wav",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
        };

        if (dialog.ShowDialog(this) != true) return;
        try
        {
            string output = _audio!.ExportWithEffects(_selectedPreset, dialog.FileName);
            SetStatus("EXPORTED", Path.GetFileName(output));
            MessageBox.Show($"Your processed signal was exported to:\n\n{output}", "Export complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Export failed.", ex);
            ShowAudioError("The signal could not be exported.", ex);
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureAudio()) return;
        _audio!.ClearSession();
        _hasRecording = false;
        SetStatus("READY", "Session cleared. Capture a new signal when ready.");
        UpdateControls();
        AppLogger.Info("Session cleared.");
    }

    private void Audio_RecordingStopped(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing) return;
            var stoppedArgs = e as StoppedEventArgs;
            bool hasError = stoppedArgs?.Exception != null;
            _hasRecording = !hasError && _audio?.HasValidRecording == true;
            SetStatus(!hasError && _hasRecording ? "CAPTURE STORED" : "CAPTURE ERROR",
                !hasError ? "Signal is ready for playback or export." : "The input device reported an error while stopping.");
            UpdateControls();
        });
    }

    private void Audio_PlaybackStopped(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing) return;
            var stoppedArgs = e as StoppedEventArgs;
            bool hasError = stoppedArgs?.Exception != null;
            SetStatus(!hasError ? (_hasRecording ? "CAPTURE STORED" : "READY") : "PLAYBACK ERROR",
                !hasError ? "Playback finished." : "The output device reported an error.");
            UpdateControls();
        });
    }

    private void OnLogEmitted(LogEntry entry)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing) return;
            ConsoleText.Text += entry + Environment.NewLine;
            ConsoleScroll.ScrollToEnd();
        });
    }

    private bool EnsureAudio()
    {
        if (_audio is not null && _selectedPreset is not null) return true;
        _ = MessageBox.Show("The audio engine is still starting. Please try again in a moment.", "DaemonElite", button: MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }

    private void UpdateControls()
    {
        bool recording = _audio?.IsRecording == true;
        bool playing = _audio?.IsPlaying == true;
        bool locked = recording || playing;
        RecordButton.IsEnabled = !locked;
        StopButton.IsEnabled = locked;
        PlayButton.IsEnabled = !locked && _hasRecording;
        ExportButton.IsEnabled = !locked && _hasRecording;
        ClearButton.IsEnabled = !locked && _hasRecording;
        PresetList.IsEnabled = !locked;
        ConsoleStatus.Text = recording ? "DAEMONELITE / CAPTURING" : playing ? "DAEMONELITE / MONITORING" : "DAEMONELITE / READY";
    }

    private void UpdateSignalMeter()
    {
        if (_audio is null) return;
        double level = _audio.GetPeakLevel();
        SignalMeter.Value = level;
        PeakText.Text = level < .001 ? "-∞ dB" : $"{20 * Math.Log10(level):0.0} dB";
    }

    private void SetStatus(string label, string detail)
    {
        StatusText.Text = label;
        ConsoleStatus.Text = $"DAEMONELITE / {label}";
        AppLogger.Info(detail);
    }

    private void ShowAudioError(string userMessage, Exception ex)
    {
        SetStatus("ERROR", "See the system console for details.");
        MessageBox.Show($"{userMessage}\n\nTechnical detail: {ex.Message}", "DaemonElite audio error", MessageBoxButton.OK, MessageBoxImage.Error);
        UpdateControls();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closed(object? sender, EventArgs e)
    {
        _isClosing = true;
        _meterTimer.Stop();
        if (_logHandler is not null) AppLogger.LogEmitted -= _logHandler;
        if (_audio is not null)
        {
            _audio.RecordingStopped -= Audio_RecordingStopped;
            _audio.PlaybackStopped -= Audio_PlaybackStopped;
            _audio.Dispose();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_audio?.IsRecording == true || _audio?.IsPlaying == true)
        {
            var result = MessageBox.Show("Audio is still active. Stop the current session and exit?", "Exit DaemonElite", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }
        base.OnClosing(e);
    }
}
