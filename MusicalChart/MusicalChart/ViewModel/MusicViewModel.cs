using Syncfusion.UI.Xaml.Charts;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicalChart
{
    public class MusicViewModel : INotifyPropertyChanged
    {
        #region Fields

        private readonly AudioService _audioService = new();
        private readonly Random _random = new();
        private DispatcherTimer? _playbackTimer;
        private double _currentX;
        private const double MaxX = 10.5;
        private VerticalLineAnnotation? _playbackLine;

        private readonly string[] _instrumentNames = {
            "Drums", "Bass", "Others", "Vocals"
        };

        private readonly Dictionary<string, bool> _enabledStatus = new();
        private readonly Dictionary<string, ObservableCollection<Track>> _chartData = new();

        #endregion

        #region Properties

        private bool _isChartPlaybackActive;
        public bool IsChartPlaybackActive
        {
            get => _isChartPlaybackActive;
            set { _isChartPlaybackActive = value; OnPropertyChanged(nameof(IsChartPlaybackActive)); }
        }

        private double _playbackPosition;
        public double PlaybackPosition
        {
            get => _playbackPosition;
            set { _playbackPosition = value; OnPropertyChanged(nameof(PlaybackPosition)); }
        }

        private bool _isPlayButtonEnabled = true;
        public bool IsPlayButtonEnabled
        {
            get => _isPlayButtonEnabled;
            set { _isPlayButtonEnabled = value; OnPropertyChanged(nameof(IsPlayButtonEnabled)); }
        }

        private bool _isStopButtonEnabled;
        public bool IsStopButtonEnabled
        {
            get => _isStopButtonEnabled;
            set { _isStopButtonEnabled = value; OnPropertyChanged(nameof(IsStopButtonEnabled)); }
        }

        public bool IsDrumsEnabled { get => _enabledStatus["Drums"]; set => SetInstrumentEnabled("Drums", value); }
        public bool IsBassEnabled { get => _enabledStatus["Bass"]; set => SetInstrumentEnabled("Bass", value); }
        public bool IsOthersEnabled { get => _enabledStatus["Others"]; set => SetInstrumentEnabled("Others", value); }
        public bool IsVocalsEnabled { get => _enabledStatus["Vocals"]; set => SetInstrumentEnabled("Vocals", value); }

        public ObservableCollection<Track> DrumsData => _chartData["Drums"];
        public ObservableCollection<Track> BassData => _chartData["Bass"];
        public ObservableCollection<Track> OthersData => _chartData["Others"];
        public ObservableCollection<Track> VocalsData => _chartData["Vocals"];

        public ICommand PlayAllCommand { get; }
        public ICommand StopAllCommand { get; }

        #endregion

        #region Constructor

        public MusicViewModel()
        {
            foreach (var instrument in _instrumentNames)
            {
                bool defaultEnabled = true;
                _enabledStatus[instrument] = defaultEnabled;
                _chartData[instrument] = new ObservableCollection<Track>();
            }

            PlayAllCommand = new RelayCommand(() => _ = PlayAllAsync());
            StopAllCommand = new RelayCommand(() => _ = StopAllAsync());

            _audioService.SetViewModel(this);
        }

        #endregion

        #region Methods

        public async Task InitializeAsync(VerticalLineAnnotation playbackLine)
        {
            _playbackLine = playbackLine;
            GenerateChartData();
            await LoadAudioFiles();

            SetupPlaybackTimer();
            _audioService.OnStartPlayback = StartPlayback;
            _audioService.OnStopPlayback = StopPlayback;
        }

        private void SetInstrumentEnabled(string instrument, bool value)
        {
            if (_enabledStatus.TryGetValue(instrument, out var currentValue) && currentValue == value)
                return;

            _enabledStatus[instrument] = value;
            OnPropertyChanged($"Is{instrument}Enabled");

            if (IsChartPlaybackActive)
            {
                if (value)
                    _ = _audioService.PlayAudioWithFade(instrument);
                else
                {
                    _ = _audioService.StopAudioWithFade(instrument);

                    if (!_enabledStatus.Values.Any(v => v))
                        _ = StopAllAsync();
                }
            }

            RefreshInstrumentData(instrument);
        }

        private void RefreshInstrumentData(string instrument)
        {
            if (_chartData.TryGetValue(instrument, out var data))
            {
                var tempData = new List<Track>(data);
                data.Clear();
                foreach (var point in tempData)
                    data.Add(point);
            }
        }

        private void StartPlayback()
        {
            if (_currentX >= MaxX)
            {
                _currentX = 0;
                PlaybackPosition = 0;
                if (_playbackLine != null)
                    _playbackLine.X1 = 0;
            }

            IsPlayButtonEnabled = false;
            IsStopButtonEnabled = true;
            IsChartPlaybackActive = true;

            if (_playbackTimer != null && !_playbackTimer.IsEnabled)
            {
                _playbackTimer.Start();
            }
        }

        private void StopPlayback()
        {
            _playbackTimer?.Stop();

            IsPlayButtonEnabled = true;
            IsStopButtonEnabled = false;
            IsChartPlaybackActive = false;
        }

        private void SetupPlaybackTimer()
        {
            _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _playbackTimer.Tick += async (s, e) =>
            {
                var increment = 0.22;
                _currentX += increment;
                PlaybackPosition = _currentX;

                if (_playbackLine != null)
                    _playbackLine.X1 = _currentX;

                ApplySeriesEffect(_currentX);

                if (_currentX >= MaxX)
                {
                    _playbackTimer.Stop();
                    _currentX = 0;
                    PlaybackPosition = 0;
                    IsChartPlaybackActive = false;
                    await _audioService.StopAllWithFade();
                    IsPlayButtonEnabled = true;
                    IsStopButtonEnabled = false;
                }
            };
        }

        private void GenerateChartData()
        {
            int baseY = 10;
            int totalPoints = 200;
            double step = 10.5 / totalPoints;

            foreach (var instrument in _instrumentNames)
            {
                var data = _chartData[instrument];
                data.Clear();

                Color baseColor = instrument switch
                {
                    "Drums" => (Color)ColorConverter.ConvertFromString("#4D1060DC"),
                    "Bass" => (Color)ColorConverter.ConvertFromString("#4D00B553"),
                    "Others" => (Color)ColorConverter.ConvertFromString("#4DDA6902"),
                    "Vocals" => (Color)ColorConverter.ConvertFromString("#4DC71969"),
                    _ => Colors.Transparent
                };

                for (int i = 0; i < totalPoints; i++)
                {
                    double x = i * step; // X stays within 0–20
                    int y = baseY + _random.Next(-5, 5);
                    data.Add(new Track(x, y, baseColor));
                }

                baseY += 30;
            }
        }

        private async Task LoadAudioFiles()
        {
            foreach (var name in _instrumentNames)
            {
                string fileName = $"{name.ToLower()}.mp3";
                await _audioService.LoadAudioFile(name, fileName);
            }
        }

        private async Task PlayAllAsync()
        {
            if (!_enabledStatus.Values.Any(v => v))
                return;

            // Reset playback position to start
            _currentX = 0;
            PlaybackPosition = 0;
            if (_playbackLine != null)
                _playbackLine.X1 = 0;

            IsPlayButtonEnabled = false;
            IsStopButtonEnabled = true;
            IsChartPlaybackActive = true;

            await _audioService.StopAllWithFade();

            _audioService.OnStartPlayback?.Invoke();
            await PlayEnabledInstruments();

            // Ensure timer is started
            if (_playbackTimer != null && !_playbackTimer.IsEnabled)
            {
                _playbackTimer.Start();
            }

            ResetTrackColors();
        }

        private async Task StopAllAsync()
        {
            IsStopButtonEnabled = false;
            IsPlayButtonEnabled = true;
            IsChartPlaybackActive = false;

            _playbackTimer?.Stop();
            await _audioService.StopAllWithFade();
        }

        private async Task PlayEnabledInstruments()
        {
            var enabledInstruments = _instrumentNames
                    .Where(i => _enabledStatus[i])
                    .ToList();

            if (enabledInstruments.Count == 0)
            {
                await StopAllAsync();
                return;
            }

            var tasks = enabledInstruments.Select(i => _audioService.PlayAudioWithFade(i));
            await Task.WhenAll(tasks);

            // Make sure timer starts even if audio has issues
            if (_playbackTimer != null && !_playbackTimer.IsEnabled)
            {
                _playbackTimer.Start();
            }
        }

        private void ApplySeriesEffect(double currentX)
        {
            foreach (var instrument in _instrumentNames)
            {
                if (_chartData.TryGetValue(instrument, out var data))
                {
                    foreach (var track in data)
                    {
                        if (track.X <= currentX)
                        {

                            Color baseColor = instrument switch
                            {
                                "Drums" => (Color)ColorConverter.ConvertFromString("#1060DC"),
                                "Bass" => (Color)ColorConverter.ConvertFromString("#00B553"),
                                "Others" => (Color)ColorConverter.ConvertFromString("#DA6902"),
                                "Vocals" => (Color)ColorConverter.ConvertFromString("#C71969"),
                                _ => Colors.Transparent
                            };

                            track.ColorPath = new SolidColorBrush(baseColor);
                        }
                    }

                    // Force chart to refresh visuals
                    RefreshInstrumentData(instrument);
                }
            }
        }

        private void ResetTrackColors()
        {
            foreach (var instrument in _instrumentNames)
            {
                if (_chartData.TryGetValue(instrument, out var data))
                {
                    Color baseColor = instrument switch
                    {
                        "Drums" => (Color)ColorConverter.ConvertFromString("#4D1060DC"),
                        "Bass" => (Color)ColorConverter.ConvertFromString("#4D00B553"),
                        "Others" => (Color)ColorConverter.ConvertFromString("#4DDA6902"),
                        "Vocals" => (Color)ColorConverter.ConvertFromString("#4DC71969"),
                        _ => Colors.Transparent
                    };

                    foreach (var track in data)
                    {
                        track.ColorPath = new SolidColorBrush(baseColor);
                    }

                    RefreshInstrumentData(instrument);
                }
            }
        }


        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

    }
}