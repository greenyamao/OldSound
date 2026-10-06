using System;
using OldSound.Core.Audio;
using OldSound.Core.Dsp;

namespace OldSound.Core.Pipeline;

/// <summary>
/// Master DSP pipeline.
/// Implements authentic signal paths of 5th-generation consoles (PS1 SPU, CD-XA, 3DO Opera SDX2)
/// and compact cassette tape with guaranteed musical gain staging.
/// </summary>
public static class RetroAudioPipeline
{
    public static AudioBuffer Process(AudioBuffer input, AudioPreset preset)
    {
        int channels = input.Channels;
        int inSampleRate = input.SampleRate;
        int targetVoiceRate = preset.SpuVoiceRate;
        int outSampleRate = preset.OutputSampleRate > 0 ? preset.OutputSampleRate : inSampleRate;

        // 1. Channel processing: anti-aliasing, encode/decode, DAC interpolation
        short[][] processedChannels = new short[channels][];
        int finalLength = 0;
        const float HeadroomPreGain = 0.85f; // -1.4 dBFS headroom preventing ADPCM predictor overflow and filter clipping

        for (int ch = 0; ch < channels; ch++)
        {
            short[] pcm16 = input.ToPcm16(ch, HeadroomPreGain);
            short[] voicePcm;

            // Anti-aliasing and resampling to console voice rate
            if (inSampleRate != targetVoiceRate)
            {
                // Pre-filtering: if PreFilterCutoffHz <= 0, filter is bypassed, producing raw decimation crunch
                short[] filtered = (preset.PreFilterCutoffHz > 0f && preset.PreFilterCutoffHz < 20000f)
                    ? ApplyPreFilter(pcm16, inSampleRate, preset.PreFilterCutoffHz)
                    : pcm16;
                voicePcm = (preset.PreFilterCutoffHz <= 0f || preset.Interpolation == InterpolationType.RawSteps)
                    ? ResampleRawSteps(filtered, inSampleRate, targetVoiceRate)
                    : ResampleLinear(filtered, inSampleRate, targetVoiceRate);
            }
            else
            {
                voicePcm = pcm16;
            }

            // Codec
            short[] decodedPcm;
            switch (preset.Codec)
            {
                case AudioCodecType.SonyAdpcm:
                    var adpcm = new SonyAdpcm();
                    decodedPcm = adpcm.ProcessPcm(voicePcm, 0.0f, preset.AuthenticAdpcmMode);
                    break;

                case AudioCodecType.Sdx2_3Do:
                    decodedPcm = Sdx2Codec.ProcessPcm(voicePcm);
                    break;

                case AudioCodecType.Bypass:
                default:
                    decodedPcm = voicePcm;
                    break;
            }

            // DAC reconstruction and interpolation to output sample rate (44.1 kHz)
            short[] reconstructedOutput;
            switch (preset.Interpolation)
            {
                case InterpolationType.Gaussian4Point:
                    reconstructedOutput = SpuGaussianInterpolator.Process(decodedPcm, targetVoiceRate, targetVoiceRate, outSampleRate);
                    break;

                case InterpolationType.Linear3DoHalfRate:
                    if (targetVoiceRate == 22050 && outSampleRate == 44100)
                    {
                        reconstructedOutput = Sdx2Codec.Upsample3DoHalfRate(decodedPcm);
                    }
                    else
                    {
                        reconstructedOutput = (targetVoiceRate != outSampleRate)
                            ? ResampleLinear(decodedPcm, targetVoiceRate, outSampleRate)
                            : decodedPcm;
                    }
                    break;

                case InterpolationType.RawSteps:
                    reconstructedOutput = (targetVoiceRate != outSampleRate)
                        ? ResampleRawSteps(decodedPcm, targetVoiceRate, outSampleRate)
                        : decodedPcm;
                    break;

                case InterpolationType.Linear:
                    reconstructedOutput = (targetVoiceRate != outSampleRate)
                        ? ResampleLinear(decodedPcm, targetVoiceRate, outSampleRate)
                        : decodedPcm;
                    break;

                case InterpolationType.Bypass:
                default:
                    reconstructedOutput = (targetVoiceRate != outSampleRate)
                        ? ResampleRawSteps(decodedPcm, targetVoiceRate, outSampleRate)
                        : decodedPcm;
                    break;
            }

            processedChannels[ch] = reconstructedOutput;
            if (ch == 0)
                finalLength = reconstructedOutput.Length;
            else
                finalLength = Math.Min(finalLength, reconstructedOutput.Length);
        }

        // Create output buffer with normalized float [-1.0 .. 1.0]
        var outBuffer = new AudioBuffer(channels, outSampleRate, finalLength);
        for (int ch = 0; ch < channels; ch++)
        {
            outBuffer.FromPcm16(ch, processedChannels[ch].AsSpan(0, finalLength), 1.0f / HeadroomPreGain);
        }

        // 2. Hardware analog output DAC filter (PS1 3-pole or 3DO 2-pole Sallen-Key)
        // When FilterCutoffHz >= 21900 Hz, the analog low-pass filter is bypassed (wide open pass-through).
        if (preset.EnableAnalogFilter && preset.FilterCutoffHz > 0 && preset.FilterCutoffHz < 21900f)
        {
            var filter = new SpuAnalogFilter(preset.FilterCutoffHz, outSampleRate, preset.FilterTopology);
            if (channels >= 2)
            {
                filter.ProcessStereo(outBuffer.GetChannelSpan(0), outBuffer.GetChannelSpan(1));
            }
            else
            {
                filter.ProcessMono(outBuffer.GetChannelSpan(0));
            }
        }

        // 2b. High-frequency Air / Presence boost or cut (-6 dB .. +12 dB)
        if (MathF.Abs(preset.TrebleBoostDb) > 0.05f)
        {
            ApplyTrebleShelf(outBuffer, 6500f, preset.TrebleBoostDb, outSampleRate);
        }

        // 3. Master analog output noise floor
        // Added at the very end of the pipeline at 44.1 kHz,
        // providing an authentic analog noise bed across the soundscape.
        if (preset.SpuNoiseLevel > 0.0001f)
        {
            var noise = new SpuNoiseFloor();
            if (channels >= 2)
            {
                noise.ProcessStereo(
                    outBuffer.GetChannelSpan(0), 
                    outBuffer.GetChannelSpan(1), 
                    preset.SpuNoiseLevel, 
                    preset.NoiseProfile, 
                    outBuffer.SampleRate,
                    preset.NoiseTone,
                    preset.NoiseHumLevel);
            }
            else
            {
                noise.ProcessMono(
                    outBuffer.GetChannelSpan(0), 
                    preset.SpuNoiseLevel, 
                    preset.NoiseProfile, 
                    outBuffer.SampleRate,
                    preset.NoiseTone,
                    preset.NoiseHumLevel);
            }
        }

        // 4. Automatic safety headroom calibration (-1.4 dBFS)
        // Ensures zero digital clipping in all output formats (WAV 16-bit / MP3 / WASAPI)
        EnsureSafetyHeadroom(outBuffer, 0.85f);

        return outBuffer;
    }

