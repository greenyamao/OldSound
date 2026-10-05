using System;

namespace OldSound.Core.Dsp;

/// <summary>
/// Реализация исторического 8-битного кодека SDX2 (Square-Root Delta),
/// применявшегося в 3DO Portfolio OS (чип CLIO / Opera DSP) для сжатия потоковой
/// фоновой музыки (в частности, саундтрека Four-Sight 1995 композитора Юдзи Номи).
/// 
/// Математическая модель (3DO Opera DSP):
///   Δ[n] = x[n] - y_hat[n-1]
///   b[n] = clip_[-128, 127]( sgn(Δ[n]) * round( sqrt(|Δ[n]| / 2) ) )
/// 
/// Декодирование через таблицу квадратов (Opera DSP LUT):
///   Δ_rec[n] = sgn(b[n]) * 2 * (b[n])^2
///   y_hat[n] = clip_[-32768, 32767]( y_hat[n-1] + Δ_rec[n] )
/// 
/// Особенности:
///   - 2:1 фиксированная компрессия (16-битный PCM сжимается в 8-битный поток дельт).
///   - Сохраняет микродинамику и низкий уровень шума в тихих пассажах.
///   - Создает специфическое ограничение скорости нарастания (slew-rate limiting)
///     на резких перкуссионных и оркестровых атаках.
/// </summary>
public static class Sdx2Codec
{
    // Таблица предвычисленных квадратичных дельт для всех 256 значений b[n] in [-128, 127]
    private static readonly short[] Lut = InitializeLut();

    private static short[] InitializeLut()
    {
        var lut = new short[256];
        for (int i = 0; i < 256; i++)
        {
            int b = i - 128;
            int sign = Math.Sign(b);
            int mag = Math.Abs(b);
            int deltaRec = sign * 2 * (mag * mag);
            lut[i] = (short)Math.Clamp(deltaRec, short.MinValue, short.MaxValue);
        }
        return lut;
    }

    /// <summary>
    /// Кодирование 16-битного линейного PCM в 8-битный поток дельт SDX2.
    /// </summary>
    public static sbyte[] Encode(ReadOnlySpan<short> pcm)
    {
        var output = new sbyte[pcm.Length];
        int accum = 0;

        for (int i = 0; i < pcm.Length; i++)
        {
            int delta = pcm[i] - accum;
            int sign = delta >= 0 ? 1 : -1;
            int mag = Math.Abs(delta);

            int val = (int)Math.Round(Math.Sqrt(mag / 2.0));
            sbyte b = (sbyte)Math.Clamp(sign * val, -128, 127);
            output[i] = b;

            // Локальная обратная связь декодера для минимизации ошибки накопления
            short recDelta = Lut[b + 128];
            accum = Math.Clamp(accum + recDelta, short.MinValue, short.MaxValue);
        }

        return output;
    }

    /// <summary>
    /// Декодирование 8-битного потока дельт SDX2 обратно в 16-битный PCM.
    /// </summary>
    public static short[] Decode(ReadOnlySpan<sbyte> sdx2Bytes, short initialAccum = 0)
    {
        var output = new short[sdx2Bytes.Length];
        int accum = initialAccum;

        for (int i = 0; i < sdx2Bytes.Length; i++)
        {
            sbyte b = sdx2Bytes[i];
            short recDelta = Lut[b + 128];
            accum = Math.Clamp(accum + recDelta, short.MinValue, short.MaxValue);
            output[i] = (short)accum;
        }

        return output;
    }

    /// <summary>
    /// Полный цикл сжатия и восстановления SDX2.
    /// </summary>
    public static short[] ProcessPcm(ReadOnlySpan<short> pcm)
    {
        sbyte[] encoded = Encode(pcm);
        return Decode(encoded);
    }

    /// <summary>
    /// Программный апсэмплинг системного микрокода half_rate.dsp платформы 3DO (22 050 Гц -> 44 100 Гц).
    /// Выполняет линейную интерполяцию отсчетов без подавления зеркальных частот (imaging),
    /// формируя спад sinc^2 (-3.18 дБ на 11 кГц) и характерную стеклянно-металлическую окраску в 11-22 кГц.
    /// </summary>
    public static short[] Upsample3DoHalfRate(ReadOnlySpan<short> samples22k)
    {
        int n = samples22k.Length;
        if (n == 0) return Array.Empty<short>();

        var output = new short[n * 2];
        for (int i = 0; i < n; i++)
        {
            output[2 * i] = samples22k[i];
            if (i < n - 1)
            {
                int avg = (samples22k[i] + samples22k[i + 1]) / 2;
                output[2 * i + 1] = (short)avg;
            }
            else
            {
                output[2 * i + 1] = samples22k[i];
            }
        }
        return output;
    }
}
