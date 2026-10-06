using System;
using OldSound.Core.Dsp;
using Xunit;

namespace OldSound.Tests;

public class SonyAdpcmTests
{
    [Fact]
    public void DecodeBlock_SilentBlock_ProducesZeroes()
    {
        var adpcm = new SonyAdpcm();
        byte[] block = new byte[16]; // filter 0, shift 0, all zeroes
        short[] output = new short[28];

        adpcm.DecodeBlock(block, output);

        foreach (var sample in output)
        {
            Assert.Equal(0, sample);
        }
    }

    [Fact]
    public void ProcessPcm_ConstantSignal_ConvergesAccurately()
    {
        var adpcm = new SonyAdpcm();
        short[] original = new short[56]; // 2 blocks of 28 samples each
        Array.Fill(original, (short)5000);

        short[] processed = adpcm.ProcessPcm(original);

        Assert.Equal(56, processed.Length);

        // In the 2nd block (after history stabilization) 4-bit ADPCM error is minimal
        for (int i = 28; i < 56; i++)
        {
            Assert.InRange(processed[i], 4900, 5100);
        }
    }

    [Fact]
    public void ProcessPcm_SineWave_PreservesWaveShape()
    {
        var adpcm = new SonyAdpcm();
        short[] sine = new short[280];
        for (int i = 0; i < sine.Length; i++)
        {
            sine[i] = (short)(MathF.Sin(2.0f * MathF.PI * 440.0f * i / 44100.0f) * 20000.0f);
        }

        short[] processed = adpcm.ProcessPcm(sine);

        Assert.Equal(sine.Length, processed.Length);

        // Verify correlation (signal should remain close to original sine wave plus quantization noise)
        double errorEnergy = 0;
        double signalEnergy = 0;
        for (int i = 0; i < sine.Length; i++)
        {
            double diff = sine[i] - processed[i];
            errorEnergy += diff * diff;
            signalEnergy += (double)sine[i] * sine[i];
        }

        double snr = 10.0 * Math.Log10(signalEnergy / errorEnergy);
        // For 4-bit ADPCM, typical SNR is 15..28 dB
        Assert.True(snr > 15.0, $"SNR is too low: {snr:F2} dB");
    }

    [Fact]
    public void Predictors_MatchPlayStationHardwareSpecs()
    {
        Assert.Equal(5, SonyAdpcm.Predictors.Length);
        Assert.Equal((0, 0), SonyAdpcm.Predictors[0]);
        Assert.Equal((60, 0), SonyAdpcm.Predictors[1]);
        Assert.Equal((115, -52), SonyAdpcm.Predictors[2]);
        Assert.Equal((98, -55), SonyAdpcm.Predictors[3]);
        Assert.Equal((122, -60), SonyAdpcm.Predictors[4]);
    }
}
