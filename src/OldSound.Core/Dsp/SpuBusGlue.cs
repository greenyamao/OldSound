using System;

namespace OldSound.Core.Dsp;

/// <summary>
/// Emulation of analog SPU summing bus character and soft console compression.
/// Implements soft-knee saturation without harsh digital clipping.
/// </summary>
public static class SpuBusGlue
{
    /// <summary>
    /// Applies soft analog bus saturation.
    /// Signals below the knee threshold remain linear, while peaks are smoothly rounded.
    /// </summary>
    /// <param name="channel">Channel samples [-1.0 .. 1.0]</param>
    /// <param name="intensity">Saturation intensity (0.0 = off, 1.0 = standard)</param>
    public static void Process(Span<float> channel, float intensity = 1.0f)
    {
        if (intensity <= 0.01f) return;

        // Soft-knee threshold: at intensity = 1.0, rounding begins at 0.75 (-2.5 dBFS)
        float knee = Math.Clamp(0.85f - 0.15f * intensity, 0.5f, 0.95f);
        float width = 1.0f - knee;

        for (int i = 0; i < channel.Length; i++)
        {
            float s = channel[i];
            if (s > knee)
            {
                float over = s - knee;
                channel[i] = knee + width * MathF.Tanh(over / width);
            }
            else if (s < -knee)
            {
                float over = -s - knee;
                channel[i] = -(knee + width * MathF.Tanh(over / width));
            }
        }
    }
}
