using System;
using OldSound.Core.Dsp;
using Xunit;

namespace OldSound.Tests;

public class SpuGaussianInterpolatorTests
{
    [Fact]
    public void Table_Length_Is512()
    {
        Assert.Equal(512, SpuGaussianInterpolator.Table.Length);
    }

    [Fact]
    public void Table_FourCoefficientsSum_MatchesHardwareGlitch()
    {
        // В документации Martin Korth (nocash psxspx):
        // Сумма четверок коэффициентов для любого i в диапазоне 00h..FFh равна 7F7Fh..7F81h (32639..32641).
        for (int i = 0; i < 256; i++)
        {
            int sum = SpuGaussianInterpolator.Table[0x0FF - i]
                    + SpuGaussianInterpolator.Table[0x1FF - i]
                    + SpuGaussianInterpolator.Table[0x100 + i]
                    + SpuGaussianInterpolator.Table[0x000 + i];

            Assert.InRange(sum, 32639, 32641);
        }
    }

    [Fact]
    public void Interpolate_ConstantDC_ProducesStableOutput()
    {
        short val = 10000;
        // Для постоянного сигнала интерполяция должна давать значение близкое к исходному
        for (int phase = 0; phase < 256; phase += 17)
        {
            short outVal = SpuGaussianInterpolator.Interpolate(val, val, val, val, phase);
            // Из-за суммы 32640 / 32768 (~0.996) значение слегка масштабируется (на 0.4%)
            Assert.InRange(outVal, 9900, 10050);
        }
    }

    [Fact]
    public void Process_HighFrequencySignal_AttenuatesHigherFrequencies()
    {
        // Сравниваем прохождение 1 кГц и 18 кГц через тракт ЦАП Гаусса
        int sampleRate = 44100;
        int count = 4410; // 0.1 сек

        short[] tone1k = new short[count];
        short[] tone18k = new short[count];

        for (int i = 0; i < count; i++)
        {
            tone1k[i] = (short)(MathF.Sin(2.0f * MathF.PI * 1000.0f * i / sampleRate) * 15000.0f);
            tone18k[i] = (short)(MathF.Sin(2.0f * MathF.PI * 18000.0f * i / sampleRate) * 15000.0f);
        }

        short[] out1k = SpuGaussianInterpolator.Process(tone1k, sampleRate, sampleRate, sampleRate);
        short[] out18k = SpuGaussianInterpolator.Process(tone18k, sampleRate, sampleRate, sampleRate);

        // Считаем амплитуду пиков
        float max1k = 0;
        float max18k = 0;
        for (int i = 50; i < count - 50; i++)
        {
            max1k = MathF.Max(max1k, MathF.Abs(out1k[i]));
            max18k = MathF.Max(max18k, MathF.Abs(out18k[i]));
        }

        // Гауссов фильтр PS1 SPU ослабляет 18 кГц относительно 1 кГц при 44.1k примерно на -7 dB (фактор ~0.45)
        Assert.True(max18k < max1k * 0.55f, $"Ожидалось ослабление 18 кГц: 1к={max1k}, 18к={max18k}");

        // При 20 кГц подавление еще сильнее
        short[] tone20k = new short[count];
        for (int i = 0; i < count; i++)
        {
            tone20k[i] = (short)(MathF.Sin(2.0f * MathF.PI * 20000.0f * i / sampleRate) * 15000.0f);
        }
        short[] out20k = SpuGaussianInterpolator.Process(tone20k, sampleRate, sampleRate, sampleRate);
        float max20k = 0;
        for (int i = 50; i < count - 50; i++)
        {
            max20k = MathF.Max(max20k, MathF.Abs(out20k[i]));
        }
        Assert.True(max20k < max18k, $"Частота 20 кГц ({max20k}) должна подавляться сильнее, чем 18 кГц ({max18k})");
    }
}
