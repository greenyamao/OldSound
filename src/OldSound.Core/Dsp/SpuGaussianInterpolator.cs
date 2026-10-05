using System;

namespace OldSound.Core.Dsp;

/// <summary>
/// Аппаратно точная эмуляция 4-точечной гауссовой интерполяции чипа SPU (PlayStation 1 DAC).
/// Включает оригинальную 512-элементную таблицу коэффициентов nocash psxspx
/// и обеспечивает исторически достоверный спад высоких частот (натуральный Low-Pass фильтр).
/// </summary>
public static class SpuGaussianInterpolator
{
    /// <summary>
    /// Оригинальная 512-значная аппаратная таблица коэффициентов Гаусса SPU.
    /// Сумма четверок коэффициентов для любого i: 0x7F7F..0x7F81 (32639..32641).
    /// </summary>
    public static readonly short[] Table = new short[512]
    {
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 3, 3,
        3, 4, 4, 5, 5, 6, 7, 7, 8, 9, 9, 10, 11, 12, 13, 14,
        15, 16, 17, 18, 19, 21, 22, 24, 25, 27, 28, 30, 32, 33, 35, 37,
        39, 41, 44, 46, 48, 51, 53, 56, 58, 61, 64, 67, 70, 73, 77, 80,
        84, 87, 91, 95, 99, 103, 107, 111, 116, 120, 125, 130, 135, 140, 145, 150,
        156, 161, 167, 173, 179, 186, 192, 199, 205, 212, 219, 227, 234, 242, 250, 257,
        266, 274, 283, 291, 300, 309, 319, 328, 338, 348, 358, 369, 379, 390, 401, 412,
        424, 436, 448, 460, 473, 485, 498, 512, 525, 539, 553, 567, 582, 597, 612, 627,
        643, 659, 675, 692, 708, 726, 743, 761, 779, 797, 816, 835, 854, 874, 894, 914,
        935, 956, 977, 999, 1020, 1043, 1066, 1089, 1112, 1136, 1160, 1184, 1209, 1234, 1260, 1286,
        1312, 1339, 1366, 1394, 1422, 1450, 1479, 1508, 1537, 1567, 1598, 1628, 1660, 1691, 1723, 1756,
        1789, 1822, 1856, 1890, 1924, 1959, 1995, 2031, 2067, 2104, 2141, 2179, 2217, 2256, 2295, 2334,
        2374, 2415, 2456, 2497, 2539, 2582, 2624, 2668, 2712, 2756, 2801, 2846, 2892, 2938, 2985, 3032,
        3079, 3128, 3176, 3225, 3275, 3325, 3376, 3427, 3479, 3531, 3584, 3637, 3691, 3745, 3799, 3855,
        3910, 3967, 4023, 4081, 4138, 4197, 4255, 4315, 4374, 4435, 4495, 4557, 4619, 4681, 4744, 4807,
        4871, 4935, 5000, 5065, 5131, 5197, 5264, 5332, 5399, 5468, 5536, 5606, 5676, 5746, 5817, 5888,
        5959, 6032, 6104, 6177, 6251, 6325, 6400, 6475, 6550, 6626, 6702, 6779, 6856, 6934, 7012, 7091,
        7170, 7249, 7329, 7409, 7490, 7571, 7653, 7735, 7817, 7900, 7983, 8066, 8150, 8234, 8319, 8404,
        8489, 8575, 8661, 8748, 8834, 8922, 9009, 9097, 9185, 9273, 9362, 9451, 9541, 9630, 9720, 9811,
        9901, 9992, 10083, 10174, 10266, 10358, 10450, 10542, 10635, 10727, 10820, 10913, 11007, 11100, 11194, 11288,
        11382, 11476, 11571, 11665, 11760, 11855, 11950, 12045, 12140, 12236, 12331, 12427, 12522, 12618, 12714, 12809,
        12905, 13001, 13097, 13193, 13289, 13385, 13481, 13577, 13673, 13769, 13865, 13961, 14056, 14152, 14248, 14343,
        14439, 14534, 14630, 14725, 14820, 14915, 15010, 15104, 15199, 15293, 15387, 15481, 15575, 15669, 15762, 15855,
        15948, 16041, 16133, 16226, 16317, 16409, 16500, 16592, 16682, 16773, 16863, 16953, 17042, 17131, 17220, 17308,
        17396, 17484, 17571, 17658, 17744, 17830, 17916, 18001, 18086, 18170, 18254, 18337, 18420, 18502, 18584, 18665,
        18746, 18826, 18905, 18985, 19063, 19141, 19219, 19295, 19372, 19447, 19522, 19597, 19671, 19744, 19816, 19888,
        19959, 20030, 20100, 20169, 20238, 20306, 20373, 20439, 20505, 20570, 20634, 20698, 20760, 20822, 20884, 20944,
        21004, 21063, 21121, 21178, 21235, 21290, 21345, 21399, 21452, 21505, 21556, 21607, 21657, 21706, 21754, 21801,
        21848, 21893, 21938, 21982, 22025, 22066, 22107, 22148, 22187, 22225, 22262, 22299, 22334, 22369, 22402, 22435,
        22467, 22498, 22527, 22556, 22584, 22611, 22637, 22662, 22686, 22709, 22731, 22752, 22772, 22791, 22809, 22826,
        22842, 22857, 22872, 22885, 22897, 22908, 22918, 22927, 22935, 22942, 22948, 22953, 22957, 22960, 22962, 22963
    };

