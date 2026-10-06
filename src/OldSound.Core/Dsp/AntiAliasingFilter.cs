using System;

namespace OldSound.Core.Dsp;

/// <summary>
/// Linear-phase Blackman-Harris FIR anti-aliasing filter (N=129).
/// Matches the studio mastering chain used for Four-Sight (1995, 3DO / PlayStation),
/// eliminating foldover aliasing when downsampling to 22,050 Hz with >80 dB stopband attenuation.
/// </summary>
public static class AntiAliasingFilter
{
    public const int DefaultTaps = 129;

    /// <summary>
    /// Calculates coefficients for a linear-phase low-pass FIR filter with Blackman-Harris window.
    /// Equivalent to scipy.signal.firwin(numTaps, cutoff / (0.5 * fs), window='blackmanharris').
    /// </summary>
    public static float[] DesignBlackmanHarrisFir(int sampleRate, float cutoffHz, int numTaps = DefaultTaps)
    {
        if (numTaps % 2 == 0) numTaps++; // Odd order for symmetric Type I filter
        float[] h = new float[numTaps];
        int m = numTaps - 1;
        float center = m / 2.0f;
        float nyquist = sampleRate * 0.5f;
        float wc = Math.Clamp(cutoffHz / nyquist, 0.01f, 0.99f);

        const float A0 = 0.35875f;
        const float A1 = 0.48829f;
        const float A2 = 0.14128f;
        const float A3 = 0.01168f;

        float sum = 0f;
        for (int i = 0; i < numTaps; i++)
        {
            float d = i - center;
            float sinc = MathF.Abs(d) < 1e-6f
                ? wc
                : MathF.Sin(MathF.PI * wc * d) / (MathF.PI * d);

            float phi = 2f * MathF.PI * i / m;
            float w = A0 - A1 * MathF.Cos(phi) + A2 * MathF.Cos(2f * phi) - A3 * MathF.Cos(3f * phi);

            h[i] = sinc * w;
            sum += h[i];
        }

        if (MathF.Abs(sum) > 1e-9f)
        {
            float invSum = 1f / sum;
            for (int i = 0; i < numTaps; i++)
            {
                h[i] *= invSum;
            }
        }

        return h;
    }

    /// <summary>
    /// Applies anti-aliasing filter with zero phase distortion (group delay compensated).
    /// </summary>
    public static short[] Apply(ReadOnlySpan<short> input, int sampleRate, float cutoffHz)
    {
        int len = input.Length;
        if (len == 0) return Array.Empty<short>();

        int numTaps = DefaultTaps;
        float[] h = DesignBlackmanHarrisFir(sampleRate, cutoffHz, numTaps);
        int halfTaps = numTaps / 2;

        var output = new short[len];

        // Optimized convolution with boundary clamping
        for (int i = 0; i < len; i++)
        {
            float acc = 0f;

            // Interior zone without boundary checks
            int offset = i - halfTaps;
            if (offset >= 0 && offset + numTaps <= len)
            {
                var slice = input.Slice(offset, numTaps);
                for (int k = 0; k < numTaps; k++)
                {
                    acc += slice[k] * h[k];
                }
            }
            else
            {
                for (int k = 0; k < numTaps; k++)
                {
                    int inIdx = Math.Clamp(offset + k, 0, len - 1);
                    acc += input[inIdx] * h[k];
                }
            }

            output[i] = (short)Math.Clamp((int)MathF.Round(acc), short.MinValue, short.MaxValue);
        }

        return output;
    }
}
