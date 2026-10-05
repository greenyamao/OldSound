using System;
using System.Collections.Generic;
using OldSound.Core.Dsp;

namespace OldSound.Core.Pipeline;

/// <summary>
/// Реестр исторических и художественных пресетов ретро-звука консолей 90-х и кассеты.
/// Обеспечивает точный физический баланс и музыкальный гейн-стейджинг без перегруза.
/// </summary>
public static class PresetRegistry
{
    private static readonly Dictionary<string, AudioPreset> _presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["four-sight-3do"] = new AudioPreset
        {
            Name = "four-sight-3do",
            Description = "3DO Four-Sight (1995) — Аутентичный тракт Юдзи Номи: 22.05 кГц 8-битный кодек SDX2 (Square-Root Delta), апсэмплинг half_rate.dsp с зеркальными гармониками (HF-imaging 11-22 кГц) и ЦАП Саллена-Кея 20.5 кГц. Сохраняет прозрачность фортепиано со специфическим сглаживанием резких атак.",
            Codec = AudioCodecType.Sdx2_3Do,
            SpuVoiceRate = 22050,
            Interpolation = InterpolationType.Linear3DoHalfRate,
            PreFilterCutoffHz = 10000f,
            EnableAnalogFilter = true,
            FilterTopology = FilterTopology.TwoPoleSallenKey,
            FilterCutoffHz = 20500f,
            SpuNoiseLevel = 0.25f,
            BusGlue = 0.35f,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["four-sight-psx-xa"] = new AudioPreset
        {
            Name = "four-sight-psx-xa",
            Description = "PS1 Four-Sight (1996) CD-XA Level B — Потоковое BGM-аудио: 18.9 кГц 4-битный ADPCM, аппаратный срез 8.5 кГц, прямой вывод на ЦАП AK4309 (в обход гауссова фильтра). Исторический саундтрек CD-версии PlayStation.",
            Codec = AudioCodecType.SonyAdpcm,
            SpuVoiceRate = 18900,
            Interpolation = InterpolationType.Linear,
            PreFilterCutoffHz = 8500f,
            EnableAnalogFilter = true,
            FilterTopology = FilterTopology.TwoPoleSallenKey,
            FilterCutoffHz = 8500f,
            AuthenticAdpcmMode = true,
            SpuNoiseLevel = 0.35f,
            BusGlue = 0.4f,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["four-sight-psx-spu"] = new AudioPreset
        {
            Name = "four-sight-psx-spu",
            Description = "PS1 Four-Sight SPU VAG — Воспроизведение через память SPU: 22.05 кГц ADPCM с 4-точечной гауссовой интерполяцией и 3-полюсным фильтром 10.5 кГц. Теплый, бархатный, кинематографичный саундтрек 90-х.",
            Codec = AudioCodecType.SonyAdpcm,
            SpuVoiceRate = 22050,
            Interpolation = InterpolationType.Gaussian4Point,
            PreFilterCutoffHz = 10500f,
            EnableAnalogFilter = true,
            FilterTopology = FilterTopology.ThreePoleSpu,
            FilterCutoffHz = 10500f,
            AuthenticAdpcmMode = true,
            SpuNoiseLevel = 0.45f,
            BusGlue = 0.45f,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["psx-sfx-22k"] = new AudioPreset
        {
            Name = "psx-sfx-22k",
            Description = "Культовый стандарт PS1 (Silent Hill, King's Field): 22.05 кГц ADPCM, гауссов ЦАП, аппаратный 3-полюсный аналоговый фильтр 10.2 кГц, теплый фон ЦАП. Тот самый глубокий «подводный» звук.",
            Codec = AudioCodecType.SonyAdpcm,
            SpuVoiceRate = 22050,
            Interpolation = InterpolationType.Gaussian4Point,
            PreFilterCutoffHz = 10500f,
            EnableAnalogFilter = true,
            FilterTopology = FilterTopology.ThreePoleSpu,
            FilterCutoffHz = 10200f,
            AuthenticAdpcmMode = true,
            SpuNoiseLevel = 0.5f,
            BusGlue = 0.5f,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["psx-xa-37k"] = new AudioPreset
        {
            Name = "psx-xa-37k",
            Description = "Полнополосный стандарт CD-XA Mode 2 Form 2 Level A: 37.8 кГц ADPCM, гауссов ЦАП, аналоговый срез 12.5 кГц. Чистый, плотный винтажный саундтрек 90-х.",
            Codec = AudioCodecType.SonyAdpcm,
            SpuVoiceRate = 37800,
            Interpolation = InterpolationType.Gaussian4Point,
            PreFilterCutoffHz = 16000f,
            EnableAnalogFilter = true,
            FilterTopology = FilterTopology.ThreePoleSpu,
            FilterCutoffHz = 12500f,
            AuthenticAdpcmMode = true,
            SpuNoiseLevel = 0.4f,
            BusGlue = 0.4f,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["psx-silent-hill"] = new AudioPreset
        {
            Name = "psx-silent-hill",
            Description = "Акустический туман Silent Hill: глубокий аналоговый срез 8.5 кГц, бархатный гауссов ЦАП, плотный подводный бас и приглушенные верха без клиппинга.",
            Codec = AudioCodecType.SonyAdpcm,
            SpuVoiceRate = 22050,
            Interpolation = InterpolationType.Gaussian4Point,
            PreFilterCutoffHz = 9000f,
            EnableAnalogFilter = true,
            FilterTopology = FilterTopology.ThreePoleSpu,
            FilterCutoffHz = 8500f,
            AuthenticAdpcmMode = true,
            SpuNoiseLevel = 0.7f,
            BusGlue = 0.6f,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["psx-lofi-11k"] = new AudioPreset
        {
            Name = "psx-lofi-11k",
            Description = "Винтажный 11.025 кГц сэмплер: 4-битный ADPCM, срез выше 5.2 кГц и характерная фактура ранних 32-битных игр.",
            Codec = AudioCodecType.SonyAdpcm,
            SpuVoiceRate = 11025,
            Interpolation = InterpolationType.Gaussian4Point,
            PreFilterCutoffHz = 5200f,
            EnableAnalogFilter = true,
            FilterTopology = FilterTopology.ThreePoleSpu,
            FilterCutoffHz = 5200f,
            AuthenticAdpcmMode = true,
            SpuNoiseLevel = 0.5f,
            BusGlue = 0.5f,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["cassette-ferric"] = new AudioPreset
        {
            Name = "cassette-ferric",
            Description = "Аналоговая компакт-кассета Type I (Ferric): теплое магнитное насыщение, подъем баса 65 Гц, тонкий шум ленты и детонация Wow/Flutter.",
            Codec = AudioCodecType.Bypass,
            SpuVoiceRate = 44100,
            Interpolation = InterpolationType.Bypass,
            EnableAnalogFilter = false,
            SpuNoiseLevel = 0f,
            BusGlue = 0f,
            EnableTape = true,
            TapeSettings = new CassetteTapeSettings
            {
                Drive = 1.4f,
                WowDepth = 0.20f,
                FlutterDepth = 0.15f,
                HissLevel = 0.20f,
                CutoffHz = 12000f,
                EnableHeadEq = true,
                Mix = 1.0f
            },
            OutputSampleRate = 44100
        },
        ["psx-tape-hybrid"] = new AudioPreset
        {
            Name = "psx-tape-hybrid",
            Description = "Гибридный тракт: 22 кГц SPU ADPCM + гауссов ЦАП + фильтр 10.5 кГц + мягкая кассетная сатурация. Глубокий, плотный аналоговый звук.",
            Codec = AudioCodecType.SonyAdpcm,
            SpuVoiceRate = 22050,
            Interpolation = InterpolationType.Gaussian4Point,
            PreFilterCutoffHz = 10500f,
            EnableAnalogFilter = true,
            FilterTopology = FilterTopology.ThreePoleSpu,
            FilterCutoffHz = 10500f,
            AuthenticAdpcmMode = true,
            SpuNoiseLevel = 0.4f,
            BusGlue = 0.4f,
            EnableTape = true,
            TapeSettings = new CassetteTapeSettings
            {
                Drive = 1.25f,
                WowDepth = 0.15f,
                FlutterDepth = 0.12f,
                HissLevel = 0.15f,
                CutoffHz = 11000f,
                EnableHeadEq = true,
                Mix = 1.0f
            },
            OutputSampleRate = 44100
        },
        ["psx-clean"] = new AudioPreset
        {
            Name = "psx-clean",
            Description = "Прозрачный Hi-Fi режим: чистый 44.1 кГц ADPCM без аналогового среза и шума.",
            Codec = AudioCodecType.SonyAdpcm,
            SpuVoiceRate = 44100,
            Interpolation = InterpolationType.Linear,
            EnableAnalogFilter = false,
            SpuNoiseLevel = 0.0f,
            BusGlue = 0.0f,
            EnableTape = false,
            OutputSampleRate = 44100
        }
    };

    public static AudioPreset Get(string name)
    {
        if (_presets.TryGetValue(name, out var preset))
        {
            return Clone(preset);
        }

        throw new ArgumentException($"Пресет с именем '{name}' не найден. Доступные пресеты: {string.Join(", ", _presets.Keys)}");
    }

    public static IEnumerable<AudioPreset> GetAll() => _presets.Values;

    public static bool Exists(string name) => _presets.ContainsKey(name);

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
