using System;
using System.Diagnostics;
using System.IO;
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

namespace OldSound.Gui;

public partial class MainWindow : Window
{
    private string? _inputFilePath;
    private AudioBuffer? _inputBuffer;
    private AudioBuffer? _outputBuffer;
    private string? _tempProcessedPath;
    private string? _lastSavedPath;

    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _timer = new();
    private bool _isDraggingTimeline = false;
    private bool _isUpdatingUiFromPreset = false;

    public MainWindow()
    {
        InitializeComponent();

        _timer.Interval = TimeSpan.FromMilliseconds(200);
        _timer.Tick += Timer_Tick;

        _player.MediaOpened += (s, e) =>
        {
            if (_player.NaturalDuration.HasTimeSpan)
            {
                SliderTimeline.Maximum = _player.NaturalDuration.TimeSpan.TotalSeconds;
                TxtTimeTotal.Text = FormatTime(_player.NaturalDuration.TimeSpan);
            }
        };

        _player.MediaEnded += (s, e) =>
        {
            _player.Stop();
            _timer.Stop();
            BtnPlay.Content = "▶ Воспроизвести";
            SliderTimeline.Value = 0;
            TxtTimeCurrent.Text = "00:00";
        };

        // Инициализируем пресет по умолчанию (Four-Sight 3DO)
        LoadPresetToControls("four-sight-3do");

        // Если в тестах лежит o.mp3, сразу предварительно загрузим его для удобства
        string defaultTestTrack = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tests", "o.mp3"));
        if (File.Exists(defaultTestTrack))
        {
            LoadFile(defaultTestTrack);
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

    #region Drag & Drop and File Selection

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
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

    private void DropArea_Click(object sender, MouseButtonEventArgs e)
    {
        BtnChooseFile_Click(sender, e);
    }

    private void BtnChooseFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выберите аудиофайл",
            Filter = "Аудиофайлы (*.mp3;*.wav;*.flac;*.ogg;*.aif)|*.mp3;*.wav;*.flac;*.ogg;*.aif;*.aiff|Все файлы (*.*)|*.*"
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
            TxtFileMeta.Text = $"{fi.Length / 1024.0 / 1024.0:F2} МБ • {fi.Extension.ToUpperInvariant()} • Загружен";

            DropAreaBorder.Visibility = Visibility.Collapsed;
            FileInfoBadge.Visibility = Visibility.Visible;
            BtnProcess.IsEnabled = true;
            TxtStatus.Text = "Файл готов к обработке. Выберите пресет или настройте параметры и нажмите Render.";

            // Сброс предыдущего результата
            _outputBuffer = null;
            _player.Stop();
            _timer.Stop();
            BtnPlay.IsEnabled = false;
            BtnPause.IsEnabled = false;
            BtnStop.IsEnabled = false;
            BtnSaveAs.IsEnabled = false;
            BtnOpenFolder.IsEnabled = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка выбора файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Preset Selection & Parameter Sync

    private void PresetRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;

        string presetName = rb.Name switch
        {
            "Rb3DoFourSight" => "four-sight-3do",
            "RbPsxXa" => "four-sight-psx-xa",
            "RbPsxSpu" => "four-sight-psx-spu",
            "RbSilentHill" => "psx-silent-hill",
            "RbPsxLofi" => "psx-lofi-11k",
            "RbTapeFerric" => "cassette-ferric",
            "RbTapeHybrid" => "psx-tape-hybrid",
            _ => "four-sight-3do"
        };

        LoadPresetToControls(presetName);
    }

    private void LoadPresetToControls(string presetName)
    {
        _isUpdatingUiFromPreset = true;
        try
        {
            var p = PresetRegistry.Get(presetName);

            // Кодек
            CbCodec.SelectedIndex = p.Codec switch
            {
                AudioCodecType.Sdx2_3Do => 0,
                AudioCodecType.SonyAdpcm => 1,
                _ => 2
            };

            // Интерполятор
            CbInterp.SelectedIndex = p.Interpolation switch
            {
                InterpolationType.Linear3DoHalfRate => 0,
                InterpolationType.Gaussian4Point => 1,
                InterpolationType.Linear => 2,
                _ => 3
            };

            // Частота голоса
            SliderVoiceRate.Value = p.SpuVoiceRate;
            TxtVoiceRate.Text = $"{p.SpuVoiceRate} Гц";

            // Срез фильтра
            SliderFilterCutoff.Value = p.FilterCutoffHz;
            TxtFilterCutoff.Text = $"{p.FilterCutoffHz:F0} Гц";
            ChkAnalogFilter.IsChecked = p.EnableAnalogFilter;

            // Шум ЦАП
            SliderNoiseFloor.Value = p.SpuNoiseLevel;
            TxtNoiseFloor.Text = p.SpuNoiseLevel > 0.01f ? $"-{(68 - p.SpuNoiseLevel * 6):F0} dBFS" : "Выкл";

            // Bus Glue
            SliderBusGlue.Value = p.BusGlue;
            TxtBusGlue.Text = $"{p.BusGlue:F2}x";

            // Кассета
            ChkTape.IsChecked = p.EnableTape;
            SliderTapeDrive.Value = p.TapeSettings.Drive;
            TxtTapeDrive.Text = $"{p.TapeSettings.Drive:F2}x";
            SliderTapeMod.Value = p.TapeSettings.WowDepth;
            TxtTapeMod.Text = $"{(int)(p.TapeSettings.WowDepth * 100)}%";
        }
        finally
        {
            _isUpdatingUiFromPreset = false;
        }
    }

    private void ParamControl_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUiFromPreset) return;
        // Пользователь изменил параметр вручную
    }

    private void SliderVoiceRate_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtVoiceRate != null)
            TxtVoiceRate.Text = $"{(int)SliderVoiceRate.Value} Гц";
    }

    private void SliderFilterCutoff_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtFilterCutoff != null)
            TxtFilterCutoff.Text = $"{SliderFilterCutoff.Value:F0} Гц";
    }

    private void SliderNoiseFloor_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtNoiseFloor != null)
        {
            float val = (float)SliderNoiseFloor.Value;
            TxtNoiseFloor.Text = val > 0.01f ? $"-{(68 - val * 6):F0} dBFS" : "Выкл";
        }
    }

    private void SliderBusGlue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtBusGlue != null)
            TxtBusGlue.Text = $"{SliderBusGlue.Value:F2}x";
    }

    private void SliderTapeDrive_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtTapeDrive != null)
            TxtTapeDrive.Text = $"{SliderTapeDrive.Value:F2}x";
    }

    private void SliderTapeMod_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtTapeMod != null)
            TxtTapeMod.Text = $"{(int)(SliderTapeMod.Value * 100)}%";
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        Rb3DoFourSight.IsChecked = true;
        LoadPresetToControls("four-sight-3do");
    }

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "OLDSOUND DSP EMULATOR\n\n" +
            "• 3DO Four-Sight (1995): Фирменный 8-битный кодек SDX2 (нелинейный дифференциал по квадратному корню) " +
            "сохраняет микродинамику тихих пассажей рояля Юдзи Номи и мягко сглаживает фронты резких ударов. " +
            "Апсэмплинг half_rate.dsp воспроизводит зеркальный верхний спектр (11-22 кГц).\n\n" +
            "• PS1 CD-XA (1996): 18.9 кГц ADPCM с аппаратным срезом 8.5 кГц прямо на ЦАП AK4309.\n\n" +
            "• PS1 SPU VAG: 22.05 кГц с аппаратной гауссовой интерполяцией ЦАП, срезающей гармоники от 8 кГц и выше.\n\n" +
            "• Все параметры откалиброваны с гарантией отсутствия цифрового клиппинга (-0.2 dBFS ceiling).",
            "О программе OldSound", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    #region Processing Execution

    private async void BtnProcess_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_inputFilePath) || !File.Exists(_inputFilePath))
        {
            MessageBox.Show("Пожалуйста, сначала выберите аудиофайл.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Собираем параметры из UI
        var preset = new AudioPreset
        {
            Codec = CbCodec.SelectedIndex switch
            {
                0 => AudioCodecType.Sdx2_3Do,
                1 => AudioCodecType.SonyAdpcm,
                _ => AudioCodecType.Bypass
            },
            Interpolation = CbInterp.SelectedIndex switch
            {
                0 => InterpolationType.Linear3DoHalfRate,
                1 => InterpolationType.Gaussian4Point,
                2 => InterpolationType.Linear,
                _ => InterpolationType.Bypass
            },
            SpuVoiceRate = (int)SliderVoiceRate.Value,
            EnableAnalogFilter = ChkAnalogFilter.IsChecked == true,
            FilterTopology = (CbCodec.SelectedIndex == 0) ? FilterTopology.TwoPoleSallenKey : FilterTopology.ThreePoleSpu,
            FilterCutoffHz = (float)SliderFilterCutoff.Value,
            PreFilterCutoffHz = (CbCodec.SelectedIndex == 0) ? 10000f : Math.Min(10500f, (float)SliderVoiceRate.Value * 0.45f),
            SpuNoiseLevel = (float)SliderNoiseFloor.Value,
            BusGlue = (float)SliderBusGlue.Value,
            EnableTape = ChkTape.IsChecked == true,
            TapeSettings = new CassetteTapeSettings
            {
                Drive = (float)SliderTapeDrive.Value,
                WowDepth = (float)SliderTapeMod.Value,
                FlutterDepth = (float)SliderTapeMod.Value * 0.75f,
                HissLevel = 0.2f,
                CutoffHz = 11500f,
                EnableHeadEq = true,
                Mix = 1.0f
            },
            OutputSampleRate = 44100
        };

        BtnProcess.IsEnabled = false;
        ProgressBar.Visibility = Visibility.Visible;
        TxtStatus.Text = "Обработка аудио... Чтение и применение физической модели тракта...";
        TxtBench.Text = "";

        var sw = Stopwatch.StartNew();

        try
        {
            await Task.Run(() =>
            {
                // Загружаем входной буфер, если еще не загружен
                if (_inputBuffer == null)
                {
                    _inputBuffer = AudioBridge.Load(_inputFilePath);
                }

                // Применяем ретро-тракт
                _outputBuffer = RetroAudioPipeline.Process(_inputBuffer, preset);

                // Сохраняем временный файл WAV для мгновенного A/B прослушивания
                string tempDir = Path.GetTempPath();
                _tempProcessedPath = Path.Combine(tempDir, $"oldsound_preview_{Guid.NewGuid():N}.wav");
                using (var fs = File.Create(_tempProcessedPath))
                {
                    WavCodec.Write(_outputBuffer, fs, bitsPerSample: 16);
                }
            });

            sw.Stop();
            TxtBench.Text = $"✓ Выполнено за {sw.Elapsed.TotalSeconds:F2} сек ({_outputBuffer!.LengthSamples} сэмплов)";
            TxtStatus.Text = $"Обработка завершена! Звук реконструирован с параметрами: {preset.Codec}, {preset.SpuVoiceRate} Гц, LPF {preset.FilterCutoffHz:F0} Гц.";

            // Активируем плеер и экспорт
            BtnPlay.IsEnabled = true;
            BtnPause.IsEnabled = true;
            BtnStop.IsEnabled = true;
            BtnSaveAs.IsEnabled = true;
            BtnOpenFolder.IsEnabled = true;

            // Загружаем результат в плеер
            SetupPlayerSource(useProcessed: RbPlayProcessed.IsChecked == true);
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Ошибка обработки: {ex.Message}";
            MessageBox.Show($"Произошла ошибка при обработке: {ex.Message}", "Ошибка DSP", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnProcess.IsEnabled = true;
            ProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    #endregion

    #region Audio Player Deck (A/B Monitoring)

    private void AudioSource_Changed(object sender, RoutedEventArgs e)
    {
        if (_outputBuffer == null) return;
        bool useProcessed = RbPlayProcessed.IsChecked == true;
        TimeSpan currentPos = _player.Position;
        bool wasPlaying = _timer.IsEnabled;

        SetupPlayerSource(useProcessed);

        if (wasPlaying)
        {
            _player.Position = currentPos;
            _player.Play();
        }
    }

    private void SetupPlayerSource(bool useProcessed)
    {
        string? targetPath = useProcessed ? _tempProcessedPath : _inputFilePath;
        if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) return;

        _player.Open(new Uri(targetPath));
        TxtNowPlaying.Text = useProcessed ? "▶ Воспроизведение: Ретро-результат" : "▶ Воспроизведение: Оригинальный трек";
    }

    private void BtnPlay_Click(object sender, RoutedEventArgs e)
    {
        _player.Play();
        _timer.Start();
        BtnPlay.Content = "▶ Воспроизведение...";
    }

    private void BtnPause_Click(object sender, RoutedEventArgs e)
    {
        _player.Pause();
        _timer.Stop();
        BtnPlay.Content = "▶ Воспроизвести";
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _player.Stop();
        _timer.Stop();
        BtnPlay.Content = "▶ Воспроизвести";
        SliderTimeline.Value = 0;
        TxtTimeCurrent.Text = "00:00";
    }

    private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _player.Volume = SliderVolume.Value;
        if (TxtVolume != null)
            TxtVolume.Text = $"{(int)(SliderVolume.Value * 100)}%";
    }

    private void SliderTimeline_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingTimeline = true;
    }

    private void SliderTimeline_MouseUp(object sender, MouseButtonEventArgs e)
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

    #endregion

    #region Export

    private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_outputBuffer == null || string.IsNullOrEmpty(_tempProcessedPath)) return;

        string origName = Path.GetFileNameWithoutExtension(_inputFilePath ?? "audio");
        var dlg = new SaveFileDialog
        {
            Title = "Сохранить обработанный ретро-трек",
            FileName = $"{origName}_retro.wav",
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
                TxtStatus.Text = $"Файл успешно сохранен: {Path.GetFileName(outPath)}";
                MessageBox.Show($"Файл успешно сохранен:\n{outPath}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
