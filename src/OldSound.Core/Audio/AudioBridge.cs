using System;
using System.Diagnostics;
using System.IO;

namespace OldSound.Core.Audio;

/// <summary>
/// Обеспечивает универсальный мост для загрузки и сохранения аудиофайлов любого формата (WAV, MP3, OGG, FLAC и т.д.).
/// Для WAV использует нативный высокоскоростной код, для остальных форматов — прозрачный процесс FFmpeg.
/// </summary>
public static class AudioBridge
{
    private static bool? _ffmpegAvailable;

    public static bool IsFFmpegAvailable()
    {
        if (_ffmpegAvailable.HasValue)
            return _ffmpegAvailable.Value;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                proc.WaitForExit(2000);
                _ffmpegAvailable = proc.ExitCode == 0;
            }
            else
            {
                _ffmpegAvailable = false;
            }
        }
        catch
        {
            _ffmpegAvailable = false;
        }

        return _ffmpegAvailable.Value;
    }

    /// <summary>
    /// Загружает аудиофайл любого поддерживаемого формата в память.
    /// </summary>
    public static AudioBuffer Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Файл '{filePath}' не найден.");

        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (ext == ".wav")
        {
            using var stream = File.OpenRead(filePath);
            return WavCodec.Read(stream);
        }

        if (!IsFFmpegAvailable())
            throw new InvalidOperationException($"Для открытия формата '{ext}' требуется установленный в PATH FFmpeg.");

        // Декодируем в WAV поток через FFmpeg
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{filePath}\" -f wav -",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Не удалось запустить процесс FFmpeg.");

        using var ms = new MemoryStream();
        proc.StandardOutput.BaseStream.CopyTo(ms);
        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            string err = proc.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Ошибка декодирования FFmpeg: {err}");
        }

        ms.Position = 0;
        return WavCodec.Read(ms);
    }

    /// <summary>
    /// Сохраняет аудио-буфер в файл целевого формата.
    /// </summary>
    public static void Save(AudioBuffer buffer, string filePath, int mp3BitrateKbps = 320, int wavBitsPerSample = 16)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (ext == ".wav")
        {
            using var stream = File.Create(filePath);
            WavCodec.Write(buffer, stream, wavBitsPerSample);
            return;
        }

        if (!IsFFmpegAvailable())
            throw new InvalidOperationException($"Для кодирования в формат '{ext}' требуется установленный в PATH FFmpeg.");

        // Сначала генерируем WAV в памяти
        using var wavMs = new MemoryStream();
        WavCodec.Write(buffer, wavMs, 16);
        wavMs.Position = 0;

        string extraArgs = ext switch
        {
            ".mp3" => $"-b:a {mp3BitrateKbps}k",
            ".ogg" => "-c:a libvorbis -q:a 7",
            ".flac" => "-c:a flac",
            ".m4a" or ".aac" => "-c:a aac -b:a 256k",
            _ => ""
        };

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -f wav -i - {extraArgs} \"{filePath}\"",
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Не удалось запустить процесс FFmpeg для записи.");

        wavMs.CopyTo(proc.StandardInput.BaseStream);
        proc.StandardInput.BaseStream.Flush();
        proc.StandardInput.BaseStream.Close();

        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            string err = proc.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Ошибка кодирования FFmpeg: {err}");
        }
    }
}
