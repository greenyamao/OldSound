using System;
using System.Runtime.CompilerServices;
using OldSound.Core.Pipeline;

namespace OldSound.Core.Dsp;

/// <summary>
/// Физическая модель аналогового шума тракта (Physical Analog Noise Engine).
/// Моделирует реальные физические компоненты шума:
/// 1. Console (PS1/3DO): тепловой шум ЦАП + срез фильтра 16.5 кГц + сетевой фон БП (50/100 Гц) + строчная развертка/DMA (15.6 кГц).
/// 2. Cassette (Type I NAB 120µs): оксидное зерно ленты + шелковый резонанс головки (8.2 кГц) + завал зазора (14.2 кГц) + рокот мотора (50/68 Гц).
/// </summary>
public sealed class SpuNoiseFloor
{
    private readonly Random _random;

    // Генератор розового шума (Kellet pink filter)
    private float _b0L, _b1L, _b2L;
    private float _b0R, _b1R, _b2R;
    private float _dcInL, _dcOutL;
    private float _dcInR, _dcOutR;

    // Каскад Biquad-фильтров формирования спектра (2 фильтра на канал)
    private float _f1X1L, _f1X2L, _f1Y1L, _f1Y2L;
    private float _f1X1R, _f1X2R, _f1Y1R, _f1Y2R;
    private float _f2X1L, _f2X2L, _f2Y1L, _f2Y2L;
    private float _f2X1R, _f2X2R, _f2Y1R, _f2Y2R;

    // Фазы синтезаторов фона питания и наводок
    private double _humPhase50;
    private double _humPhase100;
    private double _humPhase150;
    private double _clockWhinePhase;
    private double _motorRumblePhase;
    private double _tapeFlutterPhase;

    public SpuNoiseFloor(int seed = 1994)
    {
        _random = new Random(seed);
    }

    public void Reset()
    {
        _b0L = _b1L = _b2L = 0f;
        _b0R = _b1R = _b2R = 0f;
        _dcInL = _dcOutL = 0f;
        _dcInR = _dcOutR = 0f;

        _f1X1L = _f1X2L = _f1Y1L = _f1Y2L = 0f;
        _f1X1R = _f1X2R = _f1Y1R = _f1Y2R = 0f;
        _f2X1L = _f2X2L = _f2Y1L = _f2Y2L = 0f;
        _f2X1R = _f2X2R = _f2Y1R = _f2Y2R = 0f;

        _humPhase50 = 0.0;
        _humPhase100 = 0.0;
        _humPhase150 = 0.0;
        _clockWhinePhase = 0.0;
        _motorRumblePhase = 0.0;
        _tapeFlutterPhase = 0.0;
    }

