using OldSound.Core.Dsp;

namespace OldSound.Core.Pipeline;

/// <summary>
/// Описание профиля звучания и параметров звукового тракта.
/// </summary>
public sealed class AudioPreset
{
    public string Name { get; set; } = "default";
    public string Description { get; set; } = "";

    /// <summary>Целевая частота дискретизации голоса SPU (44100, 37800, 32000, 22050, 18900, 11025).</summary>
    public int SpuVoiceRate { get; set; } = 37800;

    /// <summary>Включить 4-битную Sony ADPCM (VAG) компрессию.</summary>
    public bool EnableAdpcm { get; set; } = true;

    /// <summary>Включить 4-точечную гауссову интерполяцию ЦАП SPU.</summary>
    public bool EnableGaussian { get; set; } = true;

    /// <summary>Включить аппаратный 3-полюсный аналоговый фильтр выхода ЦАП SPU (-18 дБ/окт).</summary>
    public bool EnableAnalogFilter { get; set; } = true;

    /// <summary>Частота среза аналогового фильтра SPU (в Гц, по умолчанию 10500..12500 Гц).</summary>
    public float FilterCutoffHz { get; set; } = 11000f;

    /// <summary>Уровень зернистости квантования ADPCM (0.0 = чистый MSE, 0.5..1.0 = аутентичный консольный хруст).</summary>
    public float AdpcmGrit { get; set; } = 0.5f;

    /// <summary>Использовать исторический целочисленный энкодер Sony SDK (encvag/MFAudio).</summary>
    public bool AuthenticAdpcmMode { get; set; } = true;

    /// <summary>Уровень аналогового шума ЦАП SPU (0.0 = тишина, 1.0 = аутентичный консольный фон ~ -52 dB).</summary>
    public float SpuNoiseLevel { get; set; } = 1.0f;

    /// <summary>Мягкая аналоговая компрессия и насыщение шины SPU (0.0 = выкл, 1.0 = норма).</summary>
    public float BusGlue { get; set; } = 1.0f;

    /// <summary>Включить модуль магнитной компакт-кассеты.</summary>
    public bool EnableTape { get; set; } = false;

    /// <summary>Настройки кассетного тракта.</summary>
    public CassetteTapeSettings TapeSettings { get; set; } = new();

    /// <summary>Выходная частота дискретизации (по умолчанию 44100 Гц — нативный ЦАП PS1).</summary>
    public int OutputSampleRate { get; set; } = 44100;
}
