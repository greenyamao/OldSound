using System;

namespace OldSound.Core.Dsp;

/// <summary>
/// Аппаратно точная эмуляция алгоритма Sony ADPCM (VAG format) для звукового чипа PS1 SPU.
/// Содержит точные формулы декодирования, 5 пар аппаратных коэффициентов предсказания
/// и настраиваемый энкодер с поддержкой исторической деградации (Authentic VAG Grit).
/// </summary>
public sealed class SonyAdpcm
{
    public const int SamplesPerBlock = 28;
    public const int BytesPerBlock = 16;

    // Оригинальные аппаратные коэффициенты предсказания SPU (fixed-point Q6, деление на 64)
    public static readonly (int f0, int f1)[] Predictors = new[]
    {
        (0, 0),        // Filter 0
        (60, 0),       // Filter 1
        (115, -52),    // Filter 2
        (98, -55),     // Filter 3
        (122, -60)     // Filter 4
    };

    private int _hist1; // old sample
    private int _hist2; // older sample

    public SonyAdpcm()
    {
        Reset();
    }

    public void Reset()
    {
        _hist1 = 0;
        _hist2 = 0;
    }

    /// <summary>
    /// Декодирует один 16-байтный блок VAG в 28 16-битных PCM сэмплов по оригинальной аппаратной формуле SPU.
    /// </summary>
    public void DecodeBlock(ReadOnlySpan<byte> block, Span<short> output28)
    {
        if (block.Length < BytesPerBlock)
            throw new ArgumentException("Размер блока VAG должен быть 16 байт.", nameof(block));
        if (output28.Length < SamplesPerBlock)
            throw new ArgumentException("Выходной буфер должен содержать минимум 28 сэмплов.", nameof(output28));

        int shift = 12 - (block[0] & 0x0F);
        int filter = (block[0] >> 4) & 0x07;
        if (filter > 4) filter = 4;

        int f0 = Predictors[filter].f0;
        int f1 = Predictors[filter].f1;

        int sampleIdx = 0;
        for (int byteIdx = 2; byteIdx < 16; byteIdx++)
        {
            byte b = block[byteIdx];

            // Младший ниббл (более ранний во времени сэмпл)
            int nibble1 = b & 0x0F;
            if (nibble1 >= 8) nibble1 -= 16; // знаковое расширение 4-bit -> int (-8..+7)

            int s1 = (nibble1 << shift) + ((_hist1 * f0 + _hist2 * f1 + 32) / 64);
            s1 = Math.Clamp(s1, short.MinValue, short.MaxValue);
            _hist2 = _hist1;
            _hist1 = s1;
            output28[sampleIdx++] = (short)s1;

            // Старший ниббл (более поздний во времени сэмпл)
            int nibble2 = (b >> 4) & 0x0F;
            if (nibble2 >= 8) nibble2 -= 16;

            int s2 = (nibble2 << shift) + ((_hist1 * f0 + _hist2 * f1 + 32) / 64);
            s2 = Math.Clamp(s2, short.MinValue, short.MaxValue);
            _hist2 = _hist1;
            _hist1 = s2;
            output28[sampleIdx++] = (short)s2;
        }
    }

