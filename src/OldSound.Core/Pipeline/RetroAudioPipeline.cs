using System;
using OldSound.Core.Audio;
using OldSound.Core.Dsp;

namespace OldSound.Core.Pipeline;

/// <summary>
/// Главный конвейер цифровой обработки сигналов (DSP Pipeline).
/// Связывает все аппаратные этапы ретро-консоли PS1 и кассеты:
/// 1. Подготовка частоты голоса SPU (downsample в voice rate)
/// 2. Аппаратное сжатие Sony ADPCM (VAG) с аутентичной зернистостью
/// 3. Воспроизведение через 4-точечную гауссову интерполяцию ЦАП SPU
/// 4. 3-полюсный аналоговый фильтр выхода ЦАП SPU (-18 дБ/окт, срез выше 10–12 кГц)
/// 5. Нелинейное насыщение и компрессия шины микширования SPU (Bus Glue)
/// 6. Фоновый аналоговый шум резистивной матрицы ЦАП SPU (Noise Floor)
/// 7. Опциональный тракт магнитной компакт-кассеты (Tape Saturation)
/// </summary>
public static class RetroAudioPipeline
{
    public static AudioBuffer Process(AudioBuffer input, AudioPreset preset)
    {
        int channels = input.Channels;
        int inSampleRate = input.SampleRate;
        int targetVoiceRate = preset.SpuVoiceRate;
        int outSampleRate = preset.OutputSampleRate > 0 ? preset.OutputSampleRate : inSampleRate;

        // 1. Поканальная SPU ADPCM и ЦАП обработка
        short[][] processedChannels = new short[channels][];
        int finalLength = 0;

        for (int ch = 0; ch < channels; ch++)
        {
            short[] pcm16 = input.ToPcm16(ch);
            short[] spuOutput;

            if (preset.EnableAdpcm)
            {
                var adpcm = new SonyAdpcm();

                // Ресемплинг в частоту голоса SPU перед VAG компрессией
                short[] voicePcm = (inSampleRate != targetVoiceRate)
                    ? ResampleLinear(pcm16, inSampleRate, targetVoiceRate)
                    : pcm16;

                // Сжатие в 4-битный VAG и декодирование с аутентичным квантованием
                short[] adpcmPcm = adpcm.ProcessPcm(voicePcm, preset.AdpcmGrit, preset.AuthenticAdpcmMode);

                if (preset.EnableGaussian)
                {
                    // Воспроизведение через аппаратную гауссову интерполяцию ЦАП на выходной частоте
                    spuOutput = SpuGaussianInterpolator.Process(adpcmPcm, targetVoiceRate, targetVoiceRate, outSampleRate);
                }
                else
                {
                    spuOutput = (targetVoiceRate != outSampleRate)
                        ? ResampleLinear(adpcmPcm, targetVoiceRate, outSampleRate)
                        : adpcmPcm;
                }
            }
            else if (preset.EnableGaussian)
            {
                spuOutput = SpuGaussianInterpolator.Process(pcm16, inSampleRate, targetVoiceRate, outSampleRate);
            }
            else
            {
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

        // Создаем выходной буфер с нормализованными float [-1.0 .. 1.0]
        var outBuffer = new AudioBuffer(channels, outSampleRate, finalLength);
        for (int ch = 0; ch < channels; ch++)
        {
            outBuffer.FromPcm16(ch, processedChannels[ch].AsSpan(0, finalLength));
        }

        // 2. Мягкая нелинейная сатурация и компрессия шины SPU (Bus Glue)
        if (preset.BusGlue > 0.01f)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                SpuBusGlue.Process(outBuffer.GetChannelSpan(ch), preset.BusGlue);
            }
        }

        // 3. Аналоговый фоновый шум ЦАП SPU (Noise Floor — «акустический клей»)
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

        // 4. Аппаратный 3-полюсный аналоговый фильтр выхода ЦАП SPU (-18 дБ/окт)
        // Фильтрует и звук, и шум ЦАП, формируя мягкий теплый «подводный» акустический туман
        if (preset.EnableAnalogFilter && preset.FilterCutoffHz > 0)
        {
            var filter = new SpuAnalogFilter(preset.FilterCutoffHz, outSampleRate);
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
