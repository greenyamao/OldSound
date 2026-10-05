using System;
using OldSound.Core.Audio;
using OldSound.Core.Dsp;

namespace OldSound.Core.Pipeline;

/// <summary>
/// Главный конвейер цифровой обработки сигналов (DSP Pipeline).
/// Связывает все аппаратные этапы: ресемплинг SPU, сжатие Sony ADPCM,
/// интерполяцию ЦАП Гаусса и модуль магнитной компакт-кассеты.
/// </summary>
public static class RetroAudioPipeline
{
    public static AudioBuffer Process(AudioBuffer input, AudioPreset preset)
    {
        int channels = input.Channels;
        int inSampleRate = input.SampleRate;
        int targetVoiceRate = preset.SpuVoiceRate;
        int outSampleRate = preset.OutputSampleRate > 0 ? preset.OutputSampleRate : inSampleRate;

        // Обработка SPU тракта для каждого канала
        short[][] processedChannels = new short[channels][];
        int finalLength = 0;

        for (int ch = 0; ch < channels; ch++)
        {
            short[] pcm16 = input.ToPcm16(ch);

            short[] spuOutput;

            if (preset.EnableAdpcm)
            {
                var adpcm = new SonyAdpcm();

                if (preset.EnableGaussian)
                {
                    // Полный аутентичный тракт:
                    // 1. Ресемплинг в частоту голоса SPU
                    // 2. Сжатие в 4-битный VAG и декодирование
                    // 3. Воспроизведение через аппаратную гауссову интерполяцию ЦАП
                    short[] voicePcm;
                    if (inSampleRate != targetVoiceRate)
                    {
                        voicePcm = ResampleLinear(pcm16, inSampleRate, targetVoiceRate);
                    }
                    else
                    {
                        voicePcm = pcm16;
                    }

                    short[] adpcmPcm = adpcm.ProcessPcm(voicePcm);

                    // Воспроизводим через Гаусс на выходной частоте
                    spuOutput = SpuGaussianInterpolator.Process(adpcmPcm, targetVoiceRate, targetVoiceRate, outSampleRate);
                }
                else
                {
                    // Только сырое ADPCM квантование без гауссова фильтра
                    short[] voicePcm = (inSampleRate != targetVoiceRate)
                        ? ResampleLinear(pcm16, inSampleRate, targetVoiceRate)
                        : pcm16;

                    short[] adpcmPcm = adpcm.ProcessPcm(voicePcm);

                    spuOutput = (targetVoiceRate != outSampleRate)
                        ? ResampleLinear(adpcmPcm, targetVoiceRate, outSampleRate)
                        : adpcmPcm;
                }
            }
            else if (preset.EnableGaussian)
            {
                // Только гауссов Low-Pass фильтр ЦАП без ADPCM сжатия
                spuOutput = SpuGaussianInterpolator.Process(pcm16, inSampleRate, targetVoiceRate, outSampleRate);
            }
            else
            {
                // Ресемплинг без консольной деградации
                spuOutput = (inSampleRate != outSampleRate)
                    ? ResampleLinear(pcm16, inSampleRate, outSampleRate)
                    : pcm16;
            }

            processedChannels[ch] = spuOutput;
            if (ch == 0)
                finalLength = spuOutput.Length;
            else
                finalLength = Math.Min(finalLength, spuOutput.Length);
        }

        // Создаем выходной буфер
        var outBuffer = new AudioBuffer(channels, outSampleRate, finalLength);
        for (int ch = 0; ch < channels; ch++)
        {
            outBuffer.FromPcm16(ch, processedChannels[ch].AsSpan(0, finalLength));
        }

        // Этап кассетного насыщения
        if (preset.EnableTape)
        {
            var tape = new CassetteTapeSimulator(preset.TapeSettings);

            if (channels >= 2)
            {
                tape.Process(outBuffer.GetChannelSpan(0), outBuffer.GetChannelSpan(1), outSampleRate);
            }
            else
            {
                // Моно: обрабатываем в буфере
                Span<float> mono = outBuffer.GetChannelSpan(0);
                tape.Process(mono, mono, outSampleRate);
            }
        }

        return outBuffer;
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