    /// <summary>
    /// Blackman-Harris FIR anti-aliasing pre-filter (129 taps).
    /// Suppresses frequencies above Nyquist by >60..80 dB, eliminating foldover aliasing
    /// while preserving the natural musicality of the source audio.
    /// </summary>
    private static short[] ApplyPreFilter(ReadOnlySpan<short> input, int sampleRate, float cutoffHz)
    {
        return AntiAliasingFilter.Apply(input, sampleRate, cutoffHz);
    }

    /// <summary>
    /// Automatic safety headroom scaling.
    /// If signal peaks exceed the safe threshold (-1.4 dBFS / 0.85),
    /// scales the buffer proportionally down to prevent clipping and flat-tops.
    /// </summary>
    public static float EnsureSafetyHeadroom(AudioBuffer buffer, float targetCeiling = 0.85f)
    {
        float maxPeak = 0f;
        for (int ch = 0; ch < buffer.Channels; ch++)
        {
            var span = buffer.GetChannelSpan(ch);
            for (int i = 0; i < span.Length; i++)
            {
                float abs = MathF.Abs(span[i]);
                if (abs > maxPeak) maxPeak = abs;
            }
        }

        if (maxPeak > targetCeiling && maxPeak > 1e-6f)
        {
            float scale = targetCeiling / maxPeak;
            for (int ch = 0; ch < buffer.Channels; ch++)
            {
                var span = buffer.GetChannelSpan(ch);
                for (int i = 0; i < span.Length; i++)
                {
                    span[i] *= scale;
                }
            }
            return scale;
        }

        // Safety clamp against floating-point inaccuracies
        for (int ch = 0; ch < buffer.Channels; ch++)
        {
            var span = buffer.GetChannelSpan(ch);
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = Math.Clamp(span[i], -targetCeiling, targetCeiling);
            }
        }

