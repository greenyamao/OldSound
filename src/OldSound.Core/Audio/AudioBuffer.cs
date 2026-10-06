using System;

namespace OldSound.Core.Audio;

/// <summary>
/// Multi-channel audio buffer with normalized float samples [-1.0f, +1.0f].
/// </summary>
public sealed class AudioBuffer
{
    public int Channels { get; }
    public int SampleRate { get; }
    public int LengthSamples { get; }

    // Channel x samples: [channel][sampleIndex]
    private readonly float[][] _channelData;

    public AudioBuffer(int channels, int sampleRate, int lengthSamples)
    {
        if (channels < 1 || channels > 8)
            throw new ArgumentOutOfRangeException(nameof(channels), "Supported channel count is 1 to 8.");
        if (sampleRate < 4000 || sampleRate > 192000)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate is outside supported range.");
        if (lengthSamples < 0)
            throw new ArgumentOutOfRangeException(nameof(lengthSamples), "Buffer length cannot be negative.");

        Channels = channels;
        SampleRate = sampleRate;
        LengthSamples = lengthSamples;
        _channelData = new float[channels][];
        for (int c = 0; c < channels; c++)
        {
            _channelData[c] = new float[lengthSamples];
        }
    }

    public Span<float> GetChannelSpan(int channel)
    {
        if (channel < 0 || channel >= Channels)
            throw new ArgumentOutOfRangeException(nameof(channel));
        return _channelData[channel].AsSpan();
    }

    public float[] GetChannelArray(int channel)
    {
        if (channel < 0 || channel >= Channels)
            throw new ArgumentOutOfRangeException(nameof(channel));
        return _channelData[channel];
    }

    /// <summary>
    /// Converts a channel to a 16-bit PCM array [-32768, 32767] with optional headroom gain scaling.
    /// </summary>
    public short[] ToPcm16(int channel, float gain = 1.0f)
    {
        var src = GetChannelSpan(channel);
        var dst = new short[LengthSamples];
        for (int i = 0; i < LengthSamples; i++)
        {
            float val = src[i] * gain * 32767.0f;
            if (val > 32767.0f) val = 32767.0f;
            else if (val < -32768.0f) val = -32768.0f;
            dst[i] = (short)MathF.Round(val);
        }
        return dst;
    }

    /// <summary>
    /// Populates a channel from a 16-bit PCM array with optional inverse gain scaling.
    /// </summary>
    public void FromPcm16(int channel, ReadOnlySpan<short> pcm, float gain = 1.0f)
    {
        var dst = GetChannelSpan(channel);
        int count = Math.Min(dst.Length, pcm.Length);
        float factor = gain / 32768.0f;
        for (int i = 0; i < count; i++)
        {
            dst[i] = pcm[i] * factor;
        }
    }

    /// <summary>
    /// Creates a deep copy of the buffer.
    /// </summary>
    public AudioBuffer Clone()
    {
        var copy = new AudioBuffer(Channels, SampleRate, LengthSamples);
        for (int c = 0; c < Channels; c++)
        {
            _channelData[c].CopyTo(copy._channelData[c], 0);
        }
        return copy;
    }
}
