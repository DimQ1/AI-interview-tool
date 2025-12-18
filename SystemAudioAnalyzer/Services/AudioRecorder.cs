using NAudio.Wave;
using System;
using System.IO;
using System.Timers;

namespace SystemAudioAnalyzer.Services
{
    public class AudioRecorder
    {
        private WasapiLoopbackCapture? _capture;
        private WaveFileWriter? _writer;
        private readonly string _outputFolder;
        private string? _currentFilePath;
        private System.Timers.Timer? _chunkTimer;
        private bool _isRecording;
        private readonly object _lock = new object();

        public event EventHandler<string>? AudioChunkReady;

        public AudioRecorder()
        {
            _outputFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AUDIO");
            if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
            }
        }

        public void StartRecording()
        {
            if (_isRecording) return;

            _capture = new WasapiLoopbackCapture();
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;

            StartNewChunk();
            _capture.StartRecording();
            _isRecording = true;

            _chunkTimer = new System.Timers.Timer(30000); // 5 seconds chunks
            _chunkTimer.Elapsed += OnChunkTimerElapsed;
            _chunkTimer.Start();
        }

        public void StopRecording()
        {
            if (!_isRecording) return;

            _chunkTimer?.Stop();
            _capture?.StopRecording();
            // _capture.Dispose() is called in OnRecordingStopped usually or we should do it here but wait for stop.
            // WasapiLoopbackCapture.StopRecording is async-ish, it triggers RecordingStopped.
            // We'll dispose in RecordingStopped.
        }

        private void OnChunkTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            lock (_lock)
            {
                if (_isRecording)
                {
                    FinalizeChunk();
                    StartNewChunk();
                }
            }
        }

        private void StartNewChunk()
        {
            if (_capture == null) return;

            string fileName = $"chunk_{DateTime.Now:yyyyMMdd_HHmmss_fff}.wav";
            _currentFilePath = Path.Combine(_outputFolder, fileName);
            _writer = new WaveFileWriter(_currentFilePath, _capture.WaveFormat);
        }

        private void FinalizeChunk()
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Dispose();
                _writer = null;
                if (_currentFilePath != null)
                {
                    AudioChunkReady?.Invoke(this, _currentFilePath);
                }
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            lock (_lock)
            {
                if (_writer != null)
                {
                    _writer.Write(e.Buffer, 0, e.BytesRecorded);
                }
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            lock (_lock)
            {
                FinalizeChunk();
                _isRecording = false;
                _capture?.Dispose();
                _capture = null;
                _chunkTimer?.Dispose();
                _chunkTimer = null;
            }
        }
    }
}
