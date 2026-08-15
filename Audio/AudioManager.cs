// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

// DaemonElite: (Voice Changer) 
// Copyright: (C) 2026 Justin Linwood Ross

// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
using DaemonElite.Models;
using DaemonElite.Services;
using NAudio.Dsp;
using NAudio.Wave;
using System.IO;
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
namespace DaemonElite.Audio;

public sealed class AudioManager : IDisposable
{
    public const int FftLength = 1024;
    private readonly object _sync = new();
    private readonly Complex[] _fft = new Complex[FftLength];
    private readonly float[] _magnitudes = new float[FftLength / 2];
    private readonly string _recordingPath;
    private readonly string _exportDirectory;
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _reader;
    private bool _disposed;
    private bool _recording;
    private bool _playing;

    public bool IsRecording => Volatile.Read(ref _recording);
    public bool IsPlaying => Volatile.Read(ref _playing);
    public bool HasValidRecording => File.Exists(_recordingPath) && new FileInfo(_recordingPath).Length > 44;
    public event EventHandler<EventArgs>? RecordingStopped;
    public event EventHandler<EventArgs>? PlaybackStopped;

    public AudioManager()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Black Star Labs", "DaemonElite");
        Directory.CreateDirectory(root);
        _recordingPath = Path.Combine(root, "session.wav");
        _exportDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "DaemonElite");
        Directory.CreateDirectory(_exportDirectory);
    }

    public float[] GetSmoothedMagnitudesCopy(ref float[]? destination)
    {
        lock (_sync)
        {
            destination ??= new float[_magnitudes.Length];
            if (destination.Length != _magnitudes.Length) destination = new float[_magnitudes.Length];
            Array.Copy(_magnitudes, destination, _magnitudes.Length);
            return destination;
        }
    }

    public float GetPeakLevel()
    {
        lock (_sync)
        {
            float peak = 0;
            for (int i = 0; i < _magnitudes.Length; i++)
                peak = Math.Max(peak, _magnitudes[i]);
            return Math.Clamp(peak * 4f, 0, 1);
        }
    }

    public void StartRecording()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_recording) return;
            ClearSessionUnsafe();
            try
            {
                _waveIn = new WaveInEvent { WaveFormat = new WaveFormat(44100, 16, 1), BufferMilliseconds = 80 };
                _writer = new WaveFileWriter(_recordingPath, _waveIn.WaveFormat);
                _waveIn.DataAvailable += WaveIn_DataAvailable;
                _waveIn.RecordingStopped += WaveIn_RecordingStopped;
                _waveIn.StartRecording();
                _recording = true;
                AppLogger.Info("Recording started.");
            }
            catch
            {
                CleanupRecordingUnsafe();
                throw;
            }
        }
    }

    public void StopRecording()
    {
        lock (_sync)
        {
            if (!_recording) return;
            try { _waveIn?.StopRecording(); }
            catch (Exception ex) { AppLogger.Warning("Recording stop reported an error.", ex); CleanupRecordingUnsafe(); _recording = false; }
        }
    }

    public void StartPlayback(VoicePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!HasValidRecording) throw new InvalidOperationException("No valid recording exists.");
            StopPlaybackUnsafe();
            _reader = new AudioFileReader(_recordingPath);
            ISampleProvider chain = BuildEffectChain(_reader, preset);
            _waveOut = new WaveOutEvent();
            _waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
            _waveOut.Init(chain);
            _waveOut.Play();
            _playing = true;
            AppLogger.Info($"Playback started with {preset.Name}.");
        }
    }

    public void StopPlayback()
    {
        lock (_sync) { StopPlaybackUnsafe(); }
    }

    public string ExportWithEffects(VoicePreset preset, string path)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (!HasValidRecording) throw new InvalidOperationException("No valid recording exists.");
        string directory = Path.GetDirectoryName(path) ?? _exportDirectory;
        Directory.CreateDirectory(directory);
        using var reader = new AudioFileReader(_recordingPath);
        var chain = BuildEffectChain(reader, preset);
        WaveFileWriter.CreateWaveFile16(path, chain);
        AppLogger.Info($"Export completed: {Path.GetFileName(path)}.");
        return path;
    }

    public void ClearSession()
    {
        lock (_sync)
        {
            StopPlaybackUnsafe();
            if (_recording) _waveIn?.StopRecording();
            CleanupRecordingUnsafe();
            _recording = false;
            try { if (File.Exists(_recordingPath)) File.Delete(_recordingPath); } catch (Exception ex) { AppLogger.Warning("Session file could not be removed.", ex); }
        }
    }

    private static ISampleProvider BuildEffectChain(ISampleProvider source, VoicePreset preset)
    {
        ISampleProvider chain = source;
        if (Math.Abs(preset.PitchFactor - 1f) > .01f) chain = new PitchShifterProvider(chain, preset.PitchFactor);
        if (preset.ReverbMix > .01f) chain = new AudioEffects(chain, preset.ReverbMix, preset.ReverbTime);
        if (preset.EchoDelay > 0) chain = new EchoEffectProvider(chain, preset.EchoDelay, preset.EchoFeedback);
        if (preset.Distortion > .01f) chain = new DistortionEffectProvider(chain, preset.Distortion);
        if (preset.TremoloRate > .01f) chain = new TremoloEffectProvider(chain, preset.TremoloRate, preset.TremoloDepth);
        return chain;
    }

    private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            lock (_sync) _writer?.Write(e.Buffer, 0, e.BytesRecorded);
            ProcessFft(e.Buffer, e.BytesRecorded);
        }
        catch (Exception ex) { AppLogger.Warning("Audio callback was unable to process a buffer.", ex); }
    }

    private void ProcessFft(byte[] buffer, int bytes)
    {
        int samples = Math.Min(bytes / 2, FftLength);
        for (int i = 0; i < FftLength; i++)
        {
            short value = i < samples ? (short)((buffer[i * 2 + 1] << 8) | buffer[i * 2]) : (short)0;
            _fft[i].X = (float)(value / 32768f * FastFourierTransform.HammingWindow(i, FftLength));
            _fft[i].Y = 0;
        }
        try
        {
            FastFourierTransform.FFT(true, 10, _fft);
            lock (_sync)
            {
                for (int i = 0; i < _magnitudes.Length; i++)
                {
                    float magnitude = (float)Math.Sqrt(_fft[i].X * _fft[i].X + _fft[i].Y * _fft[i].Y);
                    _magnitudes[i] += (magnitude - _magnitudes[i]) * .28f;
                }
            }
        }
        catch (Exception ex) { AppLogger.Debug($"FFT skipped: {ex.Message}"); }
    }

    private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
    {
        lock (_sync)
        {
            _recording = false;
            CleanupRecordingUnsafe();
            if (e.Exception is not null) AppLogger.Error("Recording stopped with an error.", e.Exception);
            RecordingStopped?.Invoke(this, EventArgs.Empty);
        }
    }

    private void WaveOut_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (_sync)
        {
            _playing = false;
            CleanupPlaybackUnsafe();
            if (e.Exception is not null) AppLogger.Error("Playback stopped with an error.", e.Exception);
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }
    }

    private void StopPlaybackUnsafe()
    {
        if (_waveOut is null && _reader is null) return;
        try { _waveOut?.Stop(); } catch { }
        CleanupPlaybackUnsafe();
        _playing = false;
    }

    private void CleanupRecordingUnsafe()
    {
        try { _writer?.Dispose(); } catch { }
        try { _waveIn?.Dispose(); } catch { }
        _writer = null;
        _waveIn = null;
    }

    private void CleanupPlaybackUnsafe()
    {
        try { _waveOut?.Dispose(); } catch { }
        try { _reader?.Dispose(); } catch { }
        _waveOut = null;
        _reader = null;
    }

    private void ClearSessionUnsafe()
    {
        StopPlaybackUnsafe();
        CleanupRecordingUnsafe();
        try { if (File.Exists(_recordingPath)) File.Delete(_recordingPath); } catch { }
    }

    private void ThrowIfDisposed()
    {
        if (!_disposed)
        {
            return;
        }
        throw new ObjectDisposedException(nameof(AudioManager));
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            try { _waveIn?.StopRecording(); } catch { }
            StopPlaybackUnsafe();
            CleanupRecordingUnsafe();
            CleanupPlaybackUnsafe();
        }
        GC.SuppressFinalize(this);
    }
}
