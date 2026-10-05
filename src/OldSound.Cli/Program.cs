using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OldSound.Core.Audio;
using OldSound.Core.Pipeline;
using Spectre.Console;

namespace OldSound.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length == 0 || args[0] == "--help" || args[0] == "-h" || args[0] == "help")
        {
            PrintBanner();
            PrintHelp();
            return 0;
        }

        string command = args[0].ToLowerInvariant();

        if (command == "presets" || command == "list")
        {
            PrintBanner();
            PrintPresetsTable();
            return 0;
        }

        try
        {
            if (command == "process")
            {
                return RunProcess(args.Skip(1).ToArray());
            }
            else if (command == "batch")
            {
                return RunBatch(args.Skip(1).ToArray());
            }
            else if (File.Exists(args[0]))
            {
                // Удобный shorthand: oldsound input.mp3 [output.mp3] [--preset name]
                return RunProcess(args);
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Неизвестная команда или файл:[/red] '{args[0]}'");
                PrintHelp();
                return 1;
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"\n[bold red]Ошибка:[/bold red] {ex.Message}");
            return 1;
        }
    }

    private static void PrintBanner()
    {
        AnsiConsole.Write(
            new FigletText("OldSound")
                .Color(Color.DeepSkyBlue1));
        AnsiConsole.MarkupLine("[bold grey]Аутентичный эмулятор звукового тракта PS1 SPU и аналоговой кассеты[/]");
        AnsiConsole.MarkupLine("[grey]Sony ADPCM (VAG) • 4-Point Gaussian DAC • Cassette Saturation & Hiss[/]\n");
    }

    private static void PrintHelp()
    {
        var panel = new Panel(new Markup(
            "[bold yellow]Использование:[/]\n" +
            "  [cyan]oldsound process[/] <input-file> -o <output-file> [параметры]\n" +
            "  [cyan]oldsound batch[/] <input-dir> -o <output-dir> [параметры]\n" +
            "  [cyan]oldsound[/] <input-file> [output-file] [--preset <имя>]\n" +
            "  [cyan]oldsound presets[/] (список доступных профилей звучания)\n\n" +
            "[bold yellow]Основные параметры:[/]\n" +
            "  [green]--preset <имя>[/]       Профиль звучания (по умолчанию: [yellow]psx-xa-37k[/])\n" +
            "  [green]-o, --output <путь>[/]   Путь к выходному файлу или папке\n" +
            "  [green]--rate <герцы>[/]        Целевая частота SPU (44100, 37800, 32000, 22050, 18900, 11025)\n" +
            "  [green]--tape[/]                Принудительно включить эмуляцию кассеты\n" +
            "  [green]--no-tape[/]             Отключить эмуляцию кассеты\n" +
            "  [green]--no-gauss[/]            Отключить гауссову интерполяцию ЦАП\n" +
            "  [green]--no-adpcm[/]            Отключить 4-битное ADPCM сжатие\n" +
            "  [green]--drive <число>[/]       Перегруз / сатурация кассеты (напр. 1.5)\n" +
            "  [green]--hiss <число>[/]        Уровень шума ленты (0.0 .. 1.0)\n" +
            "  [green]--pattern <маска>[/]     Маска файлов для batch (напр. [yellow]*.mp3[/], по умолч. [yellow]*.*[/])\n\n" +
            "[bold yellow]Примеры:[/]\n" +
            "  oldsound process track.wav -o track_psx.wav --preset psx-xa-37k\n" +
            "  oldsound process ambient.mp3 -o ambient_sfx.mp3 --preset psx-sfx-22k\n" +
            "  oldsound process melody.flac -o melody_tape.ogg --preset cassette-ferric\n" +
            "  oldsound batch ./music -o ./music_retro --preset psx-tape-hybrid"
        ))
        {
            Header = new PanelHeader("[bold white]Справка по командам OldSound[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);
    }

    private static void PrintPresetsTable()
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold cyan]Пресет[/]");
        table.AddColumn("[bold yellow]Частота SPU[/]");
        table.AddColumn("[bold green]ADPCM[/]");
        table.AddColumn("[bold green]Гаусс ЦАП[/]");
        table.AddColumn("[bold magenta]Кассета[/]");
        table.AddColumn("[bold white]Описание звучания[/]");

        foreach (var p in PresetRegistry.GetAll())
        {
            table.AddRow(
                $"[cyan]{p.Name}[/]",
                $"{p.SpuVoiceRate} Гц",
                p.EnableAdpcm ? "[green]Да (4-bit)[/]" : "[grey]Нет[/]",
                p.EnableGaussian ? "[green]Да[/]" : "[grey]Нет[/]",
                p.EnableTape ? "[magenta]Да[/]" : "[grey]Нет[/]",
                p.Description
            );
        }

        AnsiConsole.Write(table);
    }

    private static int RunProcess(string[] args)
    {
        string? inputPath = null;
        string? outputPath = null;
        string presetName = "psx-xa-37k";

        int? overrideRate = null;
        bool? overrideTape = null;
        bool? overrideGauss = null;
        bool? overrideAdpcm = null;
        float? overrideDrive = null;
        float? overrideHiss = null;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if ((a == "-o" || a == "--output") && i + 1 < args.Length)
                outputPath = args[++i];
            else if ((a == "-p" || a == "--preset") && i + 1 < args.Length)
                presetName = args[++i];
            else if (a == "--rate" && i + 1 < args.Length)
                overrideRate = int.Parse(args[++i]);
            else if (a == "--drive" && i + 1 < args.Length)
                overrideDrive = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
            else if (a == "--hiss" && i + 1 < args.Length)
                overrideHiss = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
            else if (a == "--tape")
                overrideTape = true;
            else if (a == "--no-tape")
                overrideTape = false;
            else if (a == "--no-gauss")
                overrideGauss = false;
            else if (a == "--no-adpcm")
                overrideAdpcm = false;
            else if (!a.StartsWith("-") && inputPath == null)
                inputPath = a;
            else if (!a.StartsWith("-") && outputPath == null)
                outputPath = a;
        }

        if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
        {
            AnsiConsole.MarkupLine("[bold red]Ошибка:[/] Входной файл не указан или не существует.");
            return 1;
        }

        if (string.IsNullOrEmpty(outputPath))
        {
            string dir = Path.GetDirectoryName(inputPath) ?? "";
            string nameNoExt = Path.GetFileNameWithoutExtension(inputPath);
            string ext = Path.GetExtension(inputPath);
            outputPath = Path.Combine(dir, $"{nameNoExt}_{presetName}{ext}");
        }

        var preset = PresetRegistry.Get(presetName);
        // Применяем кастомные переопределения
        if (overrideRate.HasValue) preset.SpuVoiceRate = overrideRate.Value;
        if (overrideTape.HasValue) preset.EnableTape = overrideTape.Value;
        if (overrideGauss.HasValue) preset.EnableGaussian = overrideGauss.Value;
        if (overrideAdpcm.HasValue) preset.EnableAdpcm = overrideAdpcm.Value;
        if (overrideDrive.HasValue) preset.TapeSettings.Drive = overrideDrive.Value;
        if (overrideHiss.HasValue) preset.TapeSettings.HissLevel = overrideHiss.Value;

        var sw = Stopwatch.StartNew();

        AnsiConsole.MarkupLine($"[cyan]Входной файл:[/]  [white]{Path.GetFullPath(inputPath)}[/]");
        AnsiConsole.MarkupLine($"[cyan]Выходной файл:[/] [white]{Path.GetFullPath(outputPath)}[/]");
        AnsiConsole.MarkupLine($"[cyan]Профиль:[/]       [bold yellow]{preset.Name}[/] ({preset.SpuVoiceRate} Гц, ADPCM: {preset.EnableAdpcm}, Gauss: {preset.EnableGaussian}, Tape: {preset.EnableTape})");

        AudioBuffer inBuffer = null!;
        AudioBuffer outBuffer = null!;

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Обработка аудио...", ctx =>
            {
                ctx.Status("Загрузка и декодирование аудиофайла...");
                inBuffer = AudioBridge.Load(inputPath);

                ctx.Status($"Применение SPU тракта ({preset.SpuVoiceRate} Гц ADPCM + Gaussian DAC)...");
                outBuffer = RetroAudioPipeline.Process(inBuffer, preset);

                ctx.Status("Сохранение и кодирование выходного файла...");
                AudioBridge.Save(outBuffer, outputPath);
            });

        sw.Stop();
        AnsiConsole.MarkupLine($"\n[bold green]✓ Готово![/] Обработано {inBuffer.LengthSamples} сэмплов ({inBuffer.Channels} канала) за [bold yellow]{sw.Elapsed.TotalSeconds:F2} сек[/].");
        AnsiConsole.MarkupLine($"Файл сохранен: [bold underline white]{Path.GetFullPath(outputPath)}[/]\n");

        return 0;
    }

    private static int RunBatch(string[] args)
    {
        string? inputDir = null;
        string? outputDir = null;
        string presetName = "psx-xa-37k";
        string pattern = "*.*";

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if ((a == "-o" || a == "--output") && i + 1 < args.Length)
                outputDir = args[++i];
            else if ((a == "-p" || a == "--preset") && i + 1 < args.Length)
                presetName = args[++i];
            else if (a == "--pattern" && i + 1 < args.Length)
                pattern = args[++i];
            else if (!a.StartsWith("-") && inputDir == null)
                inputDir = a;
        }

        if (string.IsNullOrEmpty(inputDir) || !Directory.Exists(inputDir))
        {
            AnsiConsole.MarkupLine("[bold red]Ошибка:[/] Входная директория не указана или не существует.");
            return 1;
        }

        if (string.IsNullOrEmpty(outputDir))
        {
            outputDir = Path.Combine(inputDir, $"retro_{presetName}");
        }

        Directory.CreateDirectory(outputDir);

        string[] supportedExts = { ".wav", ".mp3", ".ogg", ".flac", ".m4a", ".aac" };
        var files = Directory.GetFiles(inputDir, pattern)
            .Where(f => supportedExts.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToArray();

        if (files.Length == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Не найдено поддерживаемых аудиофайлов по маске '{pattern}' в '{inputDir}'.[/]");
            return 0;
        }

        var preset = PresetRegistry.Get(presetName);

        AnsiConsole.MarkupLine($"[bold cyan]Пакетная обработка:[/] {files.Length} файлов");
        AnsiConsole.MarkupLine($"[cyan]Входная папка:[/]   {Path.GetFullPath(inputDir)}");
        AnsiConsole.MarkupLine($"[cyan]Выходная папка:[/]  {Path.GetFullPath(outputDir)}");
        AnsiConsole.MarkupLine($"[cyan]Профиль:[/]         [bold yellow]{preset.Name}[/]\n");

        var sw = Stopwatch.StartNew();

        AnsiConsole.Progress()
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(),
            })
            .Start(ctx =>
            {
                var task = ctx.AddTask("[green]Обработка файлов[/]", maxValue: files.Length);

                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    task.Description = $"[green]{fileName}[/]";

                    string outFilePath = Path.Combine(outputDir, fileName);
                    var inBuf = AudioBridge.Load(file);
                    var outBuf = RetroAudioPipeline.Process(inBuf, preset);
                    AudioBridge.Save(outBuf, outFilePath);

                    task.Increment(1);
                }
            });

        sw.Stop();
        AnsiConsole.MarkupLine($"\n[bold green]✓ Пакетная обработка успешно завершена![/] Обработано {files.Length} файлов за [bold yellow]{sw.Elapsed.TotalSeconds:F2} сек[/].\n");
        return 0;
    }
}
