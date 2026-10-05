using OldSound.Core.Dsp;

namespace OldSound.Core.Pipeline;

/// <summary>
/// Тип алгоритмического кодека сжатия звука.
/// </summary>
public enum AudioCodecType
{
    /// <summary>4-битный PS1 Sony ADPCM (VAG) с 5 адаптивными авторегрессионными фильтрами.</summary>
    SonyAdpcm,

    /// <summary>8-битный 3DO Opera SDX2 (Square-Root Delta) с нелинейным дифференциалом.</summary>
    Sdx2_3Do,

    /// <summary>Линейный 16-битный PCM без потерь (обход кодека).</summary>
    Bypass
}

/// <summary>
/// Метод интерполяции и восстановления сэмплов в ЦАП.
/// </summary>
public enum InterpolationType
{
    /// <summary>Аппаратная 4-точечная гауссова интерполяция PS1 SPU (-6 дБ на 15 кГц, теплый тембр).</summary>
    Gaussian4Point,

    /// <summary>Линейный апсэмплинг 3DO half_rate.dsp с сохранением зеркальных частот (HF-imaging 11-22 кГц).</summary>
    Linear3DoHalfRate,

    /// <summary>Стандартная линейная интерполяция.</summary>
    Linear,

    /// <summary>Без дополнительной интерполяции.</summary>
    Bypass
}

/// <summary>
/// Профиль звучания и параметры исторического тракта воспроизведения.
/// </summary>
public sealed class AudioPreset
{
    public string Name { get; set; } = "default";
    public string Description { get; set; } = "";

    /// <summary>Тип используемого кодека (Sony ADPCM, 3DO SDX2 или Bypass).</summary>
    public AudioCodecType Codec { get; set; } = AudioCodecType.SonyAdpcm;

    /// <summary>Целевая частота дискретизации консольного потока (44100, 37800, 32000, 22050, 18900, 11025 Гц).</summary>
    public int SpuVoiceRate { get; set; } = 22050;

    /// <summary>Метод интерполяции ЦАП.</summary>
    public InterpolationType Interpolation { get; set; } = InterpolationType.Gaussian4Point;

    /// <summary>Частота предварительного антиалиасинг фильтра перед даунсэмплингом (0 = авто: 0.45 * VoiceRate).</summary>
    public float PreFilterCutoffHz { get; set; } = 0f;

    /// <summary>Включить аппаратный аналоговый фильтр выхода ЦАП.</summary>
    public bool EnableAnalogFilter { get; set; } = true;

    /// <summary>Топология аналогового фильтра (3-полюсный PS1 SPU или 2-полюсный 3DO Sallen-Key).</summary>
    public FilterTopology FilterTopology { get; set; } = FilterTopology.ThreePoleSpu;

    /// <summary>Частота среза аналогового фильтра (в Гц, например 10500 для PS1 или 20500 для 3DO).</summary>
    public float FilterCutoffHz { get; set; } = 11000f;

    /// <summary>Использовать исторический целочисленный режим Sony SDK (encvag).</summary>
    public bool AuthenticAdpcmMode { get; set; } = true;

    /// <summary>Уровень аналогового фона матрицы ЦАП (0.0 = выкл, 1.0 = тонкий консольный фон ~ -66 dBFS).</summary>
    public float SpuNoiseLevel { get; set; } = 0.5f;

    /// <summary>Мягкая аналоговая сатурация шины суммирования (0.0 = выкл, 1.0 = норма).</summary>
    public float BusGlue { get; set; } = 0.5f;

    /// <summary>Включить модуль магнитной компакт-кассеты.</summary>
    public bool EnableTape { get; set; } = false;

    /// <summary>Настройки кассетного тракта.</summary>
    public CassetteTapeSettings TapeSettings { get; set; } = new();

    /// <summary>Выходная частота дискретизации (44100 Гц — нативный ЦАП консолей).</summary>
    public int OutputSampleRate { get; set; } = 44100;
}
