// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

// DaemonElite: (Voice Changer) 
// Copyright: (C) 2026 Justin Linwood Ross

// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
// ENGINEER NOTES: Memory Footprint Only Bumped up to ~5.8 MB = (5,796,248 bytes) Across 74,111 object. 
// >>>>>>>>>>>>>>>  This is still a "phenomenally small footprint" for a WPF Desktop Application, having a few hundred kilobytes.
using NAudio.Wave;

namespace DaemonElite.Audio;

internal sealed class DelayLine
{
    private readonly float[] _buffer;
    private readonly int _mask;
    private int _writeIndex;

    public DelayLine(int minimumSize)
    {
        int size = NextPowerOfTwo(Math.Max(minimumSize, 1));
        _buffer = new float[size];
        _mask = size - 1;
    }

    public int Length => _buffer.Length;

    /// <summary>Value written <paramref name="samplesAgo"/> samples in the past.</summary>
    public float this[int samplesAgo] => _buffer[(_writeIndex - samplesAgo) & _mask];

    /// <summary>Writes the current sample and advances the write pointer.</summary>
    public void Advance(float value)
    {
        _buffer[_writeIndex] = value;
        _writeIndex = (_writeIndex + 1) & _mask;
    }

    private static int NextPowerOfTwo(int value)
    {
        value = Math.Max(value, 1);
        int power = 1;
        while (power < value) power <<= 1;
        return power;
    }
}

/// <summary>
/// Shared plumbing for single-source sample-provider effects: null checks,
/// pass-through WaveFormat, and disposal of the upstream source if it owns
/// unmanaged/OS resources (file handles, capture devices, etc).
/// </summary>
public abstract class SampleEffectProviderBase(ISampleProvider source) : ISampleProvider, IDisposable
{
    protected readonly ISampleProvider Source = source ?? throw new ArgumentNullException(nameof(source));
    private bool _disposed;

    public WaveFormat WaveFormat => Source.WaveFormat;

    public abstract int Read(float[] buffer, int offset, int count);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        (Source as IDisposable)?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Combined echo/mix effect. "Time" actually controls the delay tap.</summary>
public sealed class AudioEffects : SampleEffectProviderBase
{
    private readonly DelayLine _delay;
    private readonly int _sampleRate;

    // volatile: Mix/Time may be set from a UI thread while Read() runs on the audio thread.
    private volatile float _mix;
    private volatile float _time;

    public AudioEffects(ISampleProvider source, float mix, float time) : base(source)
    {
        _sampleRate = Source.WaveFormat.SampleRate;
        _delay = new DelayLine(Math.Max(_sampleRate * 2, 1));
        _mix = Math.Clamp(mix, 0f, 1f);
        _time = Math.Clamp(time, 0f, 5f);
    }

    public float Mix
    {
        get => _mix;
        set => _mix = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Delay time in seconds (0–5), now actually used to place the delay tap.</summary>
    public float Time
    {
        get => _time;
        set => _time = Math.Clamp(value, 0f, 5f);
    }

    public override int Read(float[] buffer, int offset, int count)
    {
        int read = Source.Read(buffer, offset, count);
        if (read <= 0) return read;

        // Snapshot once per block so the whole block is processed consistently
        // even if a property changes mid-callback from another thread.
        float mix = _mix;
        float time = _time;
        float feedback = .25f + Math.Clamp(time / 5f, 0f, 1f) * .65f;

        long delaySamplesLong = (long)(time * _sampleRate);
        int delaySamples = (int)Math.Clamp(delaySamplesLong, 1, _delay.Length - 1);

        for (int i = 0; i < read; i++)
        {
            float dry = buffer[offset + i];
            float echo = _delay[delaySamples];
            float wet = dry * mix + echo * feedback;

            buffer[offset + i] = Math.Clamp(dry * (1f - mix * .45f) + wet * .45f, -1f, 1f);
            _delay.Advance(wet);
        }

        return read;
    }
}

/// <summary>Simple feedback echo with an explicit millisecond delay.</summary>
public sealed class EchoEffectProvider : SampleEffectProviderBase
{
    private readonly DelayLine _delay;
    private readonly int _sampleRate;

    private volatile int _delayMs;
    private volatile float _feedback;

    public EchoEffectProvider(ISampleProvider source, int delayMs, float feedback) : base(source)
    {
        _sampleRate = Source.WaveFormat.SampleRate;
        _delay = new DelayLine(Math.Max(_sampleRate * 2, 1));
        _delayMs = Math.Max(delayMs, 0);
        _feedback = Math.Clamp(feedback, 0f, .95f);
    }

    public int DelayMs
    {
        get => _delayMs;
        set => _delayMs = Math.Max(value, 0);
    }

    public float Feedback
    {
        get => _feedback;
        set => _feedback = Math.Clamp(value, 0f, .95f);
    }

    public override int Read(float[] buffer, int offset, int count)
    {
        int read = Source.Read(buffer, offset, count);
        if (read <= 0) return read;

        int delayMs = _delayMs;
        float feedback = _feedback;

        // long math avoids int overflow at high delayMs * high sample rates.
        long delaySamplesLong = (long)delayMs * _sampleRate / 1000;
        int delaySamples = (int)Math.Clamp(delaySamplesLong, 1, _delay.Length - 1);

        for (int i = 0; i < read; i++)
        {
            float dry = buffer[offset + i];
            float delayed = _delay[delaySamples];
            float output = Math.Clamp(dry + delayed * feedback, -1f, 1f);

            buffer[offset + i] = output;
            _delay.Advance(output);
        }

        return read;
    }
}

/// <summary>Tanh-based soft-clip distortion.</summary>
public sealed class DistortionEffectProvider(ISampleProvider source, float drive) : SampleEffectProviderBase(source)
{
    private volatile float _drive = Math.Clamp(drive, 0f, 1f);

