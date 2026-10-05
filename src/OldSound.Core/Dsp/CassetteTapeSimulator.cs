using System;

namespace OldSound.Core.Dsp;

/// <summary>
/// Настройки эмулятора аналоговой компакт-кассеты.
/// </summary>
public sealed class CassetteTapeSettings
{
    /// <summary>Уровень магнитного насыщения / перегруза ленты (1.0 = прозрачно, 2.5–3.5 = сочный аналоговый «жмых»).</summary>
    public float Drive { get; set; } = 2.8f;

    /// <summary>Глубина детонации Wow (медленное плавание питча 0.5-1.5 Гц, диапазон 0.0 .. 1.0).</summary>
    public float WowDepth { get; set; } = 0.35f;

    /// <summary>Глубина детонации Flutter (быстрое дрожание питча 6-12 Гц, диапазон 0.0 .. 1.0).</summary>
    public float FlutterDepth { get; set; } = 0.25f;

    /// <summary>Уровень аналогового шума магнитной ленты (0.0 = выключен, 1.0 = аутентичный шум кассеты ~ -48 dB).</summary>
    public float HissLevel { get; set; } = 0.35f;

    /// <summary>Частота среза зазора магнитной головки (Head Loss Low-Pass, 8000..16000 Гц).</summary>
    public float CutoffHz { get; set; } = 11500f;

    /// <summary>Включить резонанс воспроизводящей головки (Head Bump 65 Гц и спад верха).</summary>
    public bool EnableHeadEq { get; set; } = true;

    /// <summary>Соотношение обработанного и сухого сигнала (0.0 .. 1.0).</summary>
    public float Mix { get; set; } = 1.0f;
}

/// <summary>
/// Эмулятор аналоговой компакт-кассеты: нелинейное насыщение магнитной ленты,
/// резонанс головки (Head Bump), детонация механизма (Wow & Flutter) и аналоговый шум (Hiss).
/// </summary>
public sealed class CassetteTapeSimulator
{
    private readonly CassetteTapeSettings _settings;
    private readonly Random _random = new(1985);

    // Параметры линии задержки для Wow/Flutter
    private const int MaxDelaySamples = 4096;
    private readonly float[] _delayBufferL = new float[MaxDelaySamples];
    private readonly float[] _delayBufferR = new float[MaxDelaySamples];
    private int _delayWritePos;

    // Фазы LFO
    private double _wowPhase;
    private double _flutterPhase;

    // Состояние фильтра Head Bump (Biquad Peak/LowShelf)
    private float _b0, _b1, _b2, _a1, _a2;
    private float _eqX1L, _eqX2L, _eqY1L, _eqY2L;
    private float _eqX1R, _eqX2R, _eqY1R, _eqY2R;

    // Состояние фильтра высоких частот (Head Gap Loss Low-Pass)
    private float _lpPrevL, _lpPrevR;

    // Состояние фильтра шума (розовый фильтр для шума ленты)
    private float _b0NoiseL, _b1NoiseL, _b2NoiseL;
    private float _b0NoiseR, _b1NoiseR, _b2NoiseR;

    public CassetteTapeSimulator(CassetteTapeSettings? settings = null)
    {
        _settings = settings ?? new CassetteTapeSettings();
    }

    public void Reset()
    {
        Array.Clear(_delayBufferL, 0, _delayBufferL.Length);
        Array.Clear(_delayBufferR, 0, _delayBufferR.Length);
        _delayWritePos = 0;
        _wowPhase = 0;
        _flutterPhase = 0;
        _eqX1L = _eqX2L = _eqY1L = _eqY2L = 0;
        _eqX1R = _eqX2R = _eqY1R = _eqY2R = 0;
        _lpPrevL = _lpPrevR = 0;
        _b0NoiseL = _b1NoiseL = _b2NoiseL = 0;
        _b0NoiseR = _b1NoiseR = _b2NoiseR = 0;
    }

    /// <summary>
    /// Инициализирует коэффициенты фильтра Head Bump (+3.2 dB на 65 Гц) для заданной частоты дискретизации.
    /// </summary>
    private void InitFilters(int sampleRate)
    {
        float f0 = 65.0f;
        float gainDb = 3.2f;
        float q = 0.9f;

        float a = MathF.Pow(10.0f, gainDb / 40.0f);
        float w0 = 2.0f * MathF.PI * f0 / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2.0f * q);

        float b0 = a * ((a + 1.0f) - (a - 1.0f) * cosW0 + 2.0f * MathF.Sqrt(a) * alpha);
        float b1 = 2.0f * a * ((a - 1.0f) - (a + 1.0f) * cosW0);
        float b2 = a * ((a + 1.0f) - (a - 1.0f) * cosW0 - 2.0f * MathF.Sqrt(a) * alpha);
        float a0 = (a + 1.0f) + (a - 1.0f) * cosW0 + 2.0f * MathF.Sqrt(a) * alpha;
        float a1 = -2.0f * ((a - 1.0f) + (a + 1.0f) * cosW0);
        float a2 = (a + 1.0f) + (a - 1.0f) * cosW0 - 2.0f * MathF.Sqrt(a) * alpha;