    /// <summary>
    /// Кодирует 28 сэмплов PCM в один 16-байтный блок VAG.
    /// </summary>
    /// <param name="input28">28 сэмплов PCM 16-bit</param>
    /// <param name="outputBlock">16-байтный буфер для блока VAG</param>
    /// <param name="flags">Флаги VAG (0 = обычный блок, 1 = конец, 2 = зациклен)</param>
    /// <param name="grit">Уровень зернистости квантования (0.0 = чистый Hi-Fi подбор, >0.0 = аутентичный «хруст»)</param>
    /// <param name="authenticMode">Использовать исторический целочисленный алгоритм Sony SDK (encvag/MFAudio)</param>
    public void EncodeBlock(ReadOnlySpan<short> input28, Span<byte> outputBlock, byte flags = 0, float grit = 0.0f, bool authenticMode = false)
    {
        if (input28.Length < SamplesPerBlock)
            throw new ArgumentException("Входной блок должен содержать минимум 28 сэмплов.", nameof(input28));
        if (outputBlock.Length < BytesPerBlock)
            throw new ArgumentException("Размер выходного блока должен быть минимум 16 байт.", nameof(outputBlock));

        int bestFilter = 0;
        int bestShift = 0;
        long lowestError = long.MaxValue;
        int[] bestNibbles = new int[SamplesPerBlock];
        int bestHist1 = _hist1;
        int bestHist2 = _hist2;

        Span<int> tempNibbles = stackalloc int[SamplesPerBlock];

        // В историческом режиме Sony SDK encvag использовались первые 3-4 фильтра
        int maxFilters = authenticMode ? 4 : 5;

        for (int f = 0; f < maxFilters; f++)
        {
            int f0 = Predictors[f].f0;
            int f1 = Predictors[f].f1;

            int maxDiff = 0;
            int h1 = _hist1;
            int h2 = _hist2;

            for (int i = 0; i < SamplesPerBlock; i++)
            {
                int predicted = (h1 * f0 + h2 * f1 + 32) / 64;
                int diff = Math.Abs(input28[i] - predicted);
                if (diff > maxDiff) maxDiff = diff;
                h2 = h1;
                h1 = input28[i];
            }

            int scale = 0;
            while (scale < 12 && (maxDiff >> scale) > 7)
            {
                scale++;
            }

            // Зернистость (grit): огрубление масштаба квантования дельт
            if (grit > 0.001f)
            {
                int gritOffset = (int)Math.Round(grit * 2.0f);
                scale = Math.Clamp(scale + gritOffset, 0, 12);
            }

            int shift = 12 - scale;

            int minShiftTry = authenticMode ? shift : Math.Max(0, shift - 1);
            int maxShiftTry = authenticMode ? shift : shift;

            for (int tryShift = minShiftTry; tryShift <= maxShiftTry; tryShift++)
            {
                int decodeShift = 12 - tryShift;
                long totalError = 0;
                h1 = _hist1;
                h2 = _hist2;

                for (int i = 0; i < SamplesPerBlock; i++)
                {
                    int predicted = (h1 * f0 + h2 * f1 + 32) / 64;
                    int diff = input28[i] - predicted;

                    int rawNibble;
                    if (decodeShift > 0)
                    {
                        if (authenticMode)
                        {
                            // Историческое целочисленное усечение Sony
                            rawNibble = diff >> decodeShift;
                        }
                        else
                        {
                            int half = 1 << (decodeShift - 1);
                            rawNibble = (diff >= 0) ? (diff + half) >> decodeShift : -((-diff + half) >> decodeShift);
                        }
                    }
                    else
                    {
                        rawNibble = diff;
                    }

                    rawNibble = Math.Clamp(rawNibble, -8, 7);
                    tempNibbles[i] = rawNibble;

                    int decoded = (rawNibble << decodeShift) + predicted;
                    decoded = Math.Clamp(decoded, short.MinValue, short.MaxValue);

                    int err = input28[i] - decoded;
                    totalError += (long)err * err;

                    h2 = h1;
                    h1 = decoded;
                }

                if (totalError < lowestError)
                {
                    lowestError = totalError;
                    bestFilter = f;
                    bestShift = tryShift;
                    bestHist1 = h1;
                    bestHist2 = h2;
                    tempNibbles.CopyTo(bestNibbles);
                }
            }
        }

        _hist1 = bestHist1;
        _hist2 = bestHist2;

        outputBlock[0] = (byte)((bestFilter << 4) | (bestShift & 0x0F));
        outputBlock[1] = flags;

        for (int i = 0; i < 14; i++)
        {
            int n1 = bestNibbles[i * 2] & 0x0F;
            int n2 = bestNibbles[i * 2 + 1] & 0x0F;
            outputBlock[2 + i] = (byte)(n1 | (n2 << 4));
        }
    }

    /// <summary>
    /// Прогоняет поток 16-битных PCM сэмплов через цикл аппаратной компрессии Sony ADPCM и декодирования.
    /// </summary>
    public short[] ProcessPcm(ReadOnlySpan<short> inputPcm, float grit = 0.0f, bool authenticMode = false)
    {
        Reset();

        int blockCount = (inputPcm.Length + SamplesPerBlock - 1) / SamplesPerBlock;
        var result = new short[blockCount * SamplesPerBlock];

        Span<byte> block = stackalloc byte[BytesPerBlock];
        Span<short> inputChunk = stackalloc short[SamplesPerBlock];
        Span<short> decodedChunk = stackalloc short[SamplesPerBlock];

        var encoder = new SonyAdpcm();
        var decoder = new SonyAdpcm();

        for (int b = 0; b < blockCount; b++)
        {
            int srcOffset = b * SamplesPerBlock;
            int count = Math.Min(SamplesPerBlock, inputPcm.Length - srcOffset);

            inputChunk.Clear();
            inputPcm.Slice(srcOffset, count).CopyTo(inputChunk);

            encoder.EncodeBlock(inputChunk, block, 0, grit, authenticMode);
            decoder.DecodeBlock(block, decodedChunk);

            decodedChunk.CopyTo(result.AsSpan(b * SamplesPerBlock, SamplesPerBlock));
        }

        if (result.Length == inputPcm.Length)
            return result;

        var trimmed = new short[inputPcm.Length];
        Array.Copy(result, trimmed, inputPcm.Length);
        return trimmed;
    }
}
