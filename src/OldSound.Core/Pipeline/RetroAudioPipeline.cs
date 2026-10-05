using System;
using OldSound.Core.Audio;
using OldSound.Core.Dsp;

namespace OldSound.Core.Pipeline;

/// <summary>
/// Главный конвейер цифровой обработки сигналов (DSP Pipeline).
/// Реализует аутентичный тракт консолей 5-го поколения (PS1 SPU, CD-XA, 3DO Opera SDX2)
/// и аналоговой компакт-кассеты с гарантированным музыкальным гейн-стейджингом.
/// </summary>
public static class RetroAudioPipeline
{
    public static AudioBuffer Process(AudioBuffer input, AudioPreset preset)
    {
        int channels = input.Channels;
        int inSampleRate = input.SampleRate;
        int targetVoiceRate = preset.SpuVoiceRate;
        int outSampleRate = preset.OutputSampleRate > 0 ? preset.OutputSampleRate : inSampleRate;

        // 1. Поканальная обработка: антиалиасинг, кодирование/декодирование, интерполяция ЦАП
        short[][] processedChannels = new short[channels][];
        int finalLength = 0;

        for (int ch = 0; ch < channels; ch++)
        {
            short[] pcm16 = input.ToPcm16(ch);
            short[] voicePcm;

            // Антиалиасинг и ресемплинг в частоту консольного голоса
            if (inSampleRate != targetVoiceRate)
            {
                float preCutoff = preset.PreFilterCutoffHz > 0f
                    ? preset.PreFilterCutoffHz
                    : Math.Min(10500f, targetVoiceRate * 0.45f);

                // Префильтрация перед понижением частоты дискретизации
                short[] filtered = ApplyPreFilter(pcm16, inSampleRate, preCutoff);
                voicePcm = ResampleLinear(filtered, inSampleRate, targetVoiceRate);
            }
            else
            {
                voicePcm = pcm16;
            }

            // Кодек
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

            // Реконструкция и интерполяция ЦАП на выходную сетку частот (44.1 кГц)
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

                case InterpolationType.Linear:
                    reconstructedOutput = (targetVoiceRate != outSampleRate)
                        ? ResampleLinear(decodedPcm, targetVoiceRate, outSampleRate)
                        : decodedPcm;
                    break;

                case InterpolationType.Bypass:
                default:
                    reconstructedOutput = decodedPcm;
                    break;
            }

            processedChannels[ch] = reconstructedOutput;
            if (ch == 0)
                finalLength = reconstructedOutput.Length;
            else
                finalLength = Math.Min(finalLength, reconstructedOutput.Length);
        }

        // Создаем выходной буфер с нормализованными float [-1.0 .. 1.0]
        var outBuffer = new AudioBuffer(channels, outSampleRate, finalLength);
        for (int ch = 0; ch < channels; ch++)
        {
            outBuffer.FromPcm16(ch, processedChannels[ch].AsSpan(0, finalLength));
        }

        // 2. Мягкая аналоговая сатурация шины суммирования (Bus Glue)
        if (preset.BusGlue > 0.01f)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                SpuBusGlue.Process(outBuffer.GetChannelSpan(ch), preset.BusGlue);
            }
        }

        // 3. Аналоговый фон резистивной матрицы ЦАП (Noise Floor ~ -66 dBFS)
        if (preset.SpuNoiseLevel > 0.001f)
        {
            var noise = new SpuNoiseFloor();
            if (channels >= 2)
            {
                noise.ProcessStereo(outBuffer.GetChannelSpan(0), outBuffer.GetChannelSpan(1), preset.SpuNoiseLevel);
            }
            else
            {
                noise.ProcessMono(outBuffer.GetChannelSpan(0), preset.SpuNoiseLevel);
            }
        }

        // 4. Аппаратный аналоговый выходной фильтр ЦАП (PS1 3-pole или 3DO 2-pole Sallen-Key)
        if (preset.EnableAnalogFilter && preset.FilterCutoffHz > 0)
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

        // 5. Модуль магнитной компакт-кассеты
        if (preset.EnableTape)
        {
            var tape = new CassetteTapeSimulator(preset.TapeSettings);
            if (channels >= 2)
            {
                tape.Process(outBuffer.GetChannelSpan(0), outBuffer.GetChannelSpan(1), outSampleRate);
            }
            else
            {
                Span<float> mono = outBuffer.GetChannelSpan(0);
                tape.Process(mono, mono, outSampleRate);
            }
        }

        // 6. Прозрачный мастеринг-лимитер с мягким коленом (-0.2 dBFS ceiling)
        // Предотвращает цифровое срезание пиков в любых форматах (WAV 16-bit / MP3 / AAC)
        ApplyTruePeakCeiling(outBuffer, 0.975f);

        return outBuffer;
    }

    /// <summary>
    /// Предварительный антиалиасинг фильтр 2-го порядка Баттерворта.
    /// </summary>
    private static short[] ApplyPreFilter(ReadOnlySpan<short> input, int sampleRate, float cutoffHz)
    {
        float f0 = Math.Clamp(cutoffHz, 1000f, sampleRate * 0.48f);
        float w0 = 2f * MathF.PI * f0;
        float k = 2f * sampleRate;
        float omega = 2f * sampleRate * MathF.Tan(w0 / (2f * sampleRate));

        float q = 0.70710678f;
        float omega2 = omega * omega;
        float a0 = (k * k) + (k * omega / q) + omega2;

        float b0 = omega2 / a0;
        float b1 = (2f * omega2) / a0;
        float b2 = omega2 / a0;
        float a1 = (2f * omega2 - 2f * k * k) / a0;
        float a2 = ((k * k) - (k * omega / q) + omega2) / a0;

        var output = new short[input.Length];
        float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;

        for (int i = 0; i < input.Length; i++)
        {
            float inVal = input[i];
            float outVal = b0 * inVal + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = inVal;
            y2 = y1; y1 = outVal;

            output[i] = (short)Math.Clamp((int)MathF.Round(outVal), short.MinValue, short.MaxValue);
        }

        return output;
    }

    /// <summary>
    /// Безопасный мягкий лимитер, исключающий интерсемпловые пики и перегруз в WAV/MP3.
    /// </summary>
    private static void ApplyTruePeakCeiling(AudioBuffer buffer, float ceiling = 0.975f)
    {
        float kneeStart = ceiling * 0.92f;
        float kneeWidth = ceiling - kneeStart;

        for (int ch = 0; ch < buffer.Channels; ch++)
        {
            var span = buffer.GetChannelSpan(ch);
            for (int i = 0; i < span.Length; i++)
            {
                float s = span[i];
                if (s > kneeStart)
                {
                    float over = s - kneeStart;
                    span[i] = kneeStart + kneeWidth * MathF.Tanh(over / kneeWidth);
                }
                else if (s < -kneeStart)
                {
                    float over = -s - kneeStart;
                    span[i] = -(kneeStart + kneeWidth * MathF.Tanh(over / kneeWidth));
                }
            }
        }
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
}
