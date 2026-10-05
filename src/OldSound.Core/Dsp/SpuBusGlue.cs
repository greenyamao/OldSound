using System;

namespace OldSound.Core.Dsp;

/// <summary>
/// Эмуляция нелинейности шины микширования SPU и мягкой компрессии динамического диапазона.
/// В аппаратном чипе SPU каналы суммировались в целочисленный 16-битный аккумулятор.
/// При высокой громкости или плотных оркестровках звук не клипповал жестко с цифровым треском,
/// а мягко насыщался аналоговым выходным каскадом ЦАП, связывая инструменты в единый микс («клей»).
/// </summary>
public static class SpuBusGlue
{
    /// <summary>
    /// Применяет мягкую аналоговую компрессию и сатурацию шины SPU.
    /// </summary>
    /// <param name="channel">Сэмплы канала (нормализованный float [-1.0 .. 1.0])</param>
    /// <param name="intensity">Интенсивность насыщения (1.0 = норма, >1.0 = более плотный «жмыхнутый» звук)</param>
    public static void Process(Span<float> channel, float intensity = 1.0f)
    {
        if (intensity <= 0.01f) return;

        float drive = 1.0f + 0.35f * intensity;
        float norm = MathF.Tanh(drive);

        for (int i = 0; i < channel.Length; i++)
        {
            float s = channel[i] * drive;
            // Мягкое скругление пиков консоли (cubic soft saturation)
            float sat;
            if (s > 1.2f)
                sat = 1.0f;
            else if (s < -1.2f)
                sat = -1.0f;
            else
                sat = MathF.Tanh(s) / norm;

            channel[i] = sat;
        }
    }
}