    /// <summary>
    /// Накладывает физический аналоговый шум на стереосигнал.
    /// </summary>
    public void ProcessStereo(
        Span<float> left, 
        Span<float> right, 
        float level = 1.0f, 
        AnalogNoiseProfile profile = AnalogNoiseProfile.Console,
        int sampleRate = 44100)
    {
        if (level <= 0.0001f) return;

        float clampedLevel = Math.Clamp(level, 0.0f, 2.0f);
        float hissAmp = MathF.Pow(clampedLevel, 1.4f) * 0.038f;
        float humScale = MathF.Pow(clampedLevel, 1.5f);

        int length = Math.Min(left.Length, right.Length);

        // Расчет параметров фильтров под профиль
        BiquadCoeffs f1, f2;
        if (profile == AnalogNoiseProfile.Console)
        {
            // Фильтр 1: Срез выхода ЦАП SPU на 16.5 кГц
            f1 = BiquadCoeffs.LowPass(16500f, 0.707f, sampleRate);
            // Фильтр 2: Мягкий саб-соник срез 40 Гц
            f2 = BiquadCoeffs.HighPass(40f, 0.707f, sampleRate);
        }
        else
        {
            // Фильтр 1: Завал зазора магнитной головки на 14.2 кГц
            f1 = BiquadCoeffs.LowPass(14200f, 0.8f, sampleRate);
            // Фильтр 2: Резонанс индуктивности головки NAB 120µs (+6.5 дБ на 8.2 кГц) — шелковый «ш-ш-ш»
            f2 = BiquadCoeffs.PeakingEq(8200f, 1.3f, 6.5f, sampleRate);
        }

        // Шаги приращения фазы
        double step50 = 2.0 * Math.PI * 50.0 / sampleRate;
        double step100 = 2.0 * Math.PI * 100.0 / sampleRate;
        double step150 = 2.0 * Math.PI * 150.0 / sampleRate;
        double stepWhine = 2.0 * Math.PI * 15625.0 / sampleRate;
        double stepRumble = 2.0 * Math.PI * 68.0 / sampleRate;
        double stepFlutter = 2.0 * Math.PI * 0.75 / sampleRate;

        for (int i = 0; i < length; i++)
        {
            // 1. Генерация сырого стерео розового шума
            float wL = (float)(_random.NextDouble() * 2.0 - 1.0);
            float wR = (float)(_random.NextDouble() * 2.0 - 1.0);

            _b0L = 0.99765f * _b0L + wL * 0.0990460f;
            _b1L = 0.96300f * _b1L + wL * 0.2965164f;
            _b2L = 0.57000f * _b2L + wL * 1.0526913f;
            float rawPinkL = (_b0L + _b1L + _b2L + wL * 0.1848f) * 0.25f;

            _b0R = 0.99765f * _b0R + wR * 0.0990460f;
            _b1R = 0.96300f * _b1R + wR * 0.2965164f;
            _b2R = 0.57000f * _b2R + wR * 1.0526913f;
            float rawPinkR = (_b0R + _b1R + _b2R + wR * 0.1848f) * 0.25f;

            // DC-blocker
            _dcOutL = rawPinkL - _dcInL + 0.995f * _dcOutL;
            _dcInL = rawPinkL;

            _dcOutR = rawPinkR - _dcInR + 0.995f * _dcOutR;
            _dcInR = rawPinkR;

            // 2. Спектральная коррекция (Biquad cascade)
            float shapedL = ApplyBiquad(_dcOutL, in f1, ref _f1X1L, ref _f1X2L, ref _f1Y1L, ref _f1Y2L);
            shapedL = ApplyBiquad(shapedL, in f2, ref _f2X1L, ref _f2X2L, ref _f2Y1L, ref _f2Y2L);

            float shapedR = ApplyBiquad(_dcOutR, in f1, ref _f1X1R, ref _f1X2R, ref _f1Y1R, ref _f1Y2R);
            shapedR = ApplyBiquad(shapedR, in f2, ref _f2X1R, ref _f2X2R, ref _f2Y1R, ref _f2Y2R);

            // 3. Синтез монофонических наводок земли / мотора / развертки
            float monoArtifact = 0f;
            if (profile == AnalogNoiseProfile.Console)
            {
                // Сетевой фон БП (50 Гц + 100 Гц выпрямитель + 150 Гц третья гармоника)
                float hum50 = (float)Math.Sin(_humPhase50) * 0.0032f;
                float hum100 = (float)Math.Sin(_humPhase100) * 0.0020f;
                float hum150 = (float)Math.Sin(_humPhase150) * 0.0007f;

                // Ультратонкий писк строчной развертки / DMA (15.625 кГц)
                float whine = (float)Math.Sin(_clockWhinePhase) * 0.00065f;

                monoArtifact = (hum50 + hum100 + hum150 + whine) * humScale;
            }
            else
            {
                // Фон сетевого трансформатора магнитофона + рокот подшипника тонвала (68 Гц)
                float hum50 = (float)Math.Sin(_humPhase50) * 0.0025f;
                float rumble = (float)Math.Sin(_motorRumblePhase) * 0.0016f;

                // Микро-флаттер ленты (дыхание уровня шума)
                float flutterMod = 1.0f + 0.04f * (float)Math.Sin(_tapeFlutterPhase);
                shapedL *= flutterMod;
                shapedR *= flutterMod;

                monoArtifact = (hum50 + rumble) * humScale;
            }

            // Инкремент фаз
            _humPhase50 = (_humPhase50 + step50) % (Math.PI * 2.0);
            _humPhase100 = (_humPhase100 + step100) % (Math.PI * 2.0);
            _humPhase150 = (_humPhase150 + step150) % (Math.PI * 2.0);
            _clockWhinePhase = (_clockWhinePhase + stepWhine) % (Math.PI * 2.0);
            _motorRumblePhase = (_motorRumblePhase + stepRumble) % (Math.PI * 2.0);
            _tapeFlutterPhase = (_tapeFlutterPhase + stepFlutter) % (Math.PI * 2.0);

            // 4. Смешивание: Моно-гул в центре + Стерео-зерно по панораме
            left[i] += shapedL * hissAmp + monoArtifact;
            right[i] += shapedR * hissAmp + monoArtifact;
        }
    }

