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
                AnsiConsole.MarkupLine($"[red]Unknown command or file:[/red] '{args[0]}'");
                PrintHelp();
                return 1;
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"\n[bold red]Error:[/bold red] {ex.Message}");
            return 1;
        }
    }

    private static void PrintBanner()
    {
        AnsiConsole.Write(
            new FigletText("OldSound")
                .Color(Color.DeepSkyBlue1));
        AnsiConsole.MarkupLine("[bold grey]Authentic DSP emulator of 90s console audio paths (PS1, 3DO) and cassette tape[/]");
        AnsiConsole.MarkupLine("[grey]Sony ADPCM • 3DO SDX2 Delta • 4-Point Gaussian DAC • half_rate.dsp • SPU Filters • Tape[/]\n");
    }

    private static void PrintHelp()
    {
        var panel = new Panel(new Markup(
            "[bold yellow]Usage:[/]\n" +
            "  [cyan]oldsound process[/] <input-file> -o <output-file> [options]\n" +
            "  [cyan]oldsound batch[/] <input-dir> -o <output-dir> [options]\n" +
            "  [cyan]oldsound[/] <input-file> [output-file] [--preset <name>]\n" +
            "  [cyan]oldsound presets[/] (list available audio profiles)\n\n" +
            "[bold yellow]Options:[/]\n" +
            "  [green]--preset <name>[/]       Audio profile (e.g. [yellow]four-sight-1995[/], [yellow]ps1-spu-1994[/])\n" +
            "  [green]-o, --output <path>[/]   Output file or directory path\n" +
            "  [green]-f, --format <ext>[/]    Output format: [yellow]mp3[/] (320k), [yellow]flac[/], [yellow]wav[/]\n" +
            "  [green]-b, --bitrate <kbps>[/]  MP3 bitrate in kbps (default [yellow]320[/])\n" +
            "  [green]--codec <type>[/]         Codec: [yellow]adpcm[/], [yellow]sdx2[/], [yellow]bypass[/]\n" +
            "  [green]--interp <type>[/]        DAC interpolation: [yellow]gauss[/], [yellow]linear3do[/], [yellow]linear[/]\n" +
            "  [green]--rate <hz>[/]            Console voice sample rate (44100, 37800, 22050, 18900, 11025)\n" +
            "  [green]--cutoff <hz>[/]          Analog filter cutoff frequency (e.g. [yellow]10200[/] or [yellow]20500[/])\n" +
            "  [green]--no-filter[/]           Disable DAC analog output filter\n" +
            "  [green]--glue <val>[/]           Bus glue / analog saturation (0.0 .. 2.0)\n" +
            "  [green]--spu-noise <val>[/]      DAC noise floor level (0.0 .. 2.0)\n" +
            "  [green]--no-spu-noise[/]        Disable DAC noise floor\n" +
            "  [green]--tape[/]                Enable cassette tape simulation\n" +
            "  [green]--no-tape[/]             Disable cassette tape simulation\n" +
            "  [green]--drive <val>[/]          Cassette magnetic drive (e.g. [yellow]1.4[/])\n" +
            "  [green]--hiss <val>[/]           Tape hiss level (0.0 .. 1.0)\n" +
            "  [green]--pattern <mask>[/]      File search pattern for batch (e.g. [yellow]*.mp3[/], default [yellow]*.*[/])\n\n" +
            "[bold yellow]Examples:[/]\n" +
            "  oldsound process track.wav -o track_3do.mp3 --preset four-sight-1995\n" +
            "  oldsound process track.wav -f flac --preset four-sight-1995\n" +
            "  oldsound process music.flac -o music_ps1.mp3 --preset ps1-spu-1994\n" +
            "  oldsound process ambient.mp3 -o ambient_tape.mp3 --preset cassette-type1\n" +
            "  oldsound batch ./music -o ./music_retro -f mp3 --preset four-sight-1995"
        ))
        {
            Header = new PanelHeader("[bold white]OldSound Command Help[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);
    }

    private static void PrintPresetsTable()
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold cyan]Preset[/]");
        table.AddColumn("[bold yellow]Codec[/]");
        table.AddColumn("[bold green]Rate / DAC[/]");
        table.AddColumn("[bold magenta]Output Filter[/]");
        table.AddColumn("[bold white]Historical Sound Description[/]");

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
                ? $"{p.FilterTopology} ({p.FilterCutoffHz:F0} Hz)"
                : "[grey]Off[/]";

            table.AddRow(
                $"[cyan]{p.Name}[/]",
                codecStr,
                $"{p.SpuVoiceRate} Hz ({interpStr})",
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
        string? overrideFormat = null;
        int mp3Bitrate = 320;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if ((a == "-o" || a == "--output") && i + 1 < args.Length)
                outputPath = args[++i];
            else if ((a == "-p" || a == "--preset") && i + 1 < args.Length)
                presetName = args[++i];
            else if ((a == "-f" || a == "--format") && i + 1 < args.Length)
                overrideFormat = args[++i].ToLowerInvariant().TrimStart('.');
            else if ((a == "-b" || a == "--bitrate") && i + 1 < args.Length)
                mp3Bitrate = int.Parse(args[++i]);
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
            AnsiConsole.MarkupLine("[red]Input file not specified.[/]");
            PrintHelp();
            return 1;
        }

        if (!File.Exists(inputPath))
        {
            AnsiConsole.MarkupLine($"[red]File not found:[/] {inputPath}");
            return 1;
        }

        if (string.IsNullOrEmpty(outputPath))
        {
            string dir = Path.GetDirectoryName(inputPath) ?? "";
            string nameNoExt = Path.GetFileNameWithoutExtension(inputPath);
            string ext = overrideFormat != null ? $".{overrideFormat}" : Path.GetExtension(inputPath);
            if (string.IsNullOrEmpty(ext)) ext = ".mp3";
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

        AnsiConsole.MarkupLine($"[cyan]Input file:[/]  [white]{Path.GetFullPath(inputPath)}[/]");
        AnsiConsole.MarkupLine($"[cyan]Output file:[/] [white]{Path.GetFullPath(outputPath)}[/]");
        AnsiConsole.MarkupLine($"[cyan]Preset:[/]      [bold yellow]{preset.Name}[/] ({preset.Codec}, {preset.SpuVoiceRate} Hz, Filter: {(preset.EnableAnalogFilter ? $"{preset.FilterCutoffHz:F0} Hz" : "Off")}, Tape: {preset.EnableTape})");

        AudioBuffer inBuffer = null!;
        AudioBuffer outBuffer = null!;

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Processing audio...", ctx =>
            {
                ctx.Status("Loading and decoding audio file...");
                inBuffer = AudioBridge.Load(inputPath);

                ctx.Status($"Applying authentic retro signal path ({preset.Codec} {preset.SpuVoiceRate} Hz, DAC {preset.Interpolation}, LPF {preset.FilterCutoffHz:F0} Hz)...");
                outBuffer = RetroAudioPipeline.Process(inBuffer, preset);

                ctx.Status($"Encoding and saving output file ({Path.GetExtension(outputPath).ToUpperInvariant()})...");
                AudioBridge.Save(outBuffer, outputPath, mp3BitrateKbps: mp3Bitrate);
            });

        sw.Stop();
        long outSizeBytes = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
        string sizeStr = outSizeBytes > 1024 * 1024
            ? $"{(outSizeBytes / (1024.0 * 1024.0)):F1} MB"
            : $"{(outSizeBytes / 1024.0):F0} KB";

        AnsiConsole.MarkupLine($"\n[bold green]✓ Done![/] Processed {inBuffer.LengthSamples} samples ({inBuffer.Channels} channels) in [bold yellow]{sw.Elapsed.TotalSeconds:F2}s[/].");
        AnsiConsole.MarkupLine($"File saved: [bold underline white]{Path.GetFullPath(outputPath)}[/] ({sizeStr})\n");

        return 0;
    }

    private static int RunBatch(string[] args)
    {
        string? inputDir = null;
        string? outputDir = null;
        string presetName = PresetRegistry.DefaultPresetName;
        string pattern = "*.*";
        string? overrideFormat = null;
        int mp3Bitrate = 320;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if ((a == "-o" || a == "--output") && i + 1 < args.Length)
                outputDir = args[++i];
            else if ((a == "-p" || a == "--preset") && i + 1 < args.Length)
                presetName = args[++i];
            else if ((a == "-f" || a == "--format") && i + 1 < args.Length)
                overrideFormat = args[++i].ToLowerInvariant().TrimStart('.');
            else if ((a == "-b" || a == "--bitrate") && i + 1 < args.Length)
                mp3Bitrate = int.Parse(args[++i]);
            else if (a == "--pattern" && i + 1 < args.Length)
                pattern = args[++i];
            else if (!a.StartsWith("-") && inputDir == null)
                inputDir = a;
        }

        if (string.IsNullOrEmpty(inputDir) || !Directory.Exists(inputDir))
        {
            AnsiConsole.MarkupLine("[red]Input directory not specified or does not exist.[/]");
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
            AnsiConsole.MarkupLine($"[yellow]No supported audio files matching '{pattern}' found in '{inputDir}'.[/]");
            return 0;
        }

        var preset = PresetRegistry.Get(presetName);

        AnsiConsole.MarkupLine($"[bold cyan]Batch processing:[/] {files.Length} files");
        AnsiConsole.MarkupLine($"[cyan]Input directory:[/]   {Path.GetFullPath(inputDir)}");
        AnsiConsole.MarkupLine($"[cyan]Output directory:[/]  {Path.GetFullPath(outputDir)}");
        AnsiConsole.MarkupLine($"[cyan]Preset:[/]            [bold yellow]{preset.Name}[/]\n");

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
                var task = ctx.AddTask("[green]Processing files[/]", maxValue: files.Length);

                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    task.Description = $"[green]{fileName}[/]";

                    string outFileName = overrideFormat != null
                        ? Path.ChangeExtension(fileName, "." + overrideFormat)
                        : fileName;

                    string outFilePath = Path.Combine(outputDir, outFileName);
                    var inBuf = AudioBridge.Load(file);
                    var outBuf = RetroAudioPipeline.Process(inBuf, preset);
                    AudioBridge.Save(outBuf, outFilePath, mp3BitrateKbps: mp3Bitrate);

                    task.Increment(1);
                }
            });

        sw.Stop();
        AnsiConsole.MarkupLine($"\n[bold green]✓ Batch processing completed successfully![/] Processed {files.Length} files in [bold yellow]{sw.Elapsed.TotalSeconds:F2}s[/].\n");
        return 0;
    }
}
