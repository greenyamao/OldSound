using System;
using System.IO;
using System.Text;

namespace OldSound.Core.Audio;

/// <summary>
/// Нативный высокопроизводительный кодек RIFF WAV.
/// Поддерживает чтение 16/24/32-bit PCM и 32-bit IEEE float, а также запись 16/24-bit PCM.
/// </summary>
public static class WavCodec
{
    private const uint ChunkRiff = 0x46464952; // 'RIFF'
    private const uint ChunkWave = 0x45564157; // 'WAVE'
    private const uint ChunkFmt  = 0x20746D66; // 'fmt '
    private const uint ChunkData = 0x61746164; // 'data'

    public static AudioBuffer Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        uint riff = reader.ReadUInt32();
        if (riff != ChunkRiff)
            throw new InvalidDataException("Файл не является валидным RIFF контейнером.");

        uint fileSize = reader.ReadUInt32();
        uint wave = reader.ReadUInt32();
        if (wave != ChunkWave)
            throw new InvalidDataException("Формат контейнера не WAVE.");

        ushort audioFormat = 0;
        ushort channels = 0;
        uint sampleRate = 0;
        ushort bitsPerSample = 0;
        byte[]? rawAudioData = null;

        while (stream.Position < stream.Length)
        {
            uint chunkId = reader.ReadUInt32();
            uint chunkSize = reader.ReadUInt32();

            if (chunkId == ChunkFmt)
            {
                audioFormat = reader.ReadUInt16();
                channels = reader.ReadUInt16();
                sampleRate = reader.ReadUInt32();
                uint byteRate = reader.ReadUInt32();
                ushort blockAlign = reader.ReadUInt16();
                bitsPerSample = reader.ReadUInt16();

                // Если есть расширенный заголовок (WAVEFORMATEXTENSIBLE)
                if (chunkSize > 16)
                {
                    int extra = (int)(chunkSize - 16);
                    if (audioFormat == 0xFFFE && extra >= 24)
                    {
                        ushort validBits = reader.ReadUInt16();
                        uint channelMask = reader.ReadUInt32();
                        ushort subFormat = reader.ReadUInt16();
                        reader.ReadBytes(extra - 8);
                        audioFormat = subFormat;
                    }
                    else
                    {
                        reader.ReadBytes(extra);
                    }
                }
            }
            else if (chunkId == ChunkData)
            {
                if (chunkSize == 0xFFFFFFFF || (int)chunkSize < 0)
                {
                    // Потоковый WAV (например, из pipe FFmpeg): читаем до конца потока
                    using var tempMs = new MemoryStream();
                    stream.CopyTo(tempMs);
                    rawAudioData = tempMs.ToArray();
                }
                else
                {
                    rawAudioData = reader.ReadBytes((int)chunkSize);
                }
                // Чанк data найден — выходим из цикла поиска
                break;
            }
            else
            {
                // Пропускаем метаданные и вспомогательные чанки (JUNK, LIST, bext и т.д.)
                stream.Seek(chunkSize, SeekOrigin.Current);
            }
        }

        if (rawAudioData == null)
            throw new InvalidDataException("В WAV файле отсутствует чанк данных 'data'.");
        if (channels == 0 || sampleRate == 0)
            throw new InvalidDataException("Некорректный заголовок формата WAV.");

        int bytesPerSample = bitsPerSample / 8;
        int frameSize = channels * bytesPerSample;
        int sampleCount = rawAudioData.Length / frameSize;

        var buffer = new AudioBuffer(channels, (int)sampleRate, sampleCount);

        // Парсинг сэмплов в зависимости от битности и формата
        ReadOnlySpan<byte> span = rawAudioData;

