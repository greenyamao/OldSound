using System;

namespace OldSound.Core.Dsp;

/// <summary>
/// Console output stage analog reconstruction filter topology.
/// </summary>
public enum FilterTopology
{
    /// <summary>3-pole active filter of PS1 SPU (-18 dB/oct, RC + Sallen-Key Butterworth).</summary>
    ThreePoleSpu,

    /// <summary>2-pole Sallen-Key Butterworth filter (-12 dB/oct, 3DO Burr-Brown DAC standard).</summary>
    TwoPoleSallenKey
}

/// <summary>
/// Hardware analog DAC reconstruction and anti-aliasing filter.
/// PlayStation 1 features a 3-pole active low-pass filter (-18 dB/oct with ~10-12 kHz cutoff) after CXD2922Q.
/// 3DO utilizes a 2-pole Sallen-Key low-pass filter with ~20.5 kHz cutoff.
/// </summary>
public sealed class SpuAnalogFilter
{
    private float _cutoffHz;
    private int _sampleRate;
    private FilterTopology _topology;

    // 1st order (RC pole): y[n] = b0*x[n] + b1*x[n-1] - a1*y[n-1]
    private float _rcB0, _rcB1, _rcA1;
    private float _rcX1L, _rcY1L;
    private float _rcX1R, _rcY1R;

    // 2nd order (Sallen-Key biquad):
    private float _bqB0, _bqB1, _bqB2, _bqA1, _bqA2;
    private float _bqX1L, _bqX2L, _bqY1L, _bqY2L;
    private float _bqX1R, _bqX2R, _bqY1R, _bqY2R;

    public float CutoffHz => _cutoffHz;
    public FilterTopology Topology => _topology;

    public SpuAnalogFilter(float cutoffHz = 11000f, int sampleRate = 44100, FilterTopology topology = FilterTopology.ThreePoleSpu)
    {
        SetParameters(cutoffHz, sampleRate, topology);
    }

    public void Reset()
    {
        _rcX1L = _rcY1L = 0f;
        _rcX1R = _rcY1R = 0f;
        _bqX1L = _bqX2L = _bqY1L = _bqY2L = 0f;
        _bqX1R = _bqX2R = _bqY1R = _bqY2R = 0f;
    }

    public void SetParameters(float cutoffHz, int sampleRate, FilterTopology topology = FilterTopology.ThreePoleSpu)
    {
        _cutoffHz = Math.Clamp(cutoffHz, 1000f, sampleRate * 0.48f);
        _sampleRate = sampleRate;
        _topology = topology;

        // Bilinear transform with pre-warping
        float w0 = 2f * MathF.PI * _cutoffHz;
        float k = 2f * sampleRate;
        float omega = 2f * sampleRate * MathF.Tan(w0 / (2f * sampleRate));

        // 1st order: H(s) = omega / (s + omega)
        float rcA0 = k + omega;
        _rcB0 = omega / rcA0;
        _rcB1 = omega / rcA0;
        _rcA1 = (omega - k) / rcA0;

        // 2nd order:
        // For PS1 SPU: Q = 1.0 (slightly resonant knee)
        // For 3DO Sallen-Key: Q = 0.7071 (maximally flat Butterworth)
        float q = (_topology == FilterTopology.TwoPoleSallenKey) ? 0.70710678f : 1.0f;
        float omega2 = omega * omega;
        float bqA0 = (k * k) + (k * omega / q) + omega2;
        _bqB0 = omega2 / bqA0;
        _bqB1 = (2f * omega2) / bqA0;
        _bqB2 = omega2 / bqA0;
        _bqA1 = (2f * omega2 - 2f * k * k) / bqA0;
        _bqA2 = ((k * k) - (k * omega / q) + omega2) / bqA0;
    }

    /// <summary>
    /// Processes stereo channels (in-place).
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
    /// Processes mono channel (in-place).
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
        float stageInput = input;

        // If 3-pole PS1 SPU filter is active, apply preliminary RC pole
        if (_topology == FilterTopology.ThreePoleSpu)
        {
            float y1 = _rcB0 * input + _rcB1 * _rcX1L - _rcA1 * _rcY1L;
            _rcX1L = input;
            _rcY1L = y1;
            stageInput = y1;
        }

        // 2nd order Sallen-Key biquad
        float y2 = _bqB0 * stageInput + _bqB1 * _bqX1L + _bqB2 * _bqX2L - _bqA1 * _bqY1L - _bqA2 * _bqY2L;
        _bqX2L = _bqX1L; _bqX1L = stageInput;
        _bqY2L = _bqY1L; _bqY1L = y2;

        return y2;
    }

    private float ProcessSampleRight(float input)
    {
        float stageInput = input;

        if (_topology == FilterTopology.ThreePoleSpu)
        {
            float y1 = _rcB0 * input + _rcB1 * _rcX1R - _rcA1 * _rcY1R;
            _rcX1R = input;
            _rcY1R = y1;
            stageInput = y1;
        }

        float y2 = _bqB0 * stageInput + _bqB1 * _bqX1R + _bqB2 * _bqX2R - _bqA1 * _bqY1R - _bqA2 * _bqY2R;
        _bqX2R = _bqX1R; _bqX1R = stageInput;
        _bqY2R = _bqY1R; _bqY1R = y2;

        return y2;
    }
}
