using System;

namespace OldSound.Core.Dsp;

/// <summary>
/// Historical 8-bit SDX2 (Square-Root Delta) codec implementation used in 3DO Portfolio OS
/// (CLIO / Opera DSP) for background music compression (e.g., Four-Sight 1995 soundtrack by Yuji Nomi).
/// 
/// Mathematical model (3DO Opera DSP):
///   Δ[n] = x[n] - y_hat[n-1]
///   b[n] = clip_[-128, 127]( sgn(Δ[n]) * round( sqrt(|Δ[n]| / 2) ) )
/// 
/// Decoding using lookup table (Opera DSP LUT):
///   Δ_rec[n] = sgn(b[n]) * 2 * (b[n])^2
///   y_hat[n] = clip_[-32768, 32767]( y_hat[n-1] + Δ_rec[n] )
/// 
/// Characteristics:
///   - 2:1 fixed compression (16-bit PCM to 8-bit delta stream).
///   - Preserves high resolution and low noise floor during quiet passages.
///   - Produces natural slew-rate limiting on sharp percussive and orchestral attacks.
/// </summary>
public static class Sdx2Codec
{
    // Precalculated quadratic delta table for all 256 values b[n] in [-128, 127]
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
    /// Encodes 16-bit linear PCM into an 8-bit SDX2 delta stream.
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

            // Local decoder feedback to minimize cumulative error
            short recDelta = Lut[b + 128];
            accum = Math.Clamp(accum + recDelta, short.MinValue, short.MaxValue);
        }

        return output;
    }

    /// <summary>
    /// Decodes an 8-bit SDX2 delta stream back into 16-bit linear PCM.
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
    /// Full encode and decode cycle for SDX2.
    /// </summary>
    public static short[] ProcessPcm(ReadOnlySpan<short> pcm)
    {
        sbyte[] encoded = Encode(pcm);
        return Decode(encoded);
    }

    /// <summary>
    /// Software upsampler modeling 3DO half_rate.dsp microcode (22,050 Hz -> 44,100 Hz).
    /// Performs linear sample interpolation without anti-imaging filtering,
    /// creating sinc^2 roll-off (-3.18 dB at 11 kHz) and characteristic glassy mirror harmonics in 11-22 kHz.
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
