using System;
using NAudio.Wave;

namespace VERTER.Services
{
    public class PlayerService : IDisposable
    {
        private IWavePlayer? _outputDevice;
        private AudioFileReader? _audioFile;

        public event Action? PlaybackStopped;

        public bool IsPlaying => _outputDevice?.PlaybackState == PlaybackState.Playing;

        public void Play(string filePath)
        {
            Stop();

            try
            {
                _audioFile = new AudioFileReader(filePath);
                _outputDevice = new WaveOutEvent();
                _outputDevice.Init(_audioFile);
                _outputDevice.PlaybackStopped += OnPlaybackStopped;
                _outputDevice.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Playback error: {ex.Message}");
            }
        }

        public void Pause()
        {
            _outputDevice?.Pause();
        }

        public void Resume()
        {
            _outputDevice?.Play();
        }

        public void Stop()
        {
            _outputDevice?.Stop();
            _audioFile?.Dispose();
            _outputDevice?.Dispose();
            _audioFile = null;
            _outputDevice = null;
        }

        public void Seek(double positionPercent)
        {
            if (_audioFile != null)
            {
                _audioFile.Position = (long)(_audioFile.Length * positionPercent / 100);
            }
        }

        public double GetCurrentPositionPercent()
        {
            if (_audioFile == null || _audioFile.Length == 0) return 0;
            return (double)_audioFile.Position / _audioFile.Length * 100;
        }

        public float Volume
        {
            get => _audioFile?.Volume ?? 1.0f;
            set { if (_audioFile != null) _audioFile.Volume = value; }
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            PlaybackStopped?.Invoke();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
