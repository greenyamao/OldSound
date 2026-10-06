using System;
using System.Collections.Generic;
using OldSound.Core.Dsp;

namespace OldSound.Core.Pipeline;

/// <summary>
/// Реестр пресетов звукового тракта Four-Sight (1995 3DO / 1996 PS1).
/// Фокусируется строго на аутентичной цифровой компрессии без сатураторов и дисторшена.
/// </summary>
public static class PresetRegistry
{
    public const string DefaultPresetName = "four-sight-1995";

    private static readonly Dictionary<string, AudioPreset> _presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["four-sight-1995"] = new AudioPreset
        {
            Name = "four-sight-1995",
            Description = "Four-Sight (1995) — Аутентичный звуковой тракт 3DO Opera: 22.05 кГц 8-битный SDX2 (Square-Root Delta), студийный КИХ-антиалиасинг 10.5 кГц, микрокод half_rate.dsp с кристаллическими зеркальными гармониками 11-22 кГц, ЦАП Sallen-Key 20.5 кГц, чистый цифровой тракт (без биткрашер-грязи).",
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
            Description = "PlayStation (1994 SPU VAG) — 22.05 кГц 4-битный ADPCM, студийный КИХ-антиалиасинг 10 кГц, 4-точечная гауссова интерполяция ЦАП, 3-полюсный аналоговый фильтр 12 кГц.",
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
            Description = "Компакт-кассета (Type I Tape) — Аналоговая магнитная лента (NAB 120µs EQ, сатурация ленты, wow & flutter, шелковистый шум).",
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

    // Алиасы для обратной совместимости с тестами и CLI
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

        throw new ArgumentException($"Пресет с именем '{name}' не найден. Доступные пресеты: {string.Join(", ", _presets.Keys)}");
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
