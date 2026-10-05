using System;
using System.Collections.Generic;
using OldSound.Core.Dsp;

namespace OldSound.Core.Pipeline;

/// <summary>
/// Реестр исторических и художественных пресетов ретро-звука.
/// </summary>
public static class PresetRegistry
{
    private static readonly Dictionary<string, AudioPreset> _presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["psx-xa-37k"] = new AudioPreset
        {
            Name = "psx-xa-37k",
            Description = "Исторический стандарт потокового аудио CD-XA в играх PS1 (37.8 кГц ADPCM + Gaussian DAC). Теплый кинематографичный саундтрек.",
            SpuVoiceRate = 37800,
            EnableAdpcm = true,
            EnableGaussian = true,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["psx-sfx-22k"] = new AudioPreset
        {
            Name = "psx-sfx-22k",
            Description = "Винтажный стандарт сэмплов, голосов и эмбиента (22.05 кГц ADPCM + Gaussian DAC). Тот самый мягкий «подводный» звук из Silent Hill и King's Field.",
            SpuVoiceRate = 22050,
            EnableAdpcm = true,
            EnableGaussian = true,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["psx-spu-native"] = new AudioPreset
        {
            Name = "psx-spu-native",
            Description = "Нативный тракт PS1 SPU на 44.1 кГц без даунсэмплинга (чистый 4-bit ADPCM + 1:1 Gaussian Low-Pass ЦАП).",
            SpuVoiceRate = 44100,
            EnableAdpcm = true,
            EnableGaussian = true,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["psx-lofi-11k"] = new AudioPreset
        {
            Name = "psx-lofi-11k",
            Description = "Низкобюджетные сэмплы из сильно сжатых игр (11.025 кГц ADPCM + Gaussian). Характерный хрустящий олдскульный сэмплер.",
            SpuVoiceRate = 11025,
            EnableAdpcm = true,
            EnableGaussian = true,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["psx-raw-adpcm"] = new AudioPreset
        {
            Name = "psx-raw-adpcm",
            Description = "Сырое квантование 4-bit Sony ADPCM без аппаратного сглаживания ЦАП Гаусса (демонстрация резких цифровых артефактов).",
            SpuVoiceRate = 44100,
            EnableAdpcm = true,
            EnableGaussian = false,
            EnableTape = false,
            OutputSampleRate = 44100
        },
        ["cassette-ferric"] = new AudioPreset
        {
            Name = "cassette-ferric",
            Description = "Аналоговая компакт-кассета Type I (Ferric): теплая магнитная сатурация, подъем баса 65 Гц, мягкий Wow/Flutter и шум ленты.",
            SpuVoiceRate = 44100,
            EnableAdpcm = false,
            EnableGaussian = false,
            EnableTape = true,
            TapeSettings = new CassetteTapeSettings
            {
                Drive = 1.35f,
                WowDepth = 0.25f,
                FlutterDepth = 0.18f,
                HissLevel = 0.07f,
                EnableHeadEq = true,
                Mix = 1.0f
            },
            OutputSampleRate = 44100
        },
        ["cassette-lofi"] = new AudioPreset
        {
            Name = "cassette-lofi",
            Description = "Изношенная компакт-кассета: глубокая сатурация, заметное плавание питча Wow/Flutter и выраженный шум ленты.",
            SpuVoiceRate = 44100,
            EnableAdpcm = false,
            EnableGaussian = false,
            EnableTape = true,
            TapeSettings = new CassetteTapeSettings
            {
                Drive = 1.8f,
                WowDepth = 0.65f,
                FlutterDepth = 0.45f,
                HissLevel = 0.14f,
                EnableHeadEq = true,
                Mix = 1.0f
            },
            OutputSampleRate = 44100
        },
        ["psx-tape-hybrid"] = new AudioPreset
        {
            Name = "psx-tape-hybrid",
            Description = "Гибридный винтаж: звук PS1 SPU (37.8 кГц ADPCM + Gaussian DAC), оцифрованный на компакт-кассету Type I.",
            SpuVoiceRate = 37800,
            EnableAdpcm = true,
            EnableGaussian = true,
            EnableTape = true,
            TapeSettings = new CassetteTapeSettings
            {
                Drive = 1.25f,
                WowDepth = 0.20f,
                FlutterDepth = 0.15f,
                HissLevel = 0.05f,
                EnableHeadEq = true,
                Mix = 1.0f
            },
            OutputSampleRate = 44100
        }
    };

    public static AudioPreset Get(string name)
    {
        if (_presets.TryGetValue(name, out var preset))
            return preset;

        throw new KeyNotFoundException($"Пресет '{name}' не найден. Доступные пресеты: {string.Join(", ", _presets.Keys)}");
    }

    public static bool TryGet(string name, out AudioPreset? preset)
    {
        return _presets.TryGetValue(name, out preset);
    }

    public static IReadOnlyCollection<AudioPreset> GetAll() => _presets.Values;
}
