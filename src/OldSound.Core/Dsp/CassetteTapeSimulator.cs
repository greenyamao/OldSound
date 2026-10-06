using System;

namespace OldSound.Core.Dsp;

/// <summary>
/// Settings for analog compact cassette emulation.
/// </summary>
public sealed class CassetteTapeSettings
{
    /// <summary>Magnetic tape saturation drive (1.0 = light saturation, 1.5–2.0 = pronounced compression).</summary>
    public float Drive { get; set; } = 1.4f;

    /// <summary>Wow depth (slow pitch drift 0.5-1.5 Hz, range 0.0 .. 1.0).</summary>
    public float WowDepth { get; set; } = 0.20f;

    /// <summary>Flutter depth (fast pitch flutter 6-12 Hz, range 0.0 .. 1.0).</summary>
    public float FlutterDepth { get; set; } = 0.15f;

    /// <summary>Analog tape hiss level (0.0 = off, 1.0 = subtle background ~ -60 dB).</summary>
    public float HissLevel { get; set; } = 0.20f;

    /// <summary>Tape head gap loss low-pass cutoff (8000..16000 Hz).</summary>
    public float CutoffHz { get; set; } = 12000f;

    /// <summary>Enable playback head resonance (Head Bump 65 Hz and high roll-off).</summary>
    public bool EnableHeadEq { get; set; } = true;

    /// <summary>Wet/dry mix ratio (0.0 .. 1.0).</summary>
    public float Mix { get; set; } = 1.0f;
}

/// <summary>
/// Analog compact cassette emulator: non-linear tape saturation,
/// head bump EQ, wow & flutter modulation, and analog tape hiss.
/// Maintains gain staging to prevent digital clipping.
/// </summary>
public sealed class CassetteTapeSimulator
{
    private readonly CassetteTapeSettings _settings;
    private readonly Random _random = new(1985);

    private const int MaxDelaySamples = 4096;
    private readonly float[] _delayBufferL = new float[MaxDelaySamples];
    private readonly float[] _delayBufferR = new float[MaxDelaySamples];
    private int _delayWritePos;

    private double _wowPhase;
    private double _flutterPhase;

    private float _b0, _b1, _b2, _a1, _a2;
    private float _eqX1L, _eqX2L, _eqY1L, _eqY2L;
    private float _eqX1R, _eqX2R, _eqY1R, _eqY2R;

    private float _lpPrevL, _lpPrevR;

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

    private void InitFilters(int sampleRate)
    {
        float f0 = 65.0f;
        float gainDb = 2.0f; // Moderate bass lift +2.0 dB
        float q = 0.8f;

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

        // Gain compensation to prevent clipping (+2 dB EQ -> -1.2 dB compensation)
        float comp = MathF.Pow(10.0f, -1.2f / 20.0f);
        _b0 = (b0 / a0) * comp;
        _b1 = (b1 / a0) * comp;
        _b2 = (b2 / a0) * comp;
        _a1 = a1 / a0;
        _a2 = a2 / a0;
    }

