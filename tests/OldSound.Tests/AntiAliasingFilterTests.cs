using System;
using OldSound.Core.Dsp;
using Xunit;

namespace OldSound.Tests;

public class AntiAliasingFilterTests
{
    [Fact]
    public void DesignBlackmanHarrisFir_SumEqualsOne()
    {
        float[] h = AntiAliasingFilter.DesignBlackmanHarrisFir(44100, 10000f);
        Assert.Equal(129, h.Length);

        float sum = 0f;
        for (int i = 0; i < h.Length; i++) sum += h[i];

        Assert.Equal(1.0f, sum, 4);
    }

    [Fact]
    public void Filter_PassesLowFrequencies_AndSuppressesHighFrequencies()
    {
        int sampleRate = 44100;
        int length = 44100; // 1 second
        float cutoff = 10000f;

        // 1 kHz sine wave
        short[] lowFreq = new short[length];
        for (int i = 0; i < length; i++)
        {
            lowFreq[i] = (short)(10000 * MathF.Sin(2f * MathF.PI * 1000f * i / sampleRate));
        }

        // 16 kHz sine wave (which would alias into 6.05 kHz if not filtered!)
        short[] highFreq = new short[length];
        for (int i = 0; i < length; i++)
        {
            highFreq[i] = (short)(10000 * MathF.Sin(2f * MathF.PI * 16000f * i / sampleRate));
        }

        short[] lowFiltered = AntiAliasingFilter.Apply(lowFreq, sampleRate, cutoff);
        short[] highFiltered = AntiAliasingFilter.Apply(highFreq, sampleRate, cutoff);

        // Calculate RMS in the steady state (middle portion to avoid boundary transient)
        int testStart = 1000;
        int testEnd = length - 1000;
        double lowInRms = 0, lowOutRms = 0;
        double highInRms = 0, highOutRms = 0;

        for (int i = testStart; i < testEnd; i++)
        {
            lowInRms += lowFreq[i] * lowFreq[i];
            lowOutRms += lowFiltered[i] * lowFiltered[i];
            highInRms += highFreq[i] * highFreq[i];
            highOutRms += highFiltered[i] * highFiltered[i];
        }

        lowInRms = Math.Sqrt(lowInRms / (testEnd - testStart));
        lowOutRms = Math.Sqrt(lowOutRms / (testEnd - testStart));
        highInRms = Math.Sqrt(highInRms / (testEnd - testStart));
        highOutRms = Math.Sqrt(highOutRms / (testEnd - testStart));

        float lowGainDb = 20f * MathF.Log10((float)(lowOutRms / lowInRms));
        float highGainDb = 20f * MathF.Log10((float)(Math.Max(1e-9, highOutRms) / highInRms));

        // 1 kHz must pass with practically 0 dB change
        Assert.InRange(lowGainDb, -0.2f, 0.2f);

        // 16 kHz must be attenuated by at least 55 dB (>500x suppression, near 16-bit quantization floor)
        Assert.True(highGainDb < -55f, $"16 kHz attenuation was only {highGainDb:F1} dB, expected < -55 dB");
    }
}
