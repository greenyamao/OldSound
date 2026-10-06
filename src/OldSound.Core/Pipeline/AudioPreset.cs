using OldSound.Core.Dsp;

namespace OldSound.Core.Pipeline;

/// <summary>
/// Audio compression codec type.
/// </summary>
public enum AudioCodecType
{
    /// <summary>4-bit PS1 Sony ADPCM (VAG) with 5 adaptive autoregressive prediction filters.</summary>
    SonyAdpcm,

    /// <summary>8-bit 3DO Opera SDX2 (Square-Root Delta) with non-linear difference quantization.</summary>
    Sdx2_3Do,

    /// <summary>Linear 16-bit PCM bypass (lossless codec bypass).</summary>
    Bypass
}

/// <summary>
/// DAC sample interpolation and reconstruction method.
/// </summary>
public enum InterpolationType
{
    /// <summary>Hardware 4-point Gaussian interpolation from PS1 SPU (-6 dB at 15 kHz, warm timbre).</summary>
    Gaussian4Point,

    /// <summary>Linear upsampler from 3DO half_rate.dsp with preserved high-frequency mirror imaging in 11-22 kHz.</summary>
    Linear3DoHalfRate,

    /// <summary>Standard linear interpolation.</summary>
    Linear,

    /// <summary>No interpolation (bypass).</summary>
    Bypass
}

/// <summary>
/// Analog noise engine profile.
/// </summary>
public enum AnalogNoiseProfile
{
    /// <summary>Console path (PS1/3DO SPU DAC, power supply 50/100 Hz hum, CRT/DMA 15.6 kHz clock whine, 16.5 kHz roll-off).</summary>
    Console,

    /// <summary>Analog compact cassette (Type I NAB 120µs, 8.2 kHz resonance, 14.2 kHz gap loss, 50/68 Hz motor rumble).</summary>
    Cassette
}

/// <summary>
/// Sound profile and configuration for historical audio playback pipeline.
/// </summary>
public sealed class AudioPreset
{
    public string Name { get; set; } = "default";
    public string Description { get; set; } = "";

    /// <summary>Audio codec type (Sony ADPCM, 3DO SDX2, or Bypass).</summary>
    public AudioCodecType Codec { get; set; } = AudioCodecType.SonyAdpcm;

    /// <summary>Console internal voice sample rate (44100, 37800, 32000, 22050, 18900, 11025 Hz).</summary>
    public int SpuVoiceRate { get; set; } = 22050;

    /// <summary>DAC reconstruction interpolation method.</summary>
    public InterpolationType Interpolation { get; set; } = InterpolationType.Gaussian4Point;

    /// <summary>Pre-filtering anti-aliasing cutoff frequency before downsampling (0 = bypass / raw aliasing).</summary>
    public float PreFilterCutoffHz { get; set; } = 0f;

    /// <summary>Enable hardware analog output reconstruction filter.</summary>
    public bool EnableAnalogFilter { get; set; } = true;

    /// <summary>Analog filter topology (3-pole PS1 SPU or 2-pole 3DO Sallen-Key).</summary>
    public FilterTopology FilterTopology { get; set; } = FilterTopology.ThreePoleSpu;

    /// <summary>Analog filter cutoff frequency in Hz (e.g. 10500 for PS1 or 20500 for 3DO).</summary>
    public float FilterCutoffHz { get; set; } = 11000f;

    /// <summary>Use historical Sony SDK (encvag) integer calculation mode.</summary>
    public bool AuthenticAdpcmMode { get; set; } = true;

    /// <summary>Analog DAC noise floor level (0.0 = off, 1.0 = standard ~ -66 dBFS).</summary>
    public float SpuNoiseLevel { get; set; } = 0.5f;

    /// <summary>Analog noise profile (Console or Cassette).</summary>
    public AnalogNoiseProfile NoiseProfile { get; set; } = AnalogNoiseProfile.Console;

    /// <summary>Soft analog summing bus saturation (0.0 = off, 1.0 = standard).</summary>
    public float BusGlue { get; set; } = 0.5f;

    /// <summary>Enable compact cassette tape emulation module.</summary>
    public bool EnableTape { get; set; } = false;

    /// <summary>Cassette tape settings.</summary>
    public CassetteTapeSettings TapeSettings { get; set; } = new();

    /// <summary>Output sample rate (44100 Hz native console DAC).</summary>
    public int OutputSampleRate { get; set; } = 44100;
}
