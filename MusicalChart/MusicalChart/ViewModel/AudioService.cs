using System.IO;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicalChart
{
    public class AudioService
    {
        #region Fields

        private readonly Dictionary<string, MediaPlayer> _players = new();
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        public Action? OnStartPlayback;
        public Action? OnStopPlayback;
        private MusicViewModel? _viewModel;
        private bool _isLoopingEnabled = true;

        #endregion

        #region Methods

        public void SetViewModel(MusicViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public async Task LoadAudioFile(string instrumentName, string audioFilePath)
        {
            var player = new MediaPlayer();
            var tcs = new TaskCompletionSource<bool>();

            player.MediaEnded += Player_MediaEnded;

            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio", audioFilePath);
            bool fileExists = File.Exists(fullPath);

            if (fileExists)
            {
                player.MediaOpened += (s, e) => tcs.SetResult(true);
                player.MediaFailed += (s, e) => tcs.SetResult(false);

                player.Open(new Uri(fullPath));
                player.Volume = 0.7;

                await tcs.Task;
            }

            if (_players.TryGetValue(instrumentName, out var existingPlayer))
            {
                existingPlayer.MediaEnded -= Player_MediaEnded;
                existingPlayer.Close();
            }

            // Store the new player
            _players[instrumentName] = player;
        }

        public async Task PlayAudioWithFade(string instrumentName)
        {
            if (!_players.TryGetValue(instrumentName, out var player) || player == null)
                return;

            bool isEnabled = false;
            if (_viewModel != null)
            {
                isEnabled = instrumentName switch
                {
                    "Drums" => _viewModel.IsDrumsEnabled,
                    "Bass" => _viewModel.IsBassEnabled,
                    "Others" => _viewModel.IsOthersEnabled,
                    "Vocals" => _viewModel.IsVocalsEnabled,
                    _ => false
                };
            }

            if (!isEnabled)
            {
                await StopAudioWithFade(instrumentName);
                return;
            }
            await _dispatcher.InvokeAsync(() =>
            {
                player.Stop();
                player.Position = TimeSpan.Zero;
                player.MediaEnded += Player_MediaEnded;
                player.Volume = 0.7;
                player.Play();
            });
        }

        public async Task StopAudioWithFade(string instrumentName)
        {
            if (!_players.TryGetValue(instrumentName, out var player))
                return;

            await _dispatcher.InvokeAsync(() =>
            {
                player.Stop();
            });
        }

        public async Task StopAllWithFade()
        {
            foreach (var key in _players.Keys)
            {
                await StopAudioWithFade(key);
            }

            OnStopPlayback?.Invoke();
        }

        private void Player_MediaEnded(object? sender, EventArgs e)
        {
            if (_isLoopingEnabled && sender is MediaPlayer player)
            {
                player.Position = TimeSpan.Zero;
                player.Play();
            }
        }

        public void EnableLooping() => _isLoopingEnabled = true;
        public void DisableLooping() => _isLoopingEnabled = false;


        #endregion
    }
}