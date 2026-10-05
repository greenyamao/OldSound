using System;

namespace OldSound.Core.Dsp;

/// <summary>
/// Аппаратный фоновый шум ЦАП и аналогового тракта PlayStation 1 (SPU Noise Floor).
/// В реальных консолях 90-х не существовало абсолютной цифровой тишины.
/// Пространство между нотами и сэмплами заполнялось теплым шумом резистивной матрицы ЦАП
/// и квантования ADPCM, который работал как «акустический клей», предотвращая ощущение
/// пустой цифровой стерильности (uncanny emptiness).
/// </summary>
public sealed class SpuNoiseFloor
{
    private readonly Random _random;
    private float _b0L, _b1L, _b2L;
    private float _b0R, _b1R, _b2R;

    public SpuNoiseFloor(int seed = 1994)
    {
        _random = new Random(seed);
    }

    public void Reset()
    {
        _b0L = _b1L = _b2L = 0f;
        _b0R = _b1R = _b2R = 0f;
    }

    /// <summary>
    /// Накладывает аналоговый фон ЦАП SPU на стерео сигнал.
    /// </summary>
    /// <param name="left">Левый канал</param>
    /// <param name="right">Правый канал</param>
    /// <param name="level">Уровень шума (0.0 = выключен, 1.0 = нормальный консольный фон ~ -52 dB)</param>
    public void ProcessStereo(Span<float> left, Span<float> right, float level = 1.0f)
    {
        if (level <= 0.0001f) return;

        // Базовый уровень шума ЦАП консоли (-52 dBFS = 0.0025)
        float baseAmp = level * 0.0025f;
        int length = Math.Min(left.Length, right.Length);

        for (int i = 0; i < length; i++)
        {
            float wL = (float)(_random.NextDouble() * 2.0 - 1.0);
            float wR = (float)(_random.NextDouble() * 2.0 - 1.0);

            // Мягкий розово-коричневый фильтр шума (характер ЦАП старых ревизий SCPH-100x/500x)
            _b0L = 0.99765f * _b0L + wL * 0.0990460f;
            _b1L = 0.96300f * _b1L + wL * 0.2965164f;
            _b2L = 0.57000f * _b2L + wL * 1.0526913f;
            float pinkL = (_b0L + _b1L + _b2L + wL * 0.1848f) * 0.333f;

            _b0R = 0.99765f * _b0R + wR * 0.0990460f;
            _b1R = 0.96300f * _b1R + wR * 0.2965164f;
            _b2R = 0.57000f * _b2R + wR * 1.0526913f;
            float pinkR = (_b0R + _b1R + _b2R + wR * 0.1848f) * 0.333f;

            left[i] += pinkL * baseAmp;
            right[i] += pinkR * baseAmp;
        }
    }

    /// <summary>
    /// Накладывает аналоговый фон ЦАП SPU на моно сигнал.
    /// </summary>
    public void ProcessMono(Span<float> channel, float level = 1.0f)
    {
        if (level <= 0.0001f) return;

        float baseAmp = level * 0.0025f;
        for (int i = 0; i < channel.Length; i++)
        {
            float w = (float)(_random.NextDouble() * 2.0 - 1.0);
            _b0L = 0.99765f * _b0L + w * 0.0990460f;
            _b1L = 0.96300f * _b1L + w * 0.2965164f;
            _b2L = 0.57000f * _b2L + w * 1.0526913f;
            float pink = (_b0L + _b1L + _b2L + w * 0.1848f) * 0.333f;

            channel[i] += pink * baseAmp;
        }
    }
}
