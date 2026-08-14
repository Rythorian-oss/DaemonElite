// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

// DaemonElite: (Voice Changer) 
// Copyright: (C) 2026 Justin Linwood Ross

// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DaemonElite.Audio;

/// <summary>
/// Lightweight duration-preserving resampling shifter for monitoring and offline export.
/// Duration-preserving pitch shifter backed by NAudio's SMB granular processor.
/// </summary>
public sealed class PitchShifterProvider : ISampleProvider
{
    private readonly SmbPitchShiftingSampleProvider _processor;

    public PitchShifterProvider(ISampleProvider source, float pitchFactor)
    {
        ArgumentNullException.ThrowIfNull(source);
        _processor = new SmbPitchShiftingSampleProvider(source);
        PitchFactor = pitchFactor;
    }

    public float PitchFactor
    {
        get => _processor.PitchFactor;
        set => _processor.PitchFactor = Math.Clamp(value, .35f, 2.5f);
    }

    public WaveFormat WaveFormat => _processor.WaveFormat;

    public int Read(float[] buffer, int offset, int count) => _processor.Read(buffer, offset, count);
}