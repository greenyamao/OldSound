using System;
using System.Collections.Generic;
using OldSound.Core.Dsp;

namespace OldSound.Core.Pipeline;

/// <summary>
/// Реестр исторических и художественных пресетов ретро-звука.
/// Содержит калиброванные профили для получения бескомпромиссного,
/// глубоко аутентичного «жмыхнутого» консольного и кассетного звука.
/// </summary>
public static class PresetRegistry
{
    private static readonly Dictionary<string, AudioPreset> _presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["psx-sfx-22k"] = new AudioPreset
        {
            Name = "psx-sfx-22k",
            Description = "Культовый стандарт PS1 (Silent Hill, King's Field IV): 22.05 кГц ADPCM, гауссов ЦАП, аппаратный аналоговый срез 10.2 кГц, фон ЦАП и насыщение шины SPU. Тот самый густой «подводный» звук.",
            SpuVoiceRate = 22050,
            EnableAdpcm = true,
            AdpcmGrit = 0.6f,
            AuthenticAdpcmMode = true,
            EnableGaussian = true,
            EnableAnalogFilter = true,
            FilterCutoffHz = 10200f,
            SpuNoiseLevel = 1.0f,
            BusGlue = 1.0f,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["psx-xa-37k"] = new AudioPreset
        {
            Name = "psx-xa-37k",
            Description = "Исторический стандарт потокового аудио CD-XA в играх PS1 (37.8 кГц ADPCM + Gaussian DAC + аналоговый срез 12.5 кГц + фон ЦАП). Плотный винтажный саундтрек 90-х.",
            SpuVoiceRate = 37800,
            EnableAdpcm = true,
            AdpcmGrit = 0.45f,
            AuthenticAdpcmMode = true,
            EnableGaussian = true,
            EnableAnalogFilter = true,
            FilterCutoffHz = 12500f,
            SpuNoiseLevel = 0.8f,
            BusGlue = 0.85f,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["psx-silent-hill"] = new AudioPreset
        {
            Name = "psx-silent-hill",
            Description = "Акустический туман Silent Hill / King's Field: глубокий аналоговый срез 8.8 кГц, плотный консольный шум квантования, подводный бас и сильное сжатие динамики.",
            SpuVoiceRate = 22050,
            EnableAdpcm = true,
            AdpcmGrit = 0.8f,
            AuthenticAdpcmMode = true,
            EnableGaussian = true,
            EnableAnalogFilter = true,
            FilterCutoffHz = 8800f,
            SpuNoiseLevel = 1.35f,
            BusGlue = 1.35f,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["psx-lofi-11k"] = new AudioPreset
        {
            Name = "psx-lofi-11k",
            Description = "Низкобюджетный 11.025 кГц сэмплер: тяжелый хруст 4-битных дельт, срез выше 5.2 кГц и характерная зернистая деградация из ранних 32-битных игр.",
            SpuVoiceRate = 11025,
            EnableAdpcm = true,
            AdpcmGrit = 1.0f,
            AuthenticAdpcmMode = true,
            EnableGaussian = true,
            EnableAnalogFilter = true,
            FilterCutoffHz = 5200f,
            SpuNoiseLevel = 1.2f,
            BusGlue = 1.2f,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["cassette-ferric"] = new AudioPreset
        {
            Name = "cassette-ferric",
            Description = "Аналоговая компакт-кассета Type I (Ferric): сочный магнитный перегруз (Drive 2.8), теплый бас 65 Гц, осязаемый шум ленты (-48 dB) и детонация Wow/Flutter.",
            SpuVoiceRate = 44100,
            EnableAdpcm = false,
            EnableGaussian = false,
            EnableAnalogFilter = false,
            EnableTape = true,
            TapeSettings = new CassetteTapeSettings
            {
                Drive = 2.8f,
                WowDepth = 0.35f,
                FlutterDepth = 0.25f,
                HissLevel = 0.45f,
                CutoffHz = 11500f,
                EnableHeadEq = true,
                Mix = 1.0f
            },
            OutputSampleRate = 44100
        },
        ["cassette-lofi"] = new AudioPreset
        {
            Name = "cassette-lofi",
            Description = "Изношенная зажеванная пленка: тяжелый магнитный овердрайв (Drive 4.0), заметное плавание питча, глухой срез 9.5 кГц и плотный аналоговый шум.",
            SpuVoiceRate = 44100,
            EnableAdpcm = false,
            EnableGaussian = false,
            EnableAnalogFilter = false,
            EnableTape = true,
            TapeSettings = new CassetteTapeSettings
            {
                Drive = 4.0f,
                WowDepth = 0.70f,
                FlutterDepth = 0.50f,
                HissLevel = 0.80f,
                CutoffHz = 9500f,
                EnableHeadEq = true,
                Mix = 1.0f
            },
            OutputSampleRate = 44100
        },
        ["psx-tape-hybrid"] = new AudioPreset
        {
            Name = "psx-tape-hybrid",
            Description = "Ультимативный гибрид: 22 кГц SPU ADPCM + гауссов ЦАП + аналоговый срез 10.5 кГц + магнитная сатурация кассеты. Тот самый бескомпромиссный аналоговый «жмых».",
            SpuVoiceRate = 22050,
            EnableAdpcm = true,
            AdpcmGrit = 0.6f,
            AuthenticAdpcmMode = true,
            EnableGaussian = true,
            EnableAnalogFilter = true,
            FilterCutoffHz = 10500f,
            SpuNoiseLevel = 0.85f,
            BusGlue = 1.0f,
            EnableTape = true,
            TapeSettings = new CassetteTapeSettings
            {
                Drive = 2.4f,
                WowDepth = 0.25f,
                FlutterDepth = 0.20f,
                HissLevel = 0.35f,
                CutoffHz = 11000f,
                EnableHeadEq = true,
                Mix = 1.0f
            },
            OutputSampleRate = 44100
        },
        ["psx-clean"] = new AudioPreset
        {
            Name = "psx-clean",
            Description = "Прозрачный Hi-Fi режим с минимальными искажениями (чистый 44.1 кГц ADPCM с MSE-оптимизацией без аналогового среза и шума).",
            SpuVoiceRate = 44100,
            EnableAdpcm = true,
            AdpcmGrit = 0.0f,
            AuthenticAdpcmMode = false,
            EnableGaussian = true,
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
            // Возвращаем копию для безопасности изменения параметров
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
            SpuVoiceRate = p.SpuVoiceRate,
            EnableAdpcm = p.EnableAdpcm,
            AdpcmGrit = p.AdpcmGrit,
            AuthenticAdpcmMode = p.AuthenticAdpcmMode,
            EnableGaussian = p.EnableGaussian,
            EnableAnalogFilter = p.EnableAnalogFilter,
            FilterCutoffHz = p.FilterCutoffHz,
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