    public void Process(Span<float> left, Span<float> right, int sampleRate)
    {
        InitFilters(sampleRate);

        int count = left.Length;
        float drive = Math.Clamp(_settings.Drive, 0.5f, 3.0f);

        double wowStep = 2.0 * Math.PI * 0.85 / sampleRate;
        double flutterStep = 2.0 * Math.PI * 8.5 / sampleRate;

        float maxModulationSamples = 20.0f * sampleRate / 44100.0f;
        float baseDelay = 120.0f;

        float lpAlpha = MathF.Exp(-2.0f * MathF.PI * _settings.CutoffHz / sampleRate);
        float hissAmp = _settings.HissLevel * 0.0015f;

        for (int i = 0; i < count; i++)
        {
            float dryL = left[i];
            float dryR = right[i];

            // 1. Wow & Flutter modulation
            double wow = Math.Sin(_wowPhase) * _settings.WowDepth;
            double flutter = Math.Sin(_flutterPhase) * _settings.FlutterDepth;
            double totalMod = (wow + flutter) * maxModulationSamples;
            double currentDelay = baseDelay + totalMod;

            _wowPhase += wowStep;
            if (_wowPhase > Math.PI * 2.0) _wowPhase -= Math.PI * 2.0;
            _flutterPhase += flutterStep;
            if (_flutterPhase > Math.PI * 2.0) _flutterPhase -= Math.PI * 2.0;

            _delayBufferL[_delayWritePos] = dryL;
            _delayBufferR[_delayWritePos] = dryR;

            double readPos = _delayWritePos - currentDelay;
            while (readPos < 0) readPos += MaxDelaySamples;
            while (readPos >= MaxDelaySamples) readPos -= MaxDelaySamples;

            int idx0 = (int)readPos;
            int idx1 = (idx0 + 1) % MaxDelaySamples;
            float frac = (float)(readPos - idx0);

            float wetL = _delayBufferL[idx0] + frac * (_delayBufferL[idx1] - _delayBufferL[idx0]);
            float wetR = _delayBufferR[idx0] + frac * (_delayBufferR[idx1] - _delayBufferR[idx0]);

            _delayWritePos = (_delayWritePos + 1) % MaxDelaySamples;

            // 2. Non-linear magnetic tape saturation with normalized ceiling
            wetL = SaturateTape(wetL, drive);
            wetR = SaturateTape(wetR, drive);

            // 3. Head bump EQ and gap loss
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

                _lpPrevL = (1.0f - lpAlpha) * wetL + lpAlpha * _lpPrevL;
                wetL = _lpPrevL;
                _lpPrevR = (1.0f - lpAlpha) * wetR + lpAlpha * _lpPrevR;
                wetR = _lpPrevR;
            }

            // 4. Subtle tape hiss
            if (hissAmp > 0.000001f)
            {
                float wL = (float)(_random.NextDouble() * 2.0 - 1.0);
                float wR = (float)(_random.NextDouble() * 2.0 - 1.0);

                _b0NoiseL = 0.99765f * _b0NoiseL + wL * 0.0990460f;
                _b1NoiseL = 0.96300f * _b1NoiseL + wL * 0.2965164f;
                _b2NoiseL = 0.57000f * _b2NoiseL + wL * 1.0526913f;
                float pinkL = (_b0NoiseL + _b1NoiseL + _b2NoiseL + wL * 0.1848f) * 0.15f;

                _b0NoiseR = 0.99765f * _b0NoiseR + wR * 0.0990460f;
                _b1NoiseR = 0.96300f * _b1NoiseR + wR * 0.2965164f;
                _b2NoiseR = 0.57000f * _b2NoiseR + wR * 1.0526913f;
                float pinkR = (_b0NoiseR + _b1NoiseR + _b2NoiseR + wR * 0.1848f) * 0.15f;

                wetL += pinkL * hissAmp;
                wetR += pinkR * hissAmp;
            }

            // 5. Mix and soft limiter
            float mixL = dryL + _settings.Mix * (wetL - dryL);
            float mixR = dryR + _settings.Mix * (wetR - dryR);

            left[i] = SoftLimit(mixL);
            right[i] = SoftLimit(mixR);
        }
    }

    private static float SaturateTape(float input, float drive)
    {
        float x = input * drive;
        float asym = (x > 0) ? (x + 0.04f * x * x) : x;
        float sat = MathF.Tanh(asym);
        float norm = MathF.Tanh(drive);
        return sat / norm;
    }

    private static float SoftLimit(float sample)
    {
        if (sample > 0.95f)
        {
            float over = sample - 0.95f;
            return 0.95f + 0.045f * MathF.Tanh(over / 0.045f);
        }
        if (sample < -0.95f)
        {
            float over = sample + 0.95f;
            return -0.95f + 0.045f * MathF.Tanh(over / 0.045f);
        }
        return sample;
    }
}
