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

    [Fact]
    public void Save_Mp3AndFlac_EncodesValidAudioFiles()
    {
        // 1-second stereo tone
        var buffer = new AudioBuffer(2, 44100, 44100);
        for (int i = 0; i < 44100; i++)
        {
            float s = System.MathF.Sin(2.0f * System.MathF.PI * 440.0f * i / 44100.0f) * 0.5f;
            buffer.GetChannelSpan(0)[i] = s;
            buffer.GetChannelSpan(1)[i] = s;
        }

        string tempMp3 = Path.Combine(Path.GetTempPath(), $"test_save_{System.Guid.NewGuid():N}.mp3");
        string tempFlac = Path.Combine(Path.GetTempPath(), $"test_save_{System.Guid.NewGuid():N}.flac");

        try
        {
            AudioBridge.Save(buffer, tempMp3, mp3BitrateKbps: 320);
            Assert.True(File.Exists(tempMp3));
            Assert.True(new FileInfo(tempMp3).Length > 10000);

            var reloaded = AudioBridge.Load(tempMp3);
            Assert.Equal(2, reloaded.Channels);
            Assert.Equal(44100, reloaded.SampleRate);

            AudioBridge.Save(buffer, tempFlac);
            Assert.True(File.Exists(tempFlac));
            Assert.True(new FileInfo(tempFlac).Length > 5000);
        }
        finally
        {
            try { File.Delete(tempMp3); } catch { }
            try { File.Delete(tempFlac); } catch { }
        }
    }
}
