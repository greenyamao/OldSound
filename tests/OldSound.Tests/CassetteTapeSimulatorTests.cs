using System;
using OldSound.Core.Dsp;
using Xunit;

namespace OldSound.Tests;

public class CassetteTapeSimulatorTests
{
    [Fact]
    public void Process_Saturation_SoftClipsSignal()
    {
        var settings = new CassetteTapeSettings
        {
            Drive = 2.0f,
            WowDepth = 0f,
            FlutterDepth = 0f,
            HissLevel = 0f,
            EnableHeadEq = false
        };

        var tape = new CassetteTapeSimulator(settings);
        float[] left = new float[] { 0.1f, 0.5f, 1.0f, 2.0f, 5.0f };
        float[] right = (float[])left.Clone();

        tape.Process(left, right, 44100);

        // Large signals should softly saturate within ~1.0
        for (int i = 0; i < left.Length; i++)
        {
            Assert.True(MathF.Abs(left[i]) <= 1.25f, $"Signal exceeded saturation ceiling: {left[i]}");
        }
    }

    [Fact]
    public void Process_HissEnabled_AddsNoiseFloor()
    {
        var settings = new CassetteTapeSettings
        {
            Drive = 1.0f,
            WowDepth = 0f,
            FlutterDepth = 0f,
            HissLevel = 0.5f,
            EnableHeadEq = false
        };

        var tape = new CassetteTapeSimulator(settings);
        float[] left = new float[1000];
        float[] right = new float[1000];

        tape.Process(left, right, 44100);

        // Silence should exhibit tape hiss
        float energy = 0;
        foreach (var s in left)
        {
            energy += s * s;
        }

        Assert.True(energy > 0.000001f, "Tape hiss was not detected.");
    }
}