        _b0 = b0 / a0;
        _b1 = b1 / a0;
        _b2 = b2 / a0;
        _a1 = a1 / a0;
        _a2 = a2 / a0;
    }

    /// <summary>
    /// Обрабатывает каналы аудио-буфера с применением кассетного тракта.
    /// </summary>
    public void Process(Span<float> left, Span<float> right, int sampleRate)
    {
        InitFilters(sampleRate);

        int count = left.Length;
        float drive = MathF.Max(0.5f, _settings.Drive);

        // Параметры LFO: Wow (~0.85 Гц) и Flutter (~8.5 Гц)
        double wowStep = 2.0 * Math.PI * 0.85 / sampleRate;
        double flutterStep = 2.0 * Math.PI * 8.5 / sampleRate;

        float maxModulationSamples = 28.0f * sampleRate / 44100.0f;
        float baseDelay = 150.0f;

        // Коэффициент однополюсного Low-Pass для спада верха головки (по умолчанию ~11.5 кГц)
        float lpAlpha = MathF.Exp(-2.0f * MathF.PI * _settings.CutoffHz / sampleRate);

        // Калиброванный уровень аналогового шума ленты (-50 dBFS)
        float hissAmp = _settings.HissLevel * 0.0035f;

        for (int i = 0; i < count; i++)
        {
            float dryL = left[i];
            float dryR = right[i];

            // 1. Wow & Flutter (Модулируемая задержка аналогового лентопротяжного механизма)
            double wow = Math.Sin(_wowPhase) * _settings.WowDepth;
            double flutter = Math.Sin(_flutterPhase) * _settings.FlutterDepth;
            double totalMod = (wow + flutter) * maxModulationSamples;
            double currentDelay = baseDelay + totalMod;

            _wowPhase += wowStep;
            if (_wowPhase > Math.PI * 2.0) _wowPhase -= Math.PI * 2.0;
            _flutterPhase += flutterStep;
            if (_flutterPhase > Math.PI * 2.0) _flutterPhase -= Math.PI * 2.0;

            // Запись в кольцевой буфер
            _delayBufferL[_delayWritePos] = dryL;
            _delayBufferR[_delayWritePos] = dryR;

            // Чтение с интерполяцией
            double readPos = _delayWritePos - currentDelay;
            while (readPos < 0) readPos += MaxDelaySamples;
            while (readPos >= MaxDelaySamples) readPos -= MaxDelaySamples;

            int idx0 = (int)readPos;
            int idx1 = (idx0 + 1) % MaxDelaySamples;
            float frac = (float)(readPos - idx0);

            float wetL = _delayBufferL[idx0] + frac * (_delayBufferL[idx1] - _delayBufferL[idx0]);
            float wetR = _delayBufferR[idx0] + frac * (_delayBufferR[idx1] - _delayBufferR[idx0]);

            _delayWritePos = (_delayWritePos + 1) % MaxDelaySamples;

            // 2. Нелинейная магнитная сатурация кассеты с компрессией и четными гармониками
            wetL = SaturateTape(wetL, drive);
            wetR = SaturateTape(wetR, drive);

            // 3. Head Bump EQ (подъем баса 65 Гц) и Head Loss (мягкий аналоговый срез верха)
            if (_settings.EnableHeadEq)
            {
                float yL = _b0 * wetL + _b1 * _eqX1L + _b2 * _eqX2L - _a1 * _eqY1L - _a2 * _eqY2L;
                _eqX2L = _eqX1L; _eqX1L = wetL;
                _eqY2L = _eqY1L; _eqY1L = yL;
                wetL = yL;

                float yR = _b0 * wetR + _b1 * _eqX1R + _b2 * _eqX2R - _a1 * _eqY1R - _a2 * _eqY2R;
                _eqX2R = _eqX1R; _eqX1R = wetR;
                _eqY2R = _eqY1R; _eqY1R = yR;
                wetR = yR;

                // Head Loss Low-Pass
                _lpPrevL = (1.0f - lpAlpha) * wetL + lpAlpha * _lpPrevL;
                wetL = _lpPrevL;
                _lpPrevR = (1.0f - lpAlpha) * wetR + lpAlpha * _lpPrevR;
                wetR = _lpPrevR;
            }

            // 4. Аналоговый шум магнитной ленты (Tape Hiss)
            if (hissAmp > 0.00001f)
            {
                float wL = (float)(_random.NextDouble() * 2.0 - 1.0);
                float wR = (float)(_random.NextDouble() * 2.0 - 1.0);

                _b0NoiseL = 0.99765f * _b0NoiseL + wL * 0.0990460f;
                _b1NoiseL = 0.96300f * _b1NoiseL + wL * 0.2965164f;
                _b2NoiseL = 0.57000f * _b2NoiseL + wL * 1.0526913f;
                float pinkL = (_b0NoiseL + _b1NoiseL + _b2NoiseL + wL * 0.1848f) * 0.05f;

                _b0NoiseR = 0.99765f * _b0NoiseR + wR * 0.0990460f;
                _b1NoiseR = 0.96300f * _b1NoiseR + wR * 0.2965164f;
                _b2NoiseR = 0.57000f * _b2NoiseR + wR * 1.0526913f;
                float pinkR = (_b0NoiseR + _b1NoiseR + _b2NoiseR + wR * 0.1848f) * 0.05f;

                wetL += pinkL * hissAmp;
                wetR += pinkR * hissAmp;
            }

            // 5. Микс сухого и обработанного
            left[i] = dryL + _settings.Mix * (wetL - dryL);
            right[i] = dryR + _settings.Mix * (wetR - dryR);
        }
    }

    private static float SaturateTape(float input, float drive)
    {
        float x = input * drive;
        // Магнитная асимметрия доменов (+0.07 * x^2) — создает теплые четные гармоники
        float asym = (x > 0) ? (x + 0.07f * x * x) : x;
        // Мягкая компрессия ленты: насыщает громкие пики и подтягивает тихие звуки
        float sat = MathF.Tanh(asym);
        // Сбалансированная нормализация гейна с легким подъемом для сочности
        float norm = MathF.Sqrt(drive);
        return Math.Clamp(sat / norm, -1.15f, 1.15f);
    }
}