    /// <summary>
    /// Выполняет оригинальную аппаратную 4-точечную гауссову интерполяцию для одного отсчета.
    /// </summary>
    /// <param name="oldest">Самый старый сэмпл s[n-1]</param>
    /// <param name="older">Предыдущий сэмпл s[n]</param>
    /// <param name="old">Текущий сэмпл s[n+1]</param>
    /// <param name="new">Новый сэмпл s[n+2]</param>
    /// <param name="phase8">8-битная дробная фаза сэмпла (0..255)</param>
    public static short Interpolate(short oldest, short older, short old, short @new, int phase8)
    {
        int i = phase8 & 0xFF;
        int outSample = ((Table[0x0FF - i] * oldest) >> 15)
                      + ((Table[0x1FF - i] * older)  >> 15)
                      + ((Table[0x100 + i] * old)    >> 15)
                      + ((Table[0x000 + i] * @new)   >> 15);

        return (short)Math.Clamp(outSample, short.MinValue, short.MaxValue);
    }

    /// <summary>
    /// Применяет аппаратную гауссову фильтрацию ЦАП с опциональным ресемплингом.
    /// Входной поток (например, 44.1k/48k) переводится в частоту голоса SPU (targetVoiceRate),
    /// а затем воспроизводится через гауссов интерполятор на выходной частоте (outputRate, по умолчанию 44100).
    /// </summary>
    public static short[] Process(ReadOnlySpan<short> inputPcm, int inSampleRate, int targetVoiceRate, int outputRate = 44100)
    {
        if (inputPcm.Length == 0)
            return Array.Empty<short>();

        // Шаг 1: Если исходный сэмплрейт отличается от частоты голоса SPU, ресемплим в voice rate
        short[] voiceSamples;
        if (inSampleRate != targetVoiceRate)
        {
            voiceSamples = SimpleResample(inputPcm, inSampleRate, targetVoiceRate);
        }
        else
        {
            voiceSamples = inputPcm.ToArray();
        }

        // Шаг 2: Воспроизведение через гауссов интерполятор ЦАП SPU на частоте outputRate
        // Аппаратный шаг питча в SPU: pitch = (voiceRate * 4096) / 44100
        // Дробная часть: биты 4..11 счетчика питча (8 бит)
        long totalOutputSamples = (long)Math.Ceiling((double)voiceSamples.Length * outputRate / targetVoiceRate);
        var result = new short[totalOutputSamples];

        double step = (double)targetVoiceRate / outputRate;
        double currentPos = 0.0;

        for (int outIdx = 0; outIdx < totalOutputSamples; outIdx++)
        {
            int baseIdx = (int)Math.Floor(currentPos);
            double frac = currentPos - baseIdx;
            int phase8 = Math.Clamp((int)(frac * 256.0), 0, 255);

            // Берем 4 соседних сэмпла с безопасным ограничением границ
            short oldest = GetSampleSafe(voiceSamples, baseIdx - 1);
            short older  = GetSampleSafe(voiceSamples, baseIdx);
            short old    = GetSampleSafe(voiceSamples, baseIdx + 1);
            short @new   = GetSampleSafe(voiceSamples, baseIdx + 2);

            result[outIdx] = Interpolate(oldest, older, old, @new, phase8);
            currentPos += step;
        }

        return result;
    }

    private static short GetSampleSafe(short[] buffer, int index)
    {
        if (index < 0) return buffer[0];
        if (index >= buffer.Length) return buffer[^1];
        return buffer[index];
    }

    /// <summary>
    /// Линейный ресемплер для подготовки звука к частоте голоса SPU перед воспроизведением через Гаусс.
    /// </summary>
    private static short[] SimpleResample(ReadOnlySpan<short> input, int inRate, int outRate)
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
