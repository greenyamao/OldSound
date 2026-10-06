using System;

namespace OldSound.Core.Audio;

/// <summary>
/// Представляет многоканальный аудио-буфер с нормализованными float-сэмплами [-1.0f, +1.0f].
/// </summary>
public sealed class AudioBuffer
{
    public int Channels { get; }
    public int SampleRate { get; }
    public int LengthSamples { get; }

    // Канал x сэмплы: [channel][sampleIndex]
    private readonly float[][] _channelData;

    public AudioBuffer(int channels, int sampleRate, int lengthSamples)
    {
        if (channels < 1 || channels > 8)
            throw new ArgumentOutOfRangeException(nameof(channels), "Поддерживается от 1 до 8 каналов.");
        if (sampleRate < 4000 || sampleRate > 192000)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Частота дискретизации вне допустимого диапазона.");
        if (lengthSamples < 0)
            throw new ArgumentOutOfRangeException(nameof(lengthSamples), "Длина буфера не может быть отрицательной.");

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
    /// Конвертирует канал в 16-битный PCM массив [-32768, 32767] с опциональным масштабированием (headroom gain).
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
    /// Заполняет канал из 16-битного PCM массива с опциональным обратным масштабированием.
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
    /// Создает глубокую копию буфера.
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
