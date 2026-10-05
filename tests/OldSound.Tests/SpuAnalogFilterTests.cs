using System;
using OldSound.Core.Dsp;
using Xunit;

namespace OldSound.Tests;

public class SpuAnalogFilterTests
{
    [Fact]
    public void Filter_AttenuatesHighFrequenciesSteeply()
    {
        int sampleRate = 44100;
        int length = 44100; // 1 second
        float cutoff = 10000f; // 10 kHz

        var filter = new SpuAnalogFilter(cutoff, sampleRate);

        // Generate 1 kHz tone (passband) and 18 kHz tone (stopband)
        float[] tone1k = new float[length];
        float[] tone18k = new float[length];

        for (int i = 0; i < length; i++)
        {
            tone1k[i] = MathF.Sin(2f * MathF.PI * 1000f * i / sampleRate);
            tone18k[i] = MathF.Sin(2f * MathF.PI * 18000f * i / sampleRate);
        }

        filter.ProcessMono(tone1k);
        filter.Reset();
        filter.ProcessMono(tone18k);

        // Discard first 500 samples for filter transient
        float rms1k = CalculateRms(tone1k.AsSpan(500));
        float rms18k = CalculateRms(tone18k.AsSpan(500));

        // 1 kHz should pass with almost no attenuation (~0.707)
        Assert.True(rms1k > 0.65f, $"1kHz tone was attenuated too much: {rms1k}");

        // 18 kHz (well above 10 kHz with 3 poles) should be attenuated heavily (> 15 dB down)
        Assert.True(rms18k < 0.15f, $"18kHz tone was not attenuated enough: {rms18k}");
    }

    [Fact]
    public void NoiseFloor_GeneratesAudibleAcousticFog()
    {
        var noise = new SpuNoiseFloor();
        float[] left = new float[44100];
        float[] right = new float[44100];

        noise.ProcessStereo(left, right, 1.0f);

        float rmsL = CalculateRms(left);
        float rmsR = CalculateRms(right);

        // Check noise is present and within calibrated console range (-60 dB to -45 dB, i.e. 0.001 to 0.006)
        Assert.True(rmsL > 0.0005f && rmsL < 0.01f, $"Left noise RMS out of expected range: {rmsL}");
        Assert.True(rmsR > 0.0005f && rmsR < 0.01f, $"Right noise RMS out of expected range: {rmsR}");
    }

    [Fact]
    public void BusGlue_SoftSaturatesHotSignals()
    {
        float[] hotSignal = new float[] { 0.5f, 1.0f, 1.5f, 2.0f, -1.5f };
        SpuBusGlue.Process(hotSignal, 1.0f);

        // Should clamp and softly saturate within [-1.0 .. 1.0]
        foreach (float s in hotSignal)
        {
            Assert.True(s >= -1.05f && s <= 1.05f, $"Sample exceeded soft saturation bound: {s}");
        }
    }

    private static float CalculateRms(ReadOnlySpan<float> span)
    {
        double sum = 0;
        for (int i = 0; i < span.Length; i++)
        {
            sum += span[i] * span[i];
        }
        return (float)Math.Sqrt(sum / span.Length);
    }
}
