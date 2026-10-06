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
    private static string? _resolvedFFmpegPath;

    /// <summary>
    /// Находит путь к бинарнику FFmpeg: сначала проверяет папку приложения, затем tools, затем AppData, и лишь в конце системный PATH.
    /// </summary>
    public static string? FindFFmpegBinary()
    {
        if (_resolvedFFmpegPath != null)
            return _resolvedFFmpegPath;

        // 1. Рядом с исполняемым файлом (portable dist)
        string appDir = AppContext.BaseDirectory;
        string localPath = Path.Combine(appDir, "ffmpeg.exe");
        if (File.Exists(localPath))
        {
            _resolvedFFmpegPath = localPath;
            return _resolvedFFmpegPath;
        }

        // 2. В подпапках tools или bin
        string toolsPath = Path.Combine(appDir, "tools", "ffmpeg.exe");
        if (File.Exists(toolsPath))
        {
            _resolvedFFmpegPath = toolsPath;
            return _resolvedFFmpegPath;
        }

        string binPath = Path.Combine(appDir, "bin", "ffmpeg.exe");
        if (File.Exists(binPath))
        {
            _resolvedFFmpegPath = binPath;
            return _resolvedFFmpegPath;
        }

        // 3. В профиле пользователя %LOCALAPPDATA%\OldSound\ffmpeg.exe
        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OldSound", "ffmpeg.exe");
        if (File.Exists(appDataPath))
        {
            _resolvedFFmpegPath = appDataPath;
            return _resolvedFFmpegPath;
        }

        // 4. Автоматическая распаковка встроенного в сборку FFmpeg (Offline Monolithic)
        string? extracted = TryExtractEmbeddedFFmpeg(appDataPath);
        if (extracted != null && File.Exists(extracted))
        {
            _resolvedFFmpegPath = extracted;
            return _resolvedFFmpegPath;
        }

        // 5. Проверка системного PATH
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
                proc.WaitForExit(1500);
                if (proc.ExitCode == 0)
                {
                    _resolvedFFmpegPath = "ffmpeg";
                    return _resolvedFFmpegPath;
                }
            }
        }
        catch { }

        return null;
    }

    private static string? TryExtractEmbeddedFFmpeg(string targetPath)
    {
        try
        {
            var asm = typeof(AudioBridge).Assembly;
            string resName = "OldSound.Core.Resources.ffmpeg.exe.gz";
            using var stream = asm.GetManifestResourceStream(resName);
            if (stream == null)
            {
                string? matchedName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("ffmpeg.exe.gz", StringComparison.OrdinalIgnoreCase));
                if (matchedName == null) return null;
                using var fallbackStream = asm.GetManifestResourceStream(matchedName);
                if (fallbackStream == null) return null;
                return ExtractGzStream(fallbackStream, targetPath);
            }

            return ExtractGzStream(stream, targetPath);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractGzStream(Stream gzStream, string targetPath)
    {
        string? dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tempFile = targetPath + ".tmp_" + Guid.NewGuid().ToString("N");
        try
        {
            using (var decompressor = new System.IO.Compression.GZipStream(gzStream, System.IO.Compression.CompressionMode.Decompress))
            using (var outFs = File.Create(tempFile))
            {
                decompressor.CopyTo(outFs);
            }

            if (File.Exists(targetPath))
            {
                try { File.Delete(targetPath); } catch { }
            }

            File.Move(tempFile, targetPath, overwrite: true);
            return targetPath;
        }
        catch
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
            return null;
        }
    }

    public static bool IsFFmpegAvailable() => FindFFmpegBinary() != null;

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

        string? ffmpegPath = FindFFmpegBinary();
        if (ffmpegPath == null)
            throw new InvalidOperationException($"Для открытия формата '{ext}' требуется FFmpeg (поместите ffmpeg.exe рядом с приложением или добавьте в PATH).");

        // Декодируем в WAV поток через FFmpeg
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
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

        string? ffmpegPath = FindFFmpegBinary();
        if (ffmpegPath == null)
            throw new InvalidOperationException($"Для кодирования в формат '{ext}' требуется FFmpeg (поместите ffmpeg.exe рядом с приложением или добавьте в PATH).");

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
            FileName = ffmpegPath,
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
