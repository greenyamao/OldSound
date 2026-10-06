using System;
using System.Collections.Generic;
using OldSound.Core.Dsp;

namespace OldSound.Core.Pipeline;

/// <summary>
/// Audio pipeline presets registry for Four-Sight (1995 3DO / 1996 PS1) and analog cassette tape.
/// Focuses on authentic digital compression without synthetic distortion or clipping.
/// </summary>
public static class PresetRegistry
{
    public const string DefaultPresetName = "four-sight-1995";

    private static readonly Dictionary<string, AudioPreset> _presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["four-sight-1995"] = new AudioPreset
        {
            Name = "four-sight-1995",
            Description = "Four-Sight (1995) — Authentic 3DO Opera signal path: 22.05 kHz 8-bit SDX2 (Square-Root Delta), studio FIR anti-aliasing at 10.5 kHz, half_rate.dsp microcode with crystalline 11-22 kHz mirror harmonics, Sallen-Key 20.5 kHz DAC filter, clean digital path.",
            Codec = AudioCodecType.Sdx2_3Do,
            SpuVoiceRate = 22050,
            Interpolation = InterpolationType.Linear3DoHalfRate,
            PreFilterCutoffHz = 10500f,
            EnableAnalogFilter = true,
            FilterTopology = FilterTopology.TwoPoleSallenKey,
            FilterCutoffHz = 20500f,
            SpuNoiseLevel = 0.0f,
            NoiseProfile = AnalogNoiseProfile.Console,
            BusGlue = 0.0f,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["ps1-spu-1994"] = new AudioPreset
        {
            Name = "ps1-spu-1994",
            Description = "PlayStation (1994 SPU VAG) — 22.05 kHz 4-bit ADPCM, studio FIR anti-aliasing at 10 kHz, 4-point Gaussian DAC interpolation, 3-pole analog reconstruction filter at 12 kHz.",
            Codec = AudioCodecType.SonyAdpcm,
            SpuVoiceRate = 22050,
            Interpolation = InterpolationType.Gaussian4Point,
            PreFilterCutoffHz = 10000f,
            EnableAnalogFilter = true,
            FilterTopology = FilterTopology.ThreePoleSpu,
            FilterCutoffHz = 12000f,
            AuthenticAdpcmMode = true,
            SpuNoiseLevel = 0.0f,
            NoiseProfile = AnalogNoiseProfile.Console,
            BusGlue = 0.0f,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["cassette-type1"] = new AudioPreset
        {
            Name = "cassette-type1",
            Description = "Compact Cassette (Type I Tape) — Analog magnetic tape (NAB 120µs EQ, tape saturation, wow & flutter, analog hiss).",
            Codec = AudioCodecType.Bypass,
            SpuVoiceRate = 44100,
            Interpolation = InterpolationType.Bypass,
            PreFilterCutoffHz = 0f,
            EnableAnalogFilter = false,
            FilterCutoffHz = 22000f,
            SpuNoiseLevel = 0.04f,
            NoiseProfile = AnalogNoiseProfile.Cassette,
            BusGlue = 0.0f,
            EnableTape = true,
            TapeSettings = new CassetteTapeSettings
            {
                Drive = 0.35f,
                WowDepth = 0.25f,
                FlutterDepth = 0.20f,
                HissLevel = 0.04f,
                CutoffHz = 14200f,
                EnableHeadEq = true,
                Mix = 1.0f
            },
            OutputSampleRate = 44100
        }
    };

    // Aliases for backwards compatibility with tests and CLI
    private static readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["four-sight-3do"] = "four-sight-1995",
        ["four-sight-3do-aliased"] = "four-sight-1995",
        ["four-sight-psx-4bit"] = "four-sight-1995",
        ["four-sight-psx-spu"] = "ps1-spu-1994",
        ["four-sight-crunch-11k"] = "ps1-spu-1994",
        ["four-sight-psx-xa"] = "ps1-spu-1994",
        ["cassette-tape"] = "cassette-type1"
    };

    public static AudioPreset Get(string name)
    {
        if (_presets.TryGetValue(name, out var preset))
        {
            return Clone(preset);
        }

        if (_aliases.TryGetValue(name, out var canonical) && _presets.TryGetValue(canonical, out var canonicalPreset))
        {
            return Clone(canonicalPreset);
        }

        throw new ArgumentException($"Preset '{name}' not found. Available presets: {string.Join(", ", _presets.Keys)}");
    }

    public static IEnumerable<AudioPreset> GetAll() => _presets.Values;

    public static bool Exists(string name) => _presets.ContainsKey(name) || _aliases.ContainsKey(name);

    private static AudioPreset Clone(AudioPreset p)
    {
        return new AudioPreset
        {
            Name = p.Name,
            Description = p.Description,
            Codec = p.Codec,
            SpuVoiceRate = p.SpuVoiceRate,
            Interpolation = p.Interpolation,
            PreFilterCutoffHz = p.PreFilterCutoffHz,
            EnableAnalogFilter = p.EnableAnalogFilter,
            FilterTopology = p.FilterTopology,
            FilterCutoffHz = p.FilterCutoffHz,
            AuthenticAdpcmMode = p.AuthenticAdpcmMode,
            SpuNoiseLevel = p.SpuNoiseLevel,
            NoiseProfile = p.NoiseProfile,
            BusGlue = p.BusGlue,
            EnableTape = p.EnableTape,
            TapeSettings = new CassetteTapeSettings
            {
                Drive = p.TapeSettings.Drive,
                WowDepth = p.TapeSettings.WowDepth,
                FlutterDepth = p.TapeSettings.FlutterDepth,
                HissLevel = p.TapeSettings.HissLevel,
                CutoffHz = p.TapeSettings.CutoffHz,
                EnableHeadEq = p.TapeSettings.EnableHeadEq,
                Mix = p.TapeSettings.Mix
            },
            OutputSampleRate = p.OutputSampleRate
        };
    }
}
