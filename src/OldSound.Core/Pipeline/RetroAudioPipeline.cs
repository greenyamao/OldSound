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
        const float HeadroomPreGain = 0.85f; // Запас -1.4 dBFS от переполнения ADPCM предиктора и фильтров

        for (int ch = 0; ch < channels; ch++)
        {
            short[] pcm16 = input.ToPcm16(ch, HeadroomPreGain);
            short[] voicePcm;

            // Антиалиасинг и ресемплинг в частоту консольного голоса
            if (inSampleRate != targetVoiceRate)
            {
                // Префильтрация: если PreFilterCutoffHz <= 0, фильтр отключается, сохраняя аутентичный кристаллический верх и алиасинг
                short[] filtered = (preset.PreFilterCutoffHz > 0f && preset.PreFilterCutoffHz < 20000f)
                    ? ApplyPreFilter(pcm16, inSampleRate, preset.PreFilterCutoffHz)
                    : pcm16;
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
            outBuffer.FromPcm16(ch, processedChannels[ch].AsSpan(0, finalLength), 1.0f / HeadroomPreGain);
        }

        // 2. Аппаратный аналоговый выходной фильтр ЦАП (PS1 3-pole или 3DO 2-pole Sallen-Key)
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

        // 3. Высококачественный аналоговый шум выходного тракта (Master Analog Noise Floor)
        // Добавляется в САМОМ КОНЦЕ тракта на полной сетке частот (44.1 кГц),
        // формируя высококачественный стерео-фон («акустический клей») поверх жмыхнутого звука.
        if (preset.SpuNoiseLevel > 0.0001f)
        {
            var noise = new SpuNoiseFloor();
            if (channels >= 2)
            {
                noise.ProcessStereo(outBuffer.GetChannelSpan(0), outBuffer.GetChannelSpan(1), preset.SpuNoiseLevel, preset.NoiseProfile, outBuffer.SampleRate);
            }
            else
            {
                noise.ProcessMono(outBuffer.GetChannelSpan(0), preset.SpuNoiseLevel, preset.NoiseProfile, outBuffer.SampleRate);
            }
        }

        // 4. Автоматическая калибровка безопасного запаса по уровню (-1.4 dBFS)
        // Гарантирует абсолютное отсутствие клиппинга во всех форматах (WAV 16-bit / MP3 / WASAPI)
        EnsureSafetyHeadroom(outBuffer, 0.85f);

        return outBuffer;
    }

    /// <summary>
    /// Предварительный антиалиасинг КИХ-фильтр Блэкмана-Харриса (129 taps).
    /// Гарантирует подавление спектральных компонент выше Найквиста целевой частоты
    /// на >60..80 дБ, полностью устраняя паразитный foldover-алиасинг (эффект дешевого биткрашера)
    /// и сохраняя естественную чистоту и музыкальность оригинального саундтрека Four-Sight.
    /// </summary>
    private static short[] ApplyPreFilter(ReadOnlySpan<short> input, int sampleRate, float cutoffHz)
    {
        return AntiAliasingFilter.Apply(input, sampleRate, cutoffHz);
    }

    /// <summary>
    /// Автоматическое масштабирование запаса по уровню (Safety Headroom Auto-Gain).
    /// Если пики сигнала (из-за резонанса аналоговых фильтров, интерполяции или квантования)
    /// превышают безопасный порог (-1.4 dBFS / 0.85), весь трек пропорционально и линейно
    /// масштабируется вниз ("звук становится тише"), гарантируя 0 сэмплов клиппинга
    /// без срезания формы волны (flat-tops), перегруза ЦАП или MP3 intersample peaks.
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

        // Страховочный зажим на случай погрешностей с плавающей точкой
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
}