    public float Drive
    {
        get => _drive;
        set => _drive = Math.Clamp(value, 0f, 1f);
    }

    public override int Read(float[] buffer, int offset, int count)
    {
        int read = Source.Read(buffer, offset, count);
        if (read <= 0) return read;

        float drive = _drive;
        float gain = 1f + drive * 10f;
        float shape = 1f + drive * 8f;

        for (int i = 0; i < read; i++)
        {
            float sample = buffer[offset + i] * gain * shape;
            buffer[offset + i] = Math.Clamp(MathF.Tanh(sample), -1f, 1f);
        }

        return read;
    }
}

/// <summary>Sine-wave amplitude modulation.</summary>
public sealed class TremoloEffectProvider(ISampleProvider source, float rate, float depth) : SampleEffectProviderBase(source)
{
    private const float TwoPi = 2f * MathF.PI;

    private float _phase; // only ever touched on the audio thread inside Read()
    private volatile float _rate = Math.Max(rate, 0f);
    private volatile float _depth = Math.Clamp(depth, 0f, 1f);

    public float Rate
    {
        get => _rate;
        set => _rate = Math.Max(value, 0f);
    }

    public float Depth
    {
        get => _depth;
        set => _depth = Math.Clamp(value, 0f, 1f);
    }

    public override int Read(float[] buffer, int offset, int count)
    {
        int read = Source.Read(buffer, offset, count);
        if (read <= 0) return read;

        float depth = _depth;
        float increment = TwoPi * _rate / Source.WaveFormat.SampleRate;
        float phase = _phase;

        for (int i = 0; i < read; i++)
        {
            float mod = 1f - depth * .5f + depth * .5f * MathF.Sin(phase);
            buffer[offset + i] *= mod;

            phase += increment;
            if (phase >= TwoPi) phase -= TwoPi;
        }

        _phase = phase;
        return read;
    }
}


/// <summary>
/// Optional wrapper that runs an upstream chain on a dedicated background thread,
/// decoupling potentially expensive effect processing from the real-time audio
/// callback thread. Adds latency equal to <paramref name="bufferDuration"/>, so
/// only use this if your effect chain is heavy enough to risk underruns on the
/// callback thread — the four effects above are not.
///
/// The producer thread is a real, joinable thread with a clean shutdown path
/// (Dispose stops it and waits for exit), so it can't leak past the wrapper's
/// lifetime. All shared state is protected by a single lock; waits are bounded
/// so neither side can spin the CPU or block forever.
/// </summary>
public sealed class BufferedSampleProvider : ISampleProvider, IDisposable
{
    private readonly ISampleProvider _source;
    private readonly float[] _buffer;
    private readonly int _capacity;
    private readonly object _sync = new();
    private readonly Thread _producerThread;
    private readonly ManualResetEventSlim _spaceAvailable = new(true);
    private readonly ManualResetEventSlim _dataAvailable = new(false);

    private volatile bool _stopRequested;
    private int _writePos;
    private int _readPos;
    private int _availableSamples;
    private bool _disposed;

    public BufferedSampleProvider(ISampleProvider source, TimeSpan bufferDuration)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bufferDuration, TimeSpan.Zero);

        int channels = Math.Max(source.WaveFormat.Channels, 1);
        _capacity = Math.Max(
            (int)(bufferDuration.TotalSeconds * source.WaveFormat.SampleRate * channels),
            channels);
        _buffer = new float[_capacity];

        _producerThread = new Thread(ProducerLoop)
        {
            IsBackground = true,
            Name = nameof(BufferedSampleProvider) + "-Producer",
        };
        _producerThread.Start();
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    private void ProducerLoop()
    {
        var scratch = new float[4096];

        while (!_stopRequested)
        {
            _spaceAvailable.Wait();
            if (_stopRequested) break;

            int toRead;
            lock (_sync) { toRead = Math.Min(scratch.Length, _capacity - _availableSamples); }

            if (toRead <= 0)
            {
                _spaceAvailable.Reset();
                continue;
            }

            int read = _source.Read(scratch, 0, toRead);
            if (read <= 0)
            {
                // Upstream has nothing right now — avoid a hot spin loop.
                Thread.Sleep(5);
                continue;
            }

            lock (_sync)
            {
                for (int i = 0; i < read; i++)
                {
                    _buffer[_writePos] = scratch[i];
                    _writePos = (_writePos + 1) % _capacity;
                }
                _availableSamples += read;
                if (_availableSamples >= _capacity)
                    _spaceAvailable.Reset();
            }

            _dataAvailable.Set();
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int total = 0;

        while (total < count)
        {
            int available;
            lock (_sync) { available = _availableSamples; }

            if (available == 0)
            {
                if (_stopRequested) break;
                _dataAvailable.Wait(TimeSpan.FromMilliseconds(50));
                _dataAvailable.Reset();
                continue;
            }

            int toCopy = Math.Min(available, count - total);
            lock (_sync)
            {
                for (int i = 0; i < toCopy; i++)
                {
                    buffer[offset + total + i] = _buffer[_readPos];
                    _readPos = (_readPos + 1) % _capacity;
                }
                _availableSamples -= toCopy;
                _spaceAvailable.Set();
            }

            total += toCopy;
        }

        return total;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _stopRequested = true;
        _spaceAvailable.Set();
        _dataAvailable.Set();
        _producerThread.Join(TimeSpan.FromSeconds(2));

        _spaceAvailable.Dispose();
        _dataAvailable.Dispose();
        (_source as IDisposable)?.Dispose();
    }
}
