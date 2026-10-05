using System;
using System.IO;
using OldSound.Core.Audio;
using Xunit;

namespace OldSound.Tests;

public class WavCodecTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    public void WriteAndRead_SineWave_PreservesSignal(int bitsPerSample)
    {
        int sampleRate = 44100;
        int count = 1000;
        var original = new AudioBuffer(2, sampleRate, count);

        var left = original.GetChannelSpan(0);
        var right = original.GetChannelSpan(1);

        for (int i = 0; i < count; i++)
        {
            left[i] = MathF.Sin(2.0f * MathF.PI * 440.0f * i / sampleRate) * 0.8f;
            right[i] = MathF.Cos(2.0f * MathF.PI * 440.0f * i / sampleRate) * 0.8f;
        }

        using var ms = new MemoryStream();
        WavCodec.Write(original, ms, bitsPerSample);

        ms.Position = 0;
        var loaded = WavCodec.Read(ms);

        Assert.Equal(original.Channels, loaded.Channels);
        Assert.Equal(original.SampleRate, loaded.SampleRate);
        Assert.Equal(original.LengthSamples, loaded.LengthSamples);

        var loadedLeft = loaded.GetChannelSpan(0);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(left[i], loadedLeft[i], 0.001f);
        }
    }
}