    /// <summary>
    /// Накладывает физический аналоговый шум на моно сигнал.
    /// </summary>
    public void ProcessMono(
        Span<float> channel, 
        float level = 1.0f, 
        AnalogNoiseProfile profile = AnalogNoiseProfile.Console,
        int sampleRate = 44100)
    {
        if (level <= 0.0001f) return;

        float clampedLevel = Math.Clamp(level, 0.0f, 2.0f);
        float hissAmp = MathF.Pow(clampedLevel, 1.4f) * 0.038f;
        float humScale = MathF.Pow(clampedLevel, 1.5f);

        BiquadCoeffs f1, f2;
        if (profile == AnalogNoiseProfile.Console)
        {
            f1 = BiquadCoeffs.LowPass(16500f, 0.707f, sampleRate);
            f2 = BiquadCoeffs.HighPass(40f, 0.707f, sampleRate);
        }
        else
        {
            f1 = BiquadCoeffs.LowPass(14200f, 0.8f, sampleRate);
            f2 = BiquadCoeffs.PeakingEq(8200f, 1.3f, 6.5f, sampleRate);
        }

        double step50 = 2.0 * Math.PI * 50.0 / sampleRate;
        double step100 = 2.0 * Math.PI * 100.0 / sampleRate;
        double step150 = 2.0 * Math.PI * 150.0 / sampleRate;
        double stepWhine = 2.0 * Math.PI * 15625.0 / sampleRate;
        double stepRumble = 2.0 * Math.PI * 68.0 / sampleRate;

        for (int i = 0; i < channel.Length; i++)
        {
            float w = (float)(_random.NextDouble() * 2.0 - 1.0);
            _b0L = 0.99765f * _b0L + w * 0.0990460f;
            _b1L = 0.96300f * _b1L + w * 0.2965164f;
            _b2L = 0.57000f * _b2L + w * 1.0526913f;
            float rawPink = (_b0L + _b1L + _b2L + w * 0.1848f) * 0.25f;

            _dcOutL = rawPink - _dcInL + 0.995f * _dcOutL;
            _dcInL = rawPink;

            float shaped = ApplyBiquad(_dcOutL, in f1, ref _f1X1L, ref _f1X2L, ref _f1Y1L, ref _f1Y2L);
            shaped = ApplyBiquad(shaped, in f2, ref _f2X1L, ref _f2X2L, ref _f2Y1L, ref _f2Y2L);

            float artifact = 0f;
            if (profile == AnalogNoiseProfile.Console)
            {
                float hum50 = (float)Math.Sin(_humPhase50) * 0.0032f;
                float hum100 = (float)Math.Sin(_humPhase100) * 0.0020f;
                float hum150 = (float)Math.Sin(_humPhase150) * 0.0007f;
                float whine = (float)Math.Sin(_clockWhinePhase) * 0.00065f;
                artifact = (hum50 + hum100 + hum150 + whine) * humScale;
            }
            else
            {
                float hum50 = (float)Math.Sin(_humPhase50) * 0.0025f;
                float rumble = (float)Math.Sin(_motorRumblePhase) * 0.0016f;
                artifact = (hum50 + rumble) * humScale;
            }

            _humPhase50 = (_humPhase50 + step50) % (Math.PI * 2.0);
            _humPhase100 = (_humPhase100 + step100) % (Math.PI * 2.0);
            _humPhase150 = (_humPhase150 + step150) % (Math.PI * 2.0);
            _clockWhinePhase = (_clockWhinePhase + stepWhine) % (Math.PI * 2.0);
            _motorRumblePhase = (_motorRumblePhase + stepRumble) % (Math.PI * 2.0);

            channel[i] += shaped * hissAmp + artifact;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ApplyBiquad(float inSample, in BiquadCoeffs c, ref float x1, ref float x2, ref float y1, ref float y2)
    {
        float outSample = c.B0 * inSample + c.B1 * x1 + c.B2 * x2 - c.A1 * y1 - c.A2 * y2;
        x2 = x1;
        x1 = inSample;
        y2 = y1;
        y1 = outSample;
        return outSample;
    }

    private readonly struct BiquadCoeffs
    {
        public readonly float B0, B1, B2, A1, A2;

        public BiquadCoeffs(float b0, float b1, float b2, float a1, float a2)
        {
            B0 = b0; B1 = b1; B2 = b2; A1 = a1; A2 = a2;
        }

        public static BiquadCoeffs LowPass(float f0, float q, float fs)
        {
            float w0 = 2f * MathF.PI * f0 / fs;
            float alpha = MathF.Sin(w0) / (2f * q);
            float cos = MathF.Cos(w0);
            float b0 = (1f - cos) * 0.5f;
            float b1 = 1f - cos;
            float b2 = (1f - cos) * 0.5f;
            float a0 = 1f + alpha;
            float a1 = -2f * cos;
            float a2 = 1f - alpha;
            return new BiquadCoeffs(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
        }

        public static BiquadCoeffs HighPass(float f0, float q, float fs)
        {
            float w0 = 2f * MathF.PI * f0 / fs;
            float alpha = MathF.Sin(w0) / (2f * q);
            float cos = MathF.Cos(w0);
            float b0 = (1f + cos) * 0.5f;
            float b1 = -(1f + cos);
            float b2 = (1f + cos) * 0.5f;
            float a0 = 1f + alpha;
            float a1 = -2f * cos;
            float a2 = 1f - alpha;
            return new BiquadCoeffs(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
        }

        public static BiquadCoeffs PeakingEq(float f0, float q, float gainDb, float fs)
        {
            float a = MathF.Pow(10f, gainDb / 40f);
            float w0 = 2f * MathF.PI * f0 / fs;
            float alpha = MathF.Sin(w0) / (2f * q);
            float cos = MathF.Cos(w0);
            float b0 = 1f + alpha * a;
            float b1 = -2f * cos;
            float b2 = 1f - alpha * a;
            float a0 = 1f + alpha / a;
            float a1 = -2f * cos;
            float a2 = 1f - alpha / a;
            return new BiquadCoeffs(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
        }
    }
}
