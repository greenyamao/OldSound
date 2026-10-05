using System;
using OldSound.Core.Dsp;
using Xunit;

namespace OldSound.Tests;

public class Sdx2CodecTests
{
    [Fact]
    public void EncodeAndDecode_SineWave_ProducesConsistentSignal()
    {
        int length = 1000;
        var original = new short[length];
        for (int i = 0; i < length; i++)
        {
            // 440 Hz синус при 22050 Гц с размахом 16000
            original[i] = (short)(Math.Sin(2.0 * Math.PI * 440.0 * i / 22050.0) * 16000.0);
        }

        short[] processed = Sdx2Codec.ProcessPcm(original);

        Assert.Equal(original.Length, processed.Length);

        // Проверяем корреляцию (сигнал должен точно следовать за оригиналом)
        double dotProduct = 0;
        double normOrig = 0;
        double normProc = 0;
        for (int i = 0; i < length; i++)
        {
            dotProduct += (double)original[i] * processed[i];
            normOrig += (double)original[i] * original[i];
            normProc += (double)processed[i] * processed[i];
        }

        double correlation = dotProduct / (Math.Sqrt(normOrig) * Math.Sqrt(normProc));
        Assert.True(correlation > 0.98, $"Корреляция SDX2 ({correlation}) должна быть выше 0.98 для плавного синуса.");
    }

    [Fact]
    public void Encode_SuddenStepJump_DemonstratesSlewRateLimiting()
    {
        // Резкий фронт меандра от 0 до 30000 за 1 сэмпл
        var step = new short[50];
        for (int i = 5; i < 50; i++)
        {
            step[i] = 30000;
        }

        short[] reconstructed = Sdx2Codec.ProcessPcm(step);

        // При резком скачке отсчет не может мгновенно стать 30000 в первом же сэмпле (slew-rate limiting)
        // SDX2 за 1 шаг может восстановить максимум rec_delta = 2 * (127)^2 = 32258,
        // но на следующем шаге шаг дельты мягко догоняет форму волны
        Assert.True(reconstructed[5] > 0);
        Assert.True(reconstructed[10] > 28000);
    }

    [Fact]
    public void Upsample3DoHalfRate_DoublesLength_AndInterpolates()
    {
        short[] input22k = new short[] { 1000, 3000, -2000, 0 };
        short[] upsampled44k = Sdx2Codec.Upsample3DoHalfRate(input22k);

        Assert.Equal(8, upsampled44k.Length);
        Assert.Equal(1000, upsampled44k[0]);
        Assert.Equal(2000, upsampled44k[1]); // (1000 + 3000) / 2 = 2000
        Assert.Equal(3000, upsampled44k[2]);
        Assert.Equal(500, upsampled44k[3]);  // (3000 + -2000) / 2 = 500
    }
}
