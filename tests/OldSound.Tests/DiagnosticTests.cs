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
}
