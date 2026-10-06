using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using OldSound.Core.Audio;
using OldSound.Core.Dsp;
using OldSound.Core.Pipeline;
using Wpf.Ui.Controls;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace OldSound.Gui;

public partial class MainWindow : FluentWindow
{
    private string? _inputFilePath;
    private AudioBuffer? _inputBuffer;
    private AudioBuffer? _outputBuffer;
    private string? _tempOriginalPath;
    private string? _tempProcessedPath;
    private string? _lastSavedPath;

    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _timer = new();
    private bool _isDraggingTimeline = false;
    private bool _isUpdatingUiFromPreset = false;
    private bool _isInitialized = false;

    private bool _isSwitchingSource = false;
    private TimeSpan? _pendingSeekPosition = null;
    private bool _wasPlayingBeforeSwitch = false;

    private AudioPreset _currentPreset;

    public record PresetItem(string Key, string Title, AudioPreset Preset);

    public MainWindow()
    {
        InitializeComponent();
        _isInitialized = true;

        RbSourceProcessed.IsChecked = true;
        _currentPreset = PresetRegistry.Get(PresetRegistry.DefaultPresetName);

        _timer.Interval = TimeSpan.FromMilliseconds(150);
        _timer.Tick += Timer_Tick;

        _player.MediaOpened += (s, e) =>
        {
            _isSwitchingSource = false;

            if (_player.NaturalDuration.HasTimeSpan)
            {
                SliderTimeline.Maximum = _player.NaturalDuration.TimeSpan.TotalSeconds;
                TxtTimeTotal.Text = FormatTime(_player.NaturalDuration.TimeSpan);
            }

            if (_pendingSeekPosition.HasValue)
            {
                try
                {
                    _player.Position = _pendingSeekPosition.Value;
                }
                catch { }
                _pendingSeekPosition = null;
            }

            if (_wasPlayingBeforeSwitch)
            {
                _player.Play();
                _timer.Start();
                BtnPlay.Content = "Pause";
            }
        };

        _player.MediaFailed += (s, e) =>
        {
            _isSwitchingSource = false;
        };

        _player.MediaEnded += (s, e) =>
        {
            if (_isSwitchingSource) return;
            if (_player.NaturalDuration.HasTimeSpan && _player.Position < _player.NaturalDuration.TimeSpan - TimeSpan.FromMilliseconds(500))
            {
                return;
            }

            _player.Stop();
            _timer.Stop();
            BtnPlay.Content = "Play";
            SliderTimeline.Value = 0;
            TxtTimeCurrent.Text = "00:00";
        };

        PopulatePresets();
    }

    private static string FormatPresetTitle(string name) => name switch
    {
        "four-sight-1995" => "★ Four-Sight (1995 3DO SDX2)",
        "ps1-spu-1994" => "PlayStation (1994 SPU VAG)",
        "cassette-type1" => "Компакт-кассета (Type I Tape)",
        _ => name
    };

    private void PopulatePresets()
    {
        var order = new[] { "four-sight-1995", "ps1-spu-1994", "cassette-type1" };
        var allPresets = PresetRegistry.GetAll().ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var items = new List<PresetItem>();
        foreach (var key in order)
        {
            if (allPresets.TryGetValue(key, out var p))
            {
                items.Add(new PresetItem(p.Name, FormatPresetTitle(p.Name), p));
            }
        }

        ComboPresets.ItemsSource = items;
        ComboPresets.DisplayMemberPath = "Title";
        ComboPresets.SelectedIndex = 0;
    }

