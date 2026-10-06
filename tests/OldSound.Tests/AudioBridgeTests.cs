using System.IO;
using OldSound.Core.Audio;
using OldSound.Core.Pipeline;
using Xunit;

namespace OldSound.Tests;

public class AudioBridgeTests
{
    [Fact]
    public void Load_UserMp3_LoadsSuccessfully()
    {
        string mp3Path = Path.Combine("..", "..", "..", "..", "o.mp3");
        if (!File.Exists(mp3Path))
        {
            // Also check alternate relative path
            mp3Path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "o.mp3"));
        }

        if (File.Exists(mp3Path))
        {
            var buffer = AudioBridge.Load(mp3Path);
            Assert.True(buffer.Channels >= 1);
            Assert.True(buffer.SampleRate > 0);
            Assert.True(buffer.LengthSamples > 0);

            // Test run across four-sight-1995 preset
            int testSamples = System.Math.Min(buffer.SampleRate * 2, buffer.LengthSamples);
            var slice = new AudioBuffer(buffer.Channels, buffer.SampleRate, testSamples);
            for (int c = 0; c < buffer.Channels; c++)
            {
                buffer.GetChannelSpan(c).Slice(0, testSamples).CopyTo(slice.GetChannelSpan(c));
            }

            var preset = PresetRegistry.Get("four-sight-1995");
            var processed = RetroAudioPipeline.Process(slice, preset);

            Assert.Equal(buffer.Channels, processed.Channels);
            Assert.Equal(44100, processed.SampleRate);
            Assert.True(processed.LengthSamples > 0);
        }
    }

    [Fact]
    public void FindFFmpegBinary_WhenPresent_ResolvesPath()
    {
        string? ffmpeg = AudioBridge.FindFFmpegBinary();
        Assert.NotNull(ffmpeg);
        Assert.True(ffmpeg.Length > 0);
    }

    [Fact]
    public void EmbeddedFFmpegResource_IsPresentInAssembly()
    {
        var asm = typeof(AudioBridge).Assembly;
        var names = asm.GetManifestResourceNames();
        Assert.Contains(names, n => n.EndsWith("ffmpeg.exe.gz", System.StringComparison.OrdinalIgnoreCase));
    }
}
