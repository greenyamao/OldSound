using System;
using System.IO;
using OldSound.Core.Audio;
using OldSound.Core.Dsp;
using OldSound.Core.Pipeline;
using Xunit;
using Xunit.Abstractions;

namespace OldSound.Tests;

public class DiagnosticTests
{
    private readonly ITestOutputHelper _output;

    public DiagnosticTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DiagnoseLevels_OnRealAudio()
    {
        string mp3Path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "o.mp3"));
        if (!File.Exists(mp3Path))
        {
            _output.WriteLine($"File not found: {mp3Path}");
            return;
        }

        var input = AudioBridge.Load(mp3Path);
        _output.WriteLine($"Loaded o.mp3: {input.Channels}ch, {input.SampleRate}Hz, {input.LengthSamples} samples");

        foreach (var p in PresetRegistry.GetAll())
        {
            var processed = RetroAudioPipeline.Process(input, p);
            
            float maxPeak = 0f;
            float sumSq = 0f;
            int total = processed.LengthSamples * processed.Channels;
            int clippedSamples = 0;

            for (int c = 0; c < processed.Channels; c++)
            {
                var span = processed.GetChannelSpan(c);
                for (int i = 0; i < span.Length; i++)
                {
                    float val = Math.Abs(span[i]);
                    if (val > maxPeak) maxPeak = val;
                    if (val >= 1.0f) clippedSamples++;
                    sumSq += val * val;
                }
            }

            float rms = MathF.Sqrt(sumSq / total);
            float peakDb = 20f * MathF.Log10(Math.Max(1e-6f, maxPeak));
            float rmsDb = 20f * MathF.Log10(Math.Max(1e-6f, rms));

            _output.WriteLine($"[Preset: {p.Name}] MaxPeak: {maxPeak:F3} ({peakDb:F1} dBFS), RMS: {rmsDb:F1} dBFS, Clipped: {clippedSamples} samples ({(float)clippedSamples / total * 100:F2}%)");
        }
    }

    [Fact]
    public void RenderFourSightPresets()
    {
        string mp3Path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "o.mp3"));
        if (!File.Exists(mp3Path)) return;

        string outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "output_samples"));
        Directory.CreateDirectory(outDir);

        var input = AudioBridge.Load(mp3Path);

        foreach (var p in PresetRegistry.GetAll())
        {
            var processed = RetroAudioPipeline.Process(input, p);
            string outPath = Path.Combine(outDir, $"o_{p.Name.Replace('-', '_')}.mp3");
            AudioBridge.Save(processed, outPath);
            _output.WriteLine($"Saved {outPath}");
        }
    }

    [Fact]
    public void CheckSpuNoiseLevel()
    {
        var noise = new SpuNoiseFloor();
        float[] left = new float[44100];
        float[] right = new float[44100];

        noise.ProcessStereo(left, right, 1.0f);

        float maxL = 0f;
        float sumSq = 0f;
        int nonZero16Bit = 0;
        for (int i = 0; i < left.Length; i++)
        {
            float val = MathF.Abs(left[i]);
            if (val > maxL) maxL = val;
            sumSq += val * val;

            short s16 = (short)Math.Clamp(Math.Round(left[i] * 32767f), short.MinValue, short.MaxValue);
            if (s16 != 0) nonZero16Bit++;
        }

        float rms = MathF.Sqrt(sumSq / left.Length);
        float peakDb = 20f * MathF.Log10(Math.Max(1e-9f, maxL));
        float rmsDb = 20f * MathF.Log10(Math.Max(1e-9f, rms));

        _output.WriteLine($"Noise level=1.0: Peak={maxL:E4} ({peakDb:F1} dBFS), RMS={rms:E4} ({rmsDb:F1} dBFS), NonZero16Bit samples: {nonZero16Bit} / 44100");
    }

    [Fact]
    public void CheckBothNoiseProfiles()
    {
        var noise = new SpuNoiseFloor();
        float[] left = new float[44100];
        float[] right = new float[44100];

        noise.ProcessStereo(left, right, 1.0f, AnalogNoiseProfile.Console);
        float sumSqC = 0f;
        for (int i = 0; i < left.Length; i++) sumSqC += left[i] * left[i];
        float rmsC = MathF.Sqrt(sumSqC / left.Length);
        _output.WriteLine($"Console Profile (100%): RMS = {rmsC:E4} ({20f * MathF.Log10(rmsC):F1} dBFS)");

        noise.Reset();
        Array.Clear(left);
        Array.Clear(right);
        noise.ProcessStereo(left, right, 1.0f, AnalogNoiseProfile.Cassette);
        float sumSqT = 0f;
        for (int i = 0; i < left.Length; i++) sumSqT += left[i] * left[i];
        float rmsT = MathF.Sqrt(sumSqT / left.Length);
        _output.WriteLine($"Cassette Profile (100%): RMS = {rmsT:E4} ({20f * MathF.Log10(rmsT):F1} dBFS)");
    }
}
