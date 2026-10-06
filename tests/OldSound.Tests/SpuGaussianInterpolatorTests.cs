using System;
using OldSound.Core.Dsp;
using Xunit;

namespace OldSound.Tests;

public class SpuGaussianInterpolatorTests
{
    [Fact]
    public void Table_Length_Is512()
    {
        Assert.Equal(512, SpuGaussianInterpolator.Table.Length);
    }

    [Fact]
    public void Table_FourCoefficientsSum_MatchesHardwareGlitch()
    {
        // In Martin Korth's documentation (nocash psxspx):
        // The sum of coefficient quadruplets for any i in range 00h..FFh is 7F7Fh..7F81h (32639..32641).
        for (int i = 0; i < 256; i++)
        {
            int sum = SpuGaussianInterpolator.Table[0x0FF - i]
                    + SpuGaussianInterpolator.Table[0x1FF - i]
                    + SpuGaussianInterpolator.Table[0x100 + i]
                    + SpuGaussianInterpolator.Table[0x000 + i];

            Assert.InRange(sum, 32639, 32641);
        }
    }

    [Fact]
    public void Interpolate_ConstantDC_ProducesStableOutput()
    {
        short val = 10000;
        // For a DC constant signal, interpolation should produce a value close to the input
        for (int phase = 0; phase < 256; phase += 17)
        {
            short outVal = SpuGaussianInterpolator.Interpolate(val, val, val, val, phase);
            // Due to the sum 32640 / 32768 (~0.996), the value is slightly attenuated (by ~0.4%)
            Assert.InRange(outVal, 9900, 10050);
        }
    }

    [Fact]
    public void Process_HighFrequencySignal_AttenuatesHigherFrequencies()
    {
        // Compare 1 kHz and 18 kHz pass-through across Gaussian DAC path
        int sampleRate = 44100;
        int count = 4410; // 0.1 sec

        short[] tone1k = new short[count];
        short[] tone18k = new short[count];

        for (int i = 0; i < count; i++)
        {
            tone1k[i] = (short)(MathF.Sin(2.0f * MathF.PI * 1000.0f * i / sampleRate) * 15000.0f);
            tone18k[i] = (short)(MathF.Sin(2.0f * MathF.PI * 18000.0f * i / sampleRate) * 15000.0f);
        }

        short[] out1k = SpuGaussianInterpolator.Process(tone1k, sampleRate, sampleRate, sampleRate);
        short[] out18k = SpuGaussianInterpolator.Process(tone18k, sampleRate, sampleRate, sampleRate);

        // Calculate peak amplitude
        float max1k = 0;
        float max18k = 0;
        for (int i = 50; i < count - 50; i++)
        {
            max1k = MathF.Max(max1k, MathF.Abs(out1k[i]));
            max18k = MathF.Max(max18k, MathF.Abs(out18k[i]));
        }

        // PS1 SPU Gaussian filter attenuates 18 kHz relative to 1 kHz at 44.1k by approximately -7 dB (factor ~0.45)
        Assert.True(max18k < max1k * 0.55f, $"Expected 18 kHz attenuation: 1k={max1k}, 18k={max18k}");

        // At 20 kHz attenuation is even stronger
        short[] tone20k = new short[count];
        for (int i = 0; i < count; i++)
        {
            tone20k[i] = (short)(MathF.Sin(2.0f * MathF.PI * 20000.0f * i / sampleRate) * 15000.0f);
        }
        short[] out20k = SpuGaussianInterpolator.Process(tone20k, sampleRate, sampleRate, sampleRate);
        float max20k = 0;
        for (int i = 50; i < count - 50; i++)
        {
            max20k = MathF.Max(max20k, MathF.Abs(out20k[i]));
        }
        Assert.True(max20k < max18k, $"20 kHz ({max20k}) must be attenuated more heavily than 18 kHz ({max18k})");
    }
}