        for (int ch = 0; ch < channels; ch++)
        {
            var channelSpan = buffer.GetChannelSpan(ch);

            if (audioFormat == 3 && bitsPerSample == 32) // IEEE 32-bit Float
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    int offset = (i * channels + ch) * 4;
                    float sample = BitConverter.ToSingle(span.Slice(offset, 4));
                    channelSpan[i] = sample;
                }
            }
            else if (bitsPerSample == 16) // 16-bit PCM
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    int offset = (i * channels + ch) * 2;
                    short sample = (short)(span[offset] | (span[offset + 1] << 8));
                    channelSpan[i] = sample / 32768.0f;
                }
            }
            else if (bitsPerSample == 24) // 24-bit PCM
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    int offset = (i * channels + ch) * 3;
                    int sample = (span[offset] << 8) | (span[offset + 1] << 16) | (span[offset + 2] << 24);
                    channelSpan[i] = (sample >> 8) / 8388608.0f;
                }
            }
            else if (bitsPerSample == 32) // 32-bit PCM
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    int offset = (i * channels + ch) * 4;
                    int sample = BitConverter.ToInt32(span.Slice(offset, 4));
                    channelSpan[i] = sample / 2147483648.0f;
                }
            }
            else
            {
                throw new NotSupportedException($"Битность {bitsPerSample}-bit с форматом {audioFormat} не поддерживается нативно.");
            }
        }

        return buffer;
    }

    public static void Write(AudioBuffer buffer, Stream stream, int bitsPerSample = 16)
    {
        if (bitsPerSample != 16 && bitsPerSample != 24)
            throw new ArgumentException("Поддерживается запись в 16-bit или 24-bit PCM.", nameof(bitsPerSample));

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        int bytesPerSample = bitsPerSample / 8;
        int frameSize = buffer.Channels * bytesPerSample;
        int dataSize = buffer.LengthSamples * frameSize;
        int riffSize = 36 + dataSize;

        // RIFF Header
        writer.Write(ChunkRiff);
        writer.Write((uint)riffSize);
        writer.Write(ChunkWave);

        // 'fmt ' Chunk
        writer.Write(ChunkFmt);
        writer.Write((uint)16); // Chunk size for PCM
        writer.Write((ushort)1); // PCM
        writer.Write((ushort)buffer.Channels);
        writer.Write((uint)buffer.SampleRate);
        writer.Write((uint)(buffer.SampleRate * frameSize)); // Byte rate
        writer.Write((ushort)frameSize); // Block align
        writer.Write((ushort)bitsPerSample);

        // 'data' Chunk
        writer.Write(ChunkData);
        writer.Write((uint)dataSize);

        // Запись интерливированных сэмплов
        if (bitsPerSample == 16)
        {
            var channels = new float[buffer.Channels][];
            for (int c = 0; c < buffer.Channels; c++)
                channels[c] = buffer.GetChannelArray(c);

            for (int i = 0; i < buffer.LengthSamples; i++)
            {
                for (int c = 0; c < buffer.Channels; c++)
                {
                    float val = channels[c][i] * 32767.0f;
                    if (val > 32767.0f) val = 32767.0f;
                    else if (val < -32768.0f) val = -32768.0f;
                    short s = (short)MathF.Round(val);
                    writer.Write(s);
                }
            }
        }
        else if (bitsPerSample == 24)
        {
            var channels = new float[buffer.Channels][];
            for (int c = 0; c < buffer.Channels; c++)
                channels[c] = buffer.GetChannelArray(c);

            for (int i = 0; i < buffer.LengthSamples; i++)
            {
                for (int c = 0; c < buffer.Channels; c++)
                {
                    float val = channels[c][i] * 8388607.0f;
                    if (val > 8388607.0f) val = 8388607.0f;
                    else if (val < -8388608.0f) val = -8388608.0f;
                    int sample24 = (int)MathF.Round(val);
                    writer.Write((byte)(sample24 & 0xFF));
                    writer.Write((byte)((sample24 >> 8) & 0xFF));
                    writer.Write((byte)((sample24 >> 16) & 0xFF));
                }
            }
        }
    }
}
