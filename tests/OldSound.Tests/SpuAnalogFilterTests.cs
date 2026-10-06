using System;
using OldSound.Core.Audio;
using OldSound.Core.Dsp;
using OldSound.Core.Pipeline;
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

        // Console profile
        noise.ProcessStereo(left, right, 1.0f, AnalogNoiseProfile.Console);
        float rmsL = CalculateRms(left);
        float rmsR = CalculateRms(right);
        Assert.True(rmsL > 0.001f && rmsL < 0.05f, $"Console left noise RMS out of expected range: {rmsL}");
        Assert.True(rmsR > 0.001f && rmsR < 0.05f, $"Console right noise RMS out of expected range: {rmsR}");

        // Cassette profile
        noise.Reset();
        Array.Clear(left);
        Array.Clear(right);
        noise.ProcessStereo(left, right, 1.0f, AnalogNoiseProfile.Cassette);
        float rmsTapeL = CalculateRms(left);
        float rmsTapeR = CalculateRms(right);
        Assert.True(rmsTapeL > 0.001f && rmsTapeL < 0.05f, $"Tape left noise RMS out of expected range: {rmsTapeL}");
        Assert.True(rmsTapeR > 0.001f && rmsTapeR < 0.05f, $"Tape right noise RMS out of expected range: {rmsTapeR}");

        // Pure hiss profile
        noise.Reset();
        Array.Clear(left);
        Array.Clear(right);
        noise.ProcessStereo(left, right, 1.0f, AnalogNoiseProfile.PureHiss, 44100, tone: 0.5f, humLevel: 0.0f);
        float rmsHissL = CalculateRms(left);
        Assert.True(rmsHissL > 0.001f && rmsHissL < 0.05f, $"Pure hiss RMS out of expected range: {rmsHissL}");
    }

    [Fact]
    public void Pipeline_RawStepsPreservesHighFrequencyTreble()
    {
        // Generate high frequency tone at 5 kHz near Nyquist of 11025 Hz
        int sampleRate = 44100;
        int length = 44100;
        var buffer = new AudioBuffer(1, sampleRate, length);
        var span = buffer.GetChannelSpan(0);
        for (int i = 0; i < length; i++)
        {
            span[i] = MathF.Sin(2f * MathF.PI * 4500f * i / sampleRate);
        }

        var preset = new AudioPreset
        {
            Codec = AudioCodecType.Bypass,
            SpuVoiceRate = 11025,
            Interpolation = InterpolationType.RawSteps,
            PreFilterCutoffHz = 0f,
            EnableAnalogFilter = false,
            FilterCutoffHz = 22000f,
            SpuNoiseLevel = 0f
        };

        var processed = RetroAudioPipeline.Process(buffer, preset);
        float rms = CalculateRms(processed.GetChannelSpan(0));

        // In raw steps without smoothing, RMS should be well-preserved (> 0.45)
        Assert.True(rms > 0.45f, $"Raw steps attenuated 4.5 kHz too heavily: {rms}");
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