        return 1.0f;
    }

    private static short[] ResampleLinear(ReadOnlySpan<short> input, int inRate, int outRate)
    {
        if (inRate == outRate)
            return input.ToArray();

        long outCount = (long)Math.Ceiling((double)input.Length * outRate / inRate);
        var output = new short[outCount];
        double step = (double)inRate / outRate;
        double pos = 0.0;

        for (int i = 0; i < outCount; i++)
        {
            int idx = (int)Math.Floor(pos);
            double frac = pos - idx;

            short s0 = (idx < input.Length) ? input[idx] : input[^1];
            short s1 = (idx + 1 < input.Length) ? input[idx + 1] : s0;

            int interp = (int)Math.Round(s0 + frac * (s1 - s0));
            output[i] = (short)Math.Clamp(interp, short.MinValue, short.MaxValue);

            pos += step;
        }

        return output;
    }

    private static short[] ResampleRawSteps(ReadOnlySpan<short> input, int inRate, int outRate)
    {
        if (inRate == outRate)
            return input.ToArray();

        long outCount = (long)Math.Ceiling((double)input.Length * outRate / inRate);
        var output = new short[outCount];
        double step = (double)inRate / outRate;
        double pos = 0.0;

        for (int i = 0; i < outCount; i++)
        {
            int idx = (int)Math.Floor(pos);
            if (idx >= input.Length) idx = input.Length - 1;
            output[i] = input[idx];
            pos += step;
        }

        return output;
    }

    private static void ApplyTrebleShelf(AudioBuffer buffer, float frequency, float gainDb, int sampleRate)
    {
        float a = MathF.Pow(10f, gainDb / 40f);
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2f * 0.7071f);

        float b0 = a * ((a + 1f) + (a - 1f) * cosW0 + 2f * MathF.Sqrt(a) * alpha);
        float b1 = -2f * a * ((a - 1f) + (a + 1f) * cosW0);
        float b2 = a * ((a + 1f) + (a - 1f) * cosW0 - 2f * MathF.Sqrt(a) * alpha);
        float a0 = (a + 1f) - (a - 1f) * cosW0 + 2f * MathF.Sqrt(a) * alpha;
        float a1 = 2f * ((a - 1f) - (a + 1f) * cosW0);
        float a2 = (a + 1f) - (a - 1f) * cosW0 - 2f * MathF.Sqrt(a) * alpha;

        float cB0 = b0 / a0;
        float cB1 = b1 / a0;
        float cB2 = b2 / a0;
        float cA1 = a1 / a0;
        float cA2 = a2 / a0;

        for (int ch = 0; ch < buffer.Channels; ch++)
        {
            var span = buffer.GetChannelSpan(ch);
            float x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < span.Length; i++)
            {
                float x0 = span[i];
                float y0 = cB0 * x0 + cB1 * x1 + cB2 * x2 - cA1 * y1 - cA2 * y2;
                x2 = x1;
                x1 = x0;
                y2 = y1;
                y1 = y0;
                span[i] = y0;
            }
        }
    }
}
