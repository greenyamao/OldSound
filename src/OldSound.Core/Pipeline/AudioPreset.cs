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

    /// <summary>Включить модуль магнитной компакт-кассеты.</summary>
    public bool EnableTape { get; set; } = false;

    /// <summary>Настройки кассетного тракта.</summary>
    public CassetteTapeSettings TapeSettings { get; set; } = new();

    /// <summary>Выходная частота дискретизации (по умолчанию 44100 Гц — нативный ЦАП PS1).</summary>
    public int OutputSampleRate { get; set; } = 44100;
}
