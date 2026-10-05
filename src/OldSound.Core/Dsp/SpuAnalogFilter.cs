using System;

namespace OldSound.Core.Dsp;

/// <summary>
/// Аппаратный аналоговый выходной фильтр чипа SPU PS1 (Reconstruction & Anti-Aliasing Filter).
/// На материнской плате PlayStation 1 после ЦАП CXD2922Q установлен 3-полюсный активный
/// аналоговый Low-Pass фильтр (спад -18 дБ/октаву с частотой среза около 10–12 кГц).
/// Именно он в сочетании с гауссовой интерполяцией создавал легендарный «подводный»,
/// глухой и теплый звук саундтреков Silent Hill и King's Field IV.
/// </summary>
public sealed class SpuAnalogFilter
{
    private float _cutoffHz;
    private int _sampleRate;

    // 1-й порядок (RC полюс): y[n] = b0*x[n] + b1*x[n-1] - a1*y[n-1]
    private float _rcB0, _rcB1, _rcA1;
    private float _rcX1L, _rcY1L;
    private float _rcX1R, _rcY1R;

    // 2-й порядок (Sallen-Key биквадрат):
    private float _bqB0, _bqB1, _bqB2, _bqA1, _bqA2;
    private float _bqX1L, _bqX2L, _bqY1L, _bqY2L;
    private float _bqX1R, _bqX2R, _bqY1R, _bqY2R;

    public float CutoffHz => _cutoffHz;

    public SpuAnalogFilter(float cutoffHz = 11000f, int sampleRate = 44100)
    {
        SetParameters(cutoffHz, sampleRate);
    }

    public void Reset()
    {
        _rcX1L = _rcY1L = 0f;
        _rcX1R = _rcY1R = 0f;
        _bqX1L = _bqX2L = _bqY1L = _bqY2L = 0f;
        _bqX1R = _bqX2R = _bqY1R = _bqY2R = 0f;
    }

    public void SetParameters(float cutoffHz, int sampleRate)
    {
        _cutoffHz = Math.Clamp(cutoffHz, 1000f, sampleRate * 0.48f);
        _sampleRate = sampleRate;

        // Билинейное преобразование с pre-warping
        float w0 = 2f * MathF.PI * _cutoffHz;
        float k = 2f * sampleRate;
        float omega = 2f * sampleRate * MathF.Tan(w0 / (2f * sampleRate));

        // 1-й порядок: H(s) = omega / (s + omega)
        float rcA0 = k + omega;
        _rcB0 = omega / rcA0;
        _rcB1 = omega / rcA0;
        _rcA1 = (omega - k) / rcA0;

        // 2-й порядок Butterworth (Q = 1.0): H(s) = omega^2 / (s^2 + omega*s + omega^2)
        float omega2 = omega * omega;
        float bqA0 = (k * k) + (k * omega) + omega2;
        _bqB0 = omega2 / bqA0;
        _bqB1 = (2f * omega2) / bqA0;
        _bqB2 = omega2 / bqA0;
        _bqA1 = (2f * omega2 - 2f * k * k) / bqA0;
        _bqA2 = ((k * k) - (k * omega) + omega2) / bqA0;
    }

    /// <summary>
    /// Фильтрация стерео канала (in-place).
    /// </summary>
    public void ProcessStereo(Span<float> left, Span<float> right)
    {
        int length = Math.Min(left.Length, right.Length);
        for (int i = 0; i < length; i++)
        {
            left[i] = ProcessSampleLeft(left[i]);
            right[i] = ProcessSampleRight(right[i]);
        }
    }

    /// <summary>
    /// Фильтрация моно канала (in-place).
    /// </summary>
    public void ProcessMono(Span<float> channel)
    {
        for (int i = 0; i < channel.Length; i++)
        {
            channel[i] = ProcessSampleLeft(channel[i]);
        }
    }

    private float ProcessSampleLeft(float input)
    {
        // Каскад: 1-й порядок
        float y1 = _rcB0 * input + _rcB1 * _rcX1L - _rcA1 * _rcY1L;
        _rcX1L = input;
        _rcY1L = y1;

        // Каскад: 2-й порядок
        float y2 = _bqB0 * y1 + _bqB1 * _bqX1L + _bqB2 * _bqX2L - _bqA1 * _bqY1L - _bqA2 * _bqY2L;
        _bqX2L = _bqX1L; _bqX1L = y1;
        _bqY2L = _bqY1L; _bqY1L = y2;

        return y2;
    }

    private float ProcessSampleRight(float input)
    {
        // Каскад: 1-й порядок
        float y1 = _rcB0 * input + _rcB1 * _rcX1R - _rcA1 * _rcY1R;
        _rcX1R = input;
        _rcY1R = y1;

        // Каскад: 2-й порядок
        float y2 = _bqB0 * y1 + _bqB1 * _bqX1R + _bqB2 * _bqX2R - _bqA1 * _bqY1R - _bqA2 * _bqY2R;
        _bqX2R = _bqX1R; _bqX1R = y1;
        _bqY2R = _bqY1R; _bqY1R = y2;

        return y2;
    }
}
