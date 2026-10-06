using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OldSound.Core.Audio;
using OldSound.Core.Dsp;
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
        AnsiConsole.MarkupLine("[bold grey]Аутентичный DSP-эмулятор звуковых трактов консолей 90-х (PS1, 3DO) и кассеты[/]");
        AnsiConsole.MarkupLine("[grey]Sony ADPCM • 3DO SDX2 Delta • 4-Point Gaussian DAC • half_rate.dsp • SPU Filters • Tape[/]\n");
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
            "  [green]--preset <имя>[/]       Профиль звучания (напр. [yellow]four-sight-1995[/], [yellow]ps1-spu-1994[/])\n" +
            "  [green]-o, --output <путь>[/]   Путь к выходному файлу или папке\n" +
            "  [green]--codec <тип>[/]         Кодек: [yellow]adpcm[/], [yellow]sdx2[/], [yellow]bypass[/]\n" +
            "  [green]--interp <тип>[/]        Интерполяция ЦАП: [yellow]gauss[/], [yellow]linear3do[/], [yellow]linear[/]\n" +
            "  [green]--rate <герцы>[/]        Частота консольного голоса (44100, 37800, 22050, 18900, 11025)\n" +
            "  [green]--cutoff <герцы>[/]      Частота среза аналогового фильтра (напр. [yellow]10200[/] или [yellow]20500[/])\n" +
            "  [green]--no-filter[/]           Отключить аналоговый фильтр выхода ЦАП\n" +
            "  [green]--glue <число>[/]        Насыщение / аналоговый клей шины (0.0 .. 2.0)\n" +
            "  [green]--spu-noise <число>[/]   Уровень фонового шума ЦАП (0.0 .. 2.0)\n" +
            "  [green]--no-spu-noise[/]        Отключить фоновый шум ЦАП\n" +
            "  [green]--tape[/]                Включить эмуляцию кассеты\n" +
            "  [green]--no-tape[/]             Отключить эмуляцию кассеты\n" +
            "  [green]--drive <число>[/]       Магнитное насыщение кассеты (напр. [yellow]1.4[/])\n" +
            "  [green]--hiss <число>[/]        Уровень шума ленты (0.0 .. 1.0)\n" +
            "  [green]--pattern <маска>[/]     Маска файлов для batch (напр. [yellow]*.mp3[/], по умолч. [yellow]*.*[/])\n\n" +
            "[bold yellow]Примеры:[/]\n" +
            "  oldsound process track.wav -o track_3do.wav --preset four-sight-1995\n" +
            "  oldsound process music.flac -o music_ps1.mp3 --preset ps1-spu-1994\n" +
            "  oldsound process ambient.mp3 -o ambient_tape.mp3 --preset cassette-type1\n" +
            "  oldsound batch ./music -o ./music_retro --preset four-sight-1995"
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
        table.AddColumn("[bold yellow]Кодек[/]");
        table.AddColumn("[bold green]Частота / ЦАП[/]");
        table.AddColumn("[bold magenta]Фильтр выхода[/]");
        table.AddColumn("[bold white]Описание исторического звучания[/]");

        foreach (var p in PresetRegistry.GetAll())
        {
            string codecStr = p.Codec switch
            {
                AudioCodecType.SonyAdpcm => "[green]PS1 ADPCM (4-bit)[/]",
                AudioCodecType.Sdx2_3Do => "[cyan]3DO SDX2 (8-bit)[/]",
                _ => "[grey]Linear PCM[/]"
            };

            string interpStr = p.Interpolation switch
            {
                InterpolationType.Gaussian4Point => "Gaussian 4-pt",
                InterpolationType.Linear3DoHalfRate => "3DO Linear (HF)",
                InterpolationType.Linear => "Linear",
                _ => "Bypass"
            };

            string filterStr = p.EnableAnalogFilter
                ? $"{p.FilterTopology} ({p.FilterCutoffHz:F0} Гц)"
                : "[grey]Выкл[/]";

            table.AddRow(
                $"[cyan]{p.Name}[/]",
                codecStr,
                $"{p.SpuVoiceRate} Гц ({interpStr})",
                filterStr,
                p.Description
            );
        }

        AnsiConsole.Write(table);
    }

    private static int RunProcess(string[] args)
    {
        string? inputPath = null;
        string? outputPath = null;
        string presetName = PresetRegistry.DefaultPresetName;

        int? overrideRate = null;
        float? overrideCutoff = null;
        bool? overrideFilter = null;
        float? overrideGlue = null;
        float? overrideSpuNoise = null;
        bool? overrideTape = null;
        AudioCodecType? overrideCodec = null;
        InterpolationType? overrideInterp = null;
        float? overrideDrive = null;
        float? overrideHiss = null;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if ((a == "-o" || a == "--output") && i + 1 < args.Length)
                outputPath = args[++i];
            else if ((a == "-p" || a == "--preset") && i + 1 < args.Length)
                presetName = args[++i];
            else if (a == "--codec" && i + 1 < args.Length)
            {
                string c = args[++i].ToLowerInvariant();
                overrideCodec = c switch
                {
                    "adpcm" or "psx" => AudioCodecType.SonyAdpcm,
                    "sdx2" or "3do" => AudioCodecType.Sdx2_3Do,
                    _ => AudioCodecType.Bypass
                };
            }
            else if (a == "--interp" && i + 1 < args.Length)
            {
                string ip = args[++i].ToLowerInvariant();
                overrideInterp = ip switch
                {
                    "gauss" or "gaussian" => InterpolationType.Gaussian4Point,
                    "linear3do" or "3do" => InterpolationType.Linear3DoHalfRate,
                    "linear" => InterpolationType.Linear,
                    _ => InterpolationType.Bypass
                };
            }
            else if (a == "--rate" && i + 1 < args.Length)
                overrideRate = int.Parse(args[++i]);
            else if (a == "--cutoff" && i + 1 < args.Length)
                overrideCutoff = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
            else if (a == "--no-filter")
                overrideFilter = false;
            else if (a == "--glue" && i + 1 < args.Length)
                overrideGlue = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
            else if (a == "--spu-noise" && i + 1 < args.Length)
                overrideSpuNoise = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
            else if (a == "--no-spu-noise")
                overrideSpuNoise = 0f;
            else if (a == "--drive" && i + 1 < args.Length)
                overrideDrive = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
            else if (a == "--hiss" && i + 1 < args.Length)
                overrideHiss = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
            else if (a == "--tape")
                overrideTape = true;
            else if (a == "--no-tape")
                overrideTape = false;
            else if (!a.StartsWith("-") && inputPath == null)
                inputPath = a;
            else if (!a.StartsWith("-") && outputPath == null)
                outputPath = a;
        }

        if (string.IsNullOrEmpty(inputPath))
        {
            AnsiConsole.MarkupLine("[red]Не указан входной файл.[/]");
            PrintHelp();
            return 1;
        }

        if (!File.Exists(inputPath))
        {
            AnsiConsole.MarkupLine($"[red]Файл не найден:[/] {inputPath}");
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
        if (overrideCodec.HasValue) preset.Codec = overrideCodec.Value;
        if (overrideInterp.HasValue) preset.Interpolation = overrideInterp.Value;
        if (overrideRate.HasValue) preset.SpuVoiceRate = overrideRate.Value;
        if (overrideCutoff.HasValue) { preset.FilterCutoffHz = overrideCutoff.Value; preset.EnableAnalogFilter = true; }
        if (overrideFilter.HasValue) preset.EnableAnalogFilter = overrideFilter.Value;
        if (overrideGlue.HasValue) preset.BusGlue = overrideGlue.Value;
        if (overrideSpuNoise.HasValue) preset.SpuNoiseLevel = overrideSpuNoise.Value;
        if (overrideTape.HasValue) preset.EnableTape = overrideTape.Value;
        if (overrideDrive.HasValue) preset.TapeSettings.Drive = overrideDrive.Value;
        if (overrideHiss.HasValue) preset.TapeSettings.HissLevel = overrideHiss.Value;

        var sw = Stopwatch.StartNew();

        AnsiConsole.MarkupLine($"[cyan]Входной файл:[/]  [white]{Path.GetFullPath(inputPath)}[/]");
        AnsiConsole.MarkupLine($"[cyan]Выходной файл:[/] [white]{Path.GetFullPath(outputPath)}[/]");
        AnsiConsole.MarkupLine($"[cyan]Профиль:[/]       [bold yellow]{preset.Name}[/] ({preset.Codec}, {preset.SpuVoiceRate} Гц, Filter: {(preset.EnableAnalogFilter ? $"{preset.FilterCutoffHz:F0} Гц" : "Выкл")}, Tape: {preset.EnableTape})");

        AudioBuffer inBuffer = null!;
        AudioBuffer outBuffer = null!;

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Обработка аудио...", ctx =>
            {
                ctx.Status("Загрузка и декодирование аудиофайла...");
                inBuffer = AudioBridge.Load(inputPath);

                ctx.Status($"Применение аутентичного ретро-тракта ({preset.Codec} {preset.SpuVoiceRate} Гц, ЦАП {preset.Interpolation}, LPF {preset.FilterCutoffHz:F0} Гц)...");
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
        string presetName = PresetRegistry.DefaultPresetName;
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
            AnsiConsole.MarkupLine("[red]Не указана или не существует входная папка.[/]");
            return 1;
        }

        if (string.IsNullOrEmpty(outputDir))
        {
            outputDir = Path.Combine(inputDir, $"retro_{presetName}");
        }

        Directory.CreateDirectory(outputDir);

        string[] supportedExts = { ".wav", ".mp3", ".flac", ".ogg", ".aiff" };
        var files = Directory.GetFiles(inputDir, pattern, SearchOption.TopDirectoryOnly)
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