    private void ComboPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || ComboPresets.SelectedItem is not PresetItem item) return;
        LoadPresetToControls(item.Preset);
    }

    private void LoadPresetToControls(AudioPreset p)
    {
        _isUpdatingUiFromPreset = true;
        try
        {
            _currentPreset = p;

            // Codec: 0 = SDX2 (8-bit delta), 1 = 4-Bit (adpcm), 2 = Bypass
            ComboCodec.SelectedIndex = p.Codec switch
            {
                AudioCodecType.Sdx2_3Do => 0,
                AudioCodecType.SonyAdpcm => 1,
                _ => 2
            };

            // Interpolation: 0 = 3DO Linear, 1 = Raw Steps, 2 = Gaussian, 3 = Bypass
            ComboInterp.SelectedIndex = p.Interpolation switch
            {
                InterpolationType.Linear3DoHalfRate => 0,
                InterpolationType.Linear => 1,
                InterpolationType.Gaussian4Point => 2,
                _ => 3
            };

            // Clock Rate
            SliderVoiceRate.Value = p.SpuVoiceRate;
            TxtVoiceRate.Text = $"{p.SpuVoiceRate} Hz";

            // Crystalline Aliasing Switch
            SwitchAliasing.IsChecked = (p.PreFilterCutoffHz <= 0f);

            // Filter Cutoff
            SliderFilterCutoff.Value = p.FilterCutoffHz;
            TxtFilterCutoff.Text = $"{p.FilterCutoffHz:F0} Hz";

            // Noise Model & Floor (Master Analog)
            ComboNoiseProfile.SelectedIndex = (p.NoiseProfile == AnalogNoiseProfile.Cassette) ? 1 : 0;
            SliderNoise.Value = p.SpuNoiseLevel;
            TxtNoise.Text = (p.SpuNoiseLevel > 0.001f) ? $"{(int)(p.SpuNoiseLevel * 100)}%" : "Off";

            UpdateMetrics(p);
        }
        finally
        {
            _isUpdatingUiFromPreset = false;
        }
    }

    private void UpdateMetrics(AudioPreset p)
    {
        string codecStr = p.Codec switch
        {
            AudioCodecType.SonyAdpcm => "4-Bit",
            AudioCodecType.Sdx2_3Do => "SDX2",
            _ => "PCM"
        };
        string dacStr = p.Interpolation switch
        {
            InterpolationType.Linear3DoHalfRate => "3DO Linear",
            InterpolationType.Linear => "Raw Steps",
            InterpolationType.Gaussian4Point => "Gaussian",
            _ => "Bypass"
        };
        string aliasStr = (SwitchAliasing.IsChecked == true) ? "Raw Aliased" : "Studio AA (Clean)";
        string noiseType = (ComboNoiseProfile?.SelectedIndex == 1) ? "Tape" : "SPU";
        string noiseStr = (p.SpuNoiseLevel > 0.001f) ? $"{noiseType} {(int)(p.SpuNoiseLevel * 100)}%" : "Clean";

        TxtStatBar.Text = $"{codecStr} • {p.SpuVoiceRate} Hz • {dacStr} • {aliasStr} • {noiseStr}";
    }

    private void ComboNoiseProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || _isUpdatingUiFromPreset) return;
        _currentPreset.NoiseProfile = (ComboNoiseProfile.SelectedIndex == 1)
            ? AnalogNoiseProfile.Cassette
            : AnalogNoiseProfile.Console;
        UpdateMetrics(_currentPreset);
    }

    private void ComboCodec_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || _isUpdatingUiFromPreset) return;
        _currentPreset.Codec = ComboCodec.SelectedIndex switch
        {
            0 => AudioCodecType.Sdx2_3Do,
            1 => AudioCodecType.SonyAdpcm,
            _ => AudioCodecType.Bypass
        };
        UpdateMetrics(_currentPreset);
    }

    private void ComboInterp_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || _isUpdatingUiFromPreset) return;
        _currentPreset.Interpolation = ComboInterp.SelectedIndex switch
        {
            0 => InterpolationType.Linear3DoHalfRate,
            1 => InterpolationType.Linear,
            2 => InterpolationType.Gaussian4Point,
            _ => InterpolationType.Bypass
        };
        UpdateMetrics(_currentPreset);
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized || _isUpdatingUiFromPreset) return;

        if (sender == SliderVoiceRate && TxtVoiceRate != null)
        {
            TxtVoiceRate.Text = $"{(int)SliderVoiceRate.Value} Hz";
            _currentPreset.SpuVoiceRate = (int)SliderVoiceRate.Value;
        }
        else if (sender == SliderFilterCutoff && TxtFilterCutoff != null)
        {
            TxtFilterCutoff.Text = $"{SliderFilterCutoff.Value:F0} Hz";
            _currentPreset.FilterCutoffHz = (float)SliderFilterCutoff.Value;
        }
        else if (sender == SliderNoise && TxtNoise != null)
        {
            float val = (float)SliderNoise.Value;
            TxtNoise.Text = (val > 0.001f) ? $"{(int)(val * 100)}%" : "Off";
            _currentPreset.SpuNoiseLevel = val;
        }
        UpdateMetrics(_currentPreset);
    }

    private void SwitchAliasing_Click(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _isUpdatingUiFromPreset) return;
        _currentPreset.PreFilterCutoffHz = (SwitchAliasing.IsChecked == true) ? 0f : 10500f;
        UpdateMetrics(_currentPreset);
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        ComboPresets.SelectedIndex = 0;
    }

    #region Drag & Drop and File Selection

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0 && IsAudioFile(files[0]))
            {
                LoadFile(files[0]);
            }
        }
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open Audio File",
            Filter = "Audio Files (*.mp3;*.wav;*.flac;*.ogg;*.aif)|*.mp3;*.wav;*.flac;*.ogg;*.aif;*.aiff|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            LoadFile(dlg.FileName);
        }
    }

    private static bool IsAudioFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".mp3" or ".wav" or ".flac" or ".ogg" or ".aif" or ".aiff";
    }

    private void LoadFile(string path)
    {
        try
        {
            _inputFilePath = path;
            var fi = new FileInfo(path);

            TxtFileName.Text = Path.GetFileName(path);
            TxtFileMeta.Text = $"{fi.Length / 1024.0 / 1024.0:F2} MB • {fi.Extension.ToUpperInvariant()}";

            BtnProcess.IsEnabled = true;
            TxtStatus.Text = "Loaded";
            TxtStatusDetails.Text = "Click Render to process.";

            _outputBuffer = null;
            _inputBuffer = null;
            _player.Stop();
            _timer.Stop();
            BtnPlay.IsEnabled = false;
            BtnStop.IsEnabled = false;
            BtnSaveAs.IsEnabled = false;
            BtnOpenFolder.IsEnabled = false;
            RbSourceOriginal.IsEnabled = false;
            RbSourceProcessed.IsEnabled = false;

            // Pre-load input into buffer and save as temporary WAV for instant, zero-latency A/B comparison
            Task.Run(() =>
            {
                try
                {
                    _inputBuffer = AudioBridge.Load(_inputFilePath);
                    RetroAudioPipeline.EnsureSafetyHeadroom(_inputBuffer, 0.85f);
                    string tempDir = Path.GetTempPath();
                    _tempOriginalPath = Path.Combine(tempDir, $"oldsound_orig_{Guid.NewGuid():N}.wav");
                    using var fs = File.Create(_tempOriginalPath);
                    WavCodec.Write(_inputBuffer, fs, bitsPerSample: 16);
                }
                catch { }
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"File error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Processing Execution

    private async void BtnProcess_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_inputFilePath) || !File.Exists(_inputFilePath)) return;

        var selectedCodec = ComboCodec.SelectedIndex switch
        {
            0 => AudioCodecType.Sdx2_3Do,
            1 => AudioCodecType.SonyAdpcm,
            _ => AudioCodecType.Bypass
        };

        var selectedInterp = ComboInterp.SelectedIndex switch
        {
            0 => InterpolationType.Linear3DoHalfRate,
            1 => InterpolationType.Linear,
            2 => InterpolationType.Gaussian4Point,
            _ => InterpolationType.Bypass
        };

        var preset = new AudioPreset
        {
            Name = _currentPreset.Name,
            Description = _currentPreset.Description,
            Codec = selectedCodec,
            Interpolation = selectedInterp,
            SpuVoiceRate = (int)SliderVoiceRate.Value,
            EnableAnalogFilter = SliderFilterCutoff.Value < 21900f,
            FilterTopology = (selectedCodec == AudioCodecType.Sdx2_3Do)
                ? FilterTopology.TwoPoleSallenKey
                : FilterTopology.ThreePoleSpu,
            FilterCutoffHz = (float)SliderFilterCutoff.Value,
            PreFilterCutoffHz = (SwitchAliasing.IsChecked == true) ? 0f : 10500f,
            AuthenticAdpcmMode = true,
            SpuNoiseLevel = (float)SliderNoise.Value,
            NoiseProfile = (ComboNoiseProfile.SelectedIndex == 1) ? AnalogNoiseProfile.Cassette : AnalogNoiseProfile.Console,
            BusGlue = 0.0f,
            EnableTape = _currentPreset.EnableTape,
            TapeSettings = _currentPreset.TapeSettings,
            OutputSampleRate = 44100
        };

        BtnProcess.IsEnabled = false;
        ProgressBarStatus.Visibility = Visibility.Visible;
        ProgressBarStatus.IsIndeterminate = true;
        TxtStatus.Text = "Rendering...";
        TxtStatusDetails.Text = $"{preset.Codec} @ {preset.SpuVoiceRate} Hz";

        var sw = Stopwatch.StartNew();

        try
        {
            await Task.Run(() =>
            {
                if (_inputBuffer == null)
                {
                    _inputBuffer = AudioBridge.Load(_inputFilePath);
                }

                _outputBuffer = RetroAudioPipeline.Process(_inputBuffer, preset);

                string tempDir = Path.GetTempPath();
                _tempProcessedPath = Path.Combine(tempDir, $"oldsound_preview_{Guid.NewGuid():N}.wav");
                using (var fs = File.Create(_tempProcessedPath))
                {
                    WavCodec.Write(_outputBuffer, fs, bitsPerSample: 16);
                }
            });

            sw.Stop();
            TxtStatus.Text = "Ready";
            string noiseType = (preset.NoiseProfile == AnalogNoiseProfile.Cassette) ? "Tape" : "SPU";
            TxtStatusDetails.Text = $"Rendered in {sw.Elapsed.TotalSeconds:F2}s • {noiseType} Noise: {(int)(preset.SpuNoiseLevel * 100)}%";

            BtnPlay.IsEnabled = true;
            BtnStop.IsEnabled = true;
            BtnSaveAs.IsEnabled = true;
            BtnOpenFolder.IsEnabled = true;
            RbSourceOriginal.IsEnabled = true;
            RbSourceProcessed.IsEnabled = true;

            if (RbSourceProcessed.IsChecked != true)
            {
                RbSourceProcessed.IsChecked = true; // Триггерит AudioSource_Changed -> SwitchSource()
            }
            else
            {
                SwitchSource();
            }
        }
        catch (Exception ex)
        {
            TxtStatus.Text = "Failed";
            TxtStatusDetails.Text = ex.Message;
            System.Windows.MessageBox.Show($"DSP error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnProcess.IsEnabled = true;
            ProgressBarStatus.IsIndeterminate = false;
            ProgressBarStatus.Visibility = Visibility.Collapsed;
        }
    }

    #endregion

    #region Audio Player Deck (A/B Monitoring)

    private void AudioSource_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _outputBuffer == null) return;
        SwitchSource();
    }

    private void SwitchSource()
    {
        bool useProcessed = RbSourceProcessed?.IsChecked == true;
        string? targetPath = useProcessed ? _tempProcessedPath : _tempOriginalPath;
        if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) return;

        TimeSpan currentPos = _player.Position;
        bool wasPlaying = _timer.IsEnabled;

        _isSwitchingSource = true;
        _pendingSeekPosition = currentPos;
        _wasPlayingBeforeSwitch = wasPlaying;

        // Надежное закрытие старого медиа-графа перед загрузкой нового рендера
        _player.Stop();
        _player.Close();

        _player.Open(new Uri(targetPath));
    }

    private void BtnPlay_Click(object sender, RoutedEventArgs e)
    {
        if (_timer.IsEnabled)
        {
            _player.Pause();
            _timer.Stop();
            BtnPlay.Content = "Play";
        }
        else
        {
            _player.Play();
            _timer.Start();
            BtnPlay.Content = "Pause";
        }
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _player.Stop();
        _timer.Stop();
        BtnPlay.Content = "Play";
        SliderTimeline.Value = 0;
        TxtTimeCurrent.Text = "00:00";
    }

    private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;
        _player.Volume = e.NewValue;
        if (TxtVolume != null)
        {
            TxtVolume.Text = $"{(int)(e.NewValue * 100)}%";
        }
    }

    private void SliderTimeline_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingTimeline = true;
    }

    private void SliderTimeline_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingTimeline = false;
        _player.Position = TimeSpan.FromSeconds(SliderTimeline.Value);
    }

    private void SliderTimeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isDraggingTimeline)
        {
            TxtTimeCurrent.Text = FormatTime(TimeSpan.FromSeconds(SliderTimeline.Value));
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_isDraggingTimeline && _player.NaturalDuration.HasTimeSpan)
        {
            double pos = _player.Position.TotalSeconds;
            SliderTimeline.Value = pos;
            TxtTimeCurrent.Text = FormatTime(_player.Position);
        }
    }

    private static string FormatTime(TimeSpan ts)
    {
        return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
    }

    #endregion

    #region Export

    private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_outputBuffer == null || string.IsNullOrEmpty(_tempProcessedPath)) return;

        string origName = Path.GetFileNameWithoutExtension(_inputFilePath ?? "audio");
        var dlg = new SaveFileDialog
        {
            Title = "Export Audio",
            FileName = $"{origName}_foursight.wav",
            Filter = "WAV PCM 16-bit (*.wav)|*.wav|MP3 Audio (*.mp3)|*.mp3|FLAC Lossless (*.flac)|*.flac"
        };

        if (dlg.ShowDialog() == true)
        {
            string outPath = dlg.FileName;
            string ext = Path.GetExtension(outPath).ToLowerInvariant();

            try
            {
                if (ext == ".wav")
                {
                    File.Copy(_tempProcessedPath, outPath, overwrite: true);
                }
                else
                {
                    AudioBridge.Save(_outputBuffer, outPath);
                }

                _lastSavedPath = outPath;
                TxtStatus.Text = "Exported";
                TxtStatusDetails.Text = Path.GetFileName(outPath);
                System.Windows.MessageBox.Show($"File exported successfully:\n{outPath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Export error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        string? target = _lastSavedPath ?? _inputFilePath;
        if (!string.IsNullOrEmpty(target) && File.Exists(target))
        {
            Process.Start("explorer.exe", $"/select,\"{Path.GetFullPath(target)}\"");
        }
        else
        {
            string outDir = Path.GetFullPath("output_samples");
            if (Directory.Exists(outDir))
            {
                Process.Start("explorer.exe", outDir);
            }
        }
    }

    #endregion
}
