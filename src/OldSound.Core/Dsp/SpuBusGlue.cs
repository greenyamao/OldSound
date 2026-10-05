using System;

namespace OldSound.Core.Dsp;

/// <summary>
/// Эмуляция аналогового характера шины суммирования SPU и мягкой консольной компрессии.
/// Плавное насыщение с мягким коленом (Soft-Knee Saturation), полностью исключающее клиппинг.
/// </summary>
public static class SpuBusGlue
{
    /// <summary>
    /// Применяет мягкую аналоговую сатурацию шины SPU.
    /// Сигнал ниже порога колена остается абсолютно линейным, а пики плавно скругляются к 1.0.
    /// </summary>
    /// <param name="channel">Сэмплы канала [-1.0 .. 1.0]</param>
    /// <param name="intensity">Интенсивность насыщения (0.0 = выкл, 1.0 = норма)</param>
    public static void Process(Span<float> channel, float intensity = 1.0f)
    {
        if (intensity <= 0.01f) return;

        // Порог мягкого колена: при intensity = 1.0 скругление начинается с 0.75 (-2.5 dBFS)
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
