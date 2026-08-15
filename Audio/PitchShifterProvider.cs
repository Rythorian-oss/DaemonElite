// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

// DaemonElite: (Voice Changer) 
// Copyright: (C) 2026 Justin Linwood Ross

// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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
