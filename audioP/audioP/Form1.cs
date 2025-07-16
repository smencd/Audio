using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using NAudio.Wave;
using NAudio.Dsp;
using SoundTouch.Net;
using SoundTouch.Net.NAudioSupport;
using System.Linq;
using System.Collections.Generic;

namespace audioP
{
    public partial class Form1 : Form
    {
        private WaveStream waveStream;
        private WaveOutEvent outputDevice;
        //private Timer progressTimer;
        private bool userIsDraggingTrackBar = false;
        private float[] audioData;
        private Bitmap waveformBitmap;

        // Chord detection fields
        private Timer chordDetectionTimer;
        private Music musicAnalyser = new Music();
        private List<string> detectedChords = new List<string>();
        private const int CHORD_ANALYSIS_WINDOW_MS = 2000; // Analyze 2-second chunks

        public Form1()
        {
            InitializeComponent();

            trackBar2.Scroll += trackBar2_Scroll;
            trackBar2.Minimum = -100;
            trackBar2.Maximum = 100;
            trackBar2.Value = 0;
            trackBar2.SmallChange = 5;
            trackBar2.LargeChange = 10;

            progressTimer = new Timer();
            progressTimer.Interval = 50;
            progressTimer.Tick += ProgressTimer_Tick;

            trackBar1.Scroll += trackBar1_Scroll;
            trackBar1.MouseDown += (s, e) => userIsDraggingTrackBar = true;
            trackBar1.MouseUp += (s, e) => userIsDraggingTrackBar = false;

            panel1.Paint += Panel1_Paint;

            // Initialize chord detection
            chordDetectionTimer = new Timer();
            chordDetectionTimer.Interval = 1000;
            chordDetectionTimer.Tick += ChordDetectionTimer_Tick;
            panel2.Paint += Panel2_Paint;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Audio Files|*.wav;*.mp3;*.aiff|All Files|*.*";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    CleanUpResources();

                    waveStream = new AudioFileReader(openFileDialog.FileName);
                    audioData = ReadAudioData(waveStream);
                    RenderWaveform();

                    outputDevice = new WaveOutEvent();
                    outputDevice.Init(waveStream);

                    trackBar1.Maximum = (int)waveStream.TotalTime.TotalSeconds;
                    trackBar1.Value = 0;

                    label1.Text = Path.GetFileName(openFileDialog.FileName);
                    label3.Text = waveStream.TotalTime.ToString(@"mm\:ss");
                    label2.Text = "00:00";

                    button2.Enabled = true;
                    trackBar1.Enabled = true;

                    outputDevice.Play();
                    button2.Text = "Pause";
                    progressTimer.Start();

                    panel1.Invalidate();
                    chordDetectionTimer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки файла: {ex.Message}");
                }
            }
        }

        private float[] ReadAudioData(WaveStream stream)
        {
            stream.Position = 0;
            byte[] buffer = new byte[stream.Length];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            float[] data = new float[bytesRead / 4];
            Buffer.BlockCopy(buffer, 0, data, 0, bytesRead);

            return data;
        }

        private void RenderWaveform()
        {
            if (audioData == null || audioData.Length == 0) return;

            int width = panel1.Width;
            int height = panel1.Height;

            waveformBitmap = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(waveformBitmap))
            {
                g.Clear(Color.White);

                int samplesPerPixel = audioData.Length / width;
                float[] chunk = new float[samplesPerPixel];

                for (int x = 0; x < width; x++)
                {
                    int chunkStart = x * samplesPerPixel;
                    int chunkSize = Math.Min(samplesPerPixel, audioData.Length - chunkStart);

                    Array.Copy(audioData, chunkStart, chunk, 0, chunkSize);

                    float max = 0;
                    float min = 0;
                    for (int i = 0; i < chunkSize; i++)
                    {
                        max = Math.Max(max, chunk[i]);
                        min = Math.Min(min, chunk[i]);
                    }

                    int top = (int)((1 + max) * height / 2);
                    int bottom = (int)((1 + min) * height / 2);

                    g.DrawLine(Pens.Blue, x, height / 2 - top / 2, x, height / 2 - bottom / 2);
                }
            }
        }

        private void Panel1_Paint(object sender, PaintEventArgs e)
        {
            if (waveformBitmap != null)
            {
                e.Graphics.DrawImage(waveformBitmap, Point.Empty);

                if (waveStream != null && outputDevice != null)
                {
                    float posRatio = (float)waveStream.CurrentTime.TotalSeconds / (float)waveStream.TotalTime.TotalSeconds;
                    int posX = (int)(posRatio * panel1.Width);

                    e.Graphics.DrawLine(Pens.Red, posX, 0, posX, panel1.Height);
                }
            }
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            if (waveStream != null && !userIsDraggingTrackBar)
            {
                label2.Text = waveStream.CurrentTime.ToString(@"mm\:ss");
                trackBar1.Value = (int)waveStream.CurrentTime.TotalSeconds;
                panel1.Invalidate();
            }
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            if (waveStream != null && outputDevice != null)
            {
                waveStream.CurrentTime = TimeSpan.FromSeconds(trackBar1.Value);
                label2.Text = waveStream.CurrentTime.ToString(@"mm\:ss");
                panel1.Invalidate();

                if (outputDevice.PlaybackState == PlaybackState.Paused)
                {
                    outputDevice.Play();
                    outputDevice.Pause();
                }
            }
        }

        private void CleanUpResources()
        {
            if (outputDevice != null)
            {
                outputDevice.Stop();
                outputDevice.Dispose();
                outputDevice = null;
            }

            if (waveStream != null)
            {
                waveStream.Dispose();
                waveStream = null;
            }

            if (waveformBitmap != null)
            {
                waveformBitmap.Dispose();
                waveformBitmap = null;
            }

            progressTimer.Stop();
            chordDetectionTimer.Stop();

            button2.Enabled = false;
            button2.Text = "Play";
            trackBar1.Enabled = false;
            trackBar1.Value = 0;
            label1.Text = "Файл не загружен";
            label2.Text = "00:00";
            label3.Text = "00:00";

            detectedChords.Clear();
            panel1.Invalidate();
            panel2.Invalidate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            CleanUpResources();
            progressTimer.Dispose();
            chordDetectionTimer.Dispose();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (outputDevice != null)
            {
                if (outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    outputDevice.Pause();
                    button2.Text = "Play";
                    this.progressTimer.Stop(); // Явное указание this
                    this.chordDetectionTimer.Stop();
                }
                else
                {
                    outputDevice.Play();
                    button2.Text = "Pause";
                    this.progressTimer.Start(); // Явное указание this
                    this.chordDetectionTimer.Start();
                }
            }
        }

        private void panel1_Paint_1(object sender, PaintEventArgs e) { }

        private void trackBar2_Scroll(object sender, EventArgs e)
        {
            if (waveStream == null || outputDevice == null) return;

            try
            {
                var playbackState = outputDevice.PlaybackState;
                var currentTime = waveStream.CurrentTime;
                string filePath = ((AudioFileReader)waveStream).FileName;

                outputDevice.Stop();
                outputDevice.Dispose();
                waveStream.Dispose();

                float rate = 1.0f + (trackBar2.Value * 0.05f);

                var audioFile = new AudioFileReader(filePath);
                var speedControl = new SpeedAdjustmentSampleProvider(audioFile.ToSampleProvider())
                {
                    Speed = rate
                };

                outputDevice = new WaveOutEvent();
                outputDevice.Init(speedControl);
                waveStream = audioFile;
                waveStream.CurrentTime = currentTime;

                label4.Text = $"Speed: {rate * 100:F0}%";

                if (playbackState == PlaybackState.Playing)
                {
                    outputDevice.Play();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка изменения скорости: {ex.Message}");
            }
        }

        // Chord detection methods
        private void ChordDetectionTimer_Tick(object sender, EventArgs e)
        {
            if (waveStream == null || outputDevice == null || outputDevice.PlaybackState != PlaybackState.Playing)
                return;

            AnalyzeCurrentChord();
            panel2.Invalidate();
        }

        private void AnalyzeCurrentChord()
        {
            try
            {
                if (waveStream is AudioFileReader audioFile)
                {
                    var currentPosition = audioFile.Position;
                    var bytesPerMillisecond = audioFile.WaveFormat.AverageBytesPerSecond / 1000;
                    var bytesToRead = bytesPerMillisecond * CHORD_ANALYSIS_WINDOW_MS;

                    bytesToRead = (int)Math.Min(bytesToRead, audioFile.Length - currentPosition);
                    if (bytesToRead <= 0) return;

                    byte[] buffer = new byte[bytesToRead];
                    int bytesRead = audioFile.Read(buffer, 0, buffer.Length);
                    audioFile.Position = currentPosition;

                    float[] samples;
                    if (audioFile.WaveFormat.BitsPerSample == 16)
                    {
                        samples = new float[bytesRead / 2];
                        for (int i = 0; i < samples.Length; i++)
                        {
                            samples[i] = BitConverter.ToInt16(buffer, i * 2) / 32768f;
                        }
                    }
                    else if (audioFile.WaveFormat.BitsPerSample == 32 && audioFile.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                    {
                        samples = new float[bytesRead / 4];
                        Buffer.BlockCopy(buffer, 0, samples, 0, bytesRead);
                    }
                    else
                    {
                        return;
                    }

                    var chord = DetectChord(samples, audioFile.WaveFormat.SampleRate);
                    if (!string.IsNullOrEmpty(chord))
                    {
                        detectedChords.Add(chord);
                        if (detectedChords.Count > 5)
                        {
                            detectedChords.RemoveAt(0);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chord detection error: {ex.Message}");
            }
        }

        private string DetectChord(float[] samples, int sampleRate)
        {
            musicAnalyser.ResetNoteCount();
            int fftSize = 4096;
            var fftResults = new List<(float frequency, float magnitude)>();

            for (int i = 0; i < samples.Length; i += fftSize / 2)
            {
                if (i + fftSize >= samples.Length) break;

                var segment = new float[fftSize];
                Array.Copy(samples, i, segment, 0, fftSize);

                var window = new float[fftSize];
                for (int n = 0; n < fftSize; n++)
                {
                    window[n] = segment[n] * (float)FastFourierTransform.HammingWindow(n, fftSize);
                }

                var fftBuffer = new Complex[fftSize];
                for (int n = 0; n < fftSize; n++)
                {
                    fftBuffer[n].X = window[n];
                    fftBuffer[n].Y = 0;
                }

                FastFourierTransform.FFT(true, (int)Math.Log(fftSize, 2), fftBuffer);

                for (int n = 0; n < fftSize / 2; n++)
                {
                    float frequency = n * sampleRate / (float)fftSize;
                    if (frequency < 65 || frequency > 1000) continue;

                    float magnitude = (float)Math.Sqrt(fftBuffer[n].X * fftBuffer[n].X + fftBuffer[n].Y * fftBuffer[n].Y);
                    fftResults.Add((frequency, magnitude));
                }
            }

            var prominentFrequencies = fftResults
                .OrderByDescending(x => x.magnitude)
                .Take(20)
                .Select(x => x.frequency)
                .ToList();

            var detectedNotes = new List<string>();
            foreach (var freq in prominentFrequencies)
            {
                var note = musicAnalyser.GetNote(freq);
                if (note != "N/A")
                {
                    detectedNotes.Add(note);
                    musicAnalyser.CountNote(note);
                }
            }

            if (detectedNotes.Count == 0) return "No chord detected";

            int maxCount = musicAnalyser.NoteOccurences.Max();
            int rootIndex = Array.IndexOf(musicAnalyser.NoteOccurences, maxCount);
            string rootNote = Music.GetNoteName(rootIndex);

            var intervals = new List<int>();
            foreach (var note in detectedNotes.Distinct())
            {
                int noteIndex = Music.GetNoteIndex(note.Substring(0, note.Length - 1) + "0");
                int interval = (noteIndex - rootIndex + 12) % 12;
                if (interval != 0) intervals.Add(interval);
            }

            int fifthOmitted;
            string quality = Music.GetChordQuality(intervals.OrderBy(x => x).ToList(), out fifthOmitted);

            string chordName = rootNote;
            if (!string.IsNullOrEmpty(quality))
            {
                chordName += quality;
            }

            return $"{chordName} ({DateTime.Now.ToString("HH:mm:ss")})";
        }

        private void Panel2_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.White);

            if (detectedChords.Count == 0)
            {
                e.Graphics.DrawString("No chords detected",
                    new Font("Arial", 12),
                    Brushes.Black,
                    new PointF(10, 10));
                return;
            }

            float y = 10;
            foreach (var chord in detectedChords)
            {
                e.Graphics.DrawString(chord,
                    new Font("Arial", 12, FontStyle.Bold),
                    Brushes.Blue,
                    new PointF(10, y));
                y += 25;
            }

            e.Graphics.DrawString($"Current Chord: {detectedChords.LastOrDefault()}",
                new Font("Arial", 14, FontStyle.Bold),
                Brushes.Red,
                new PointF(10, y + 10));
        }
    }

    public class SpeedAdjustmentSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private float speed = 1.0f;
        private float[] sourceBuffer;
        private float position;
        private int sourceBufferLength;

        public SpeedAdjustmentSampleProvider(ISampleProvider source)
        {
            this.source = source;
            if (source.WaveFormat.BlockAlign == 0)
                throw new ArgumentException("Source wave format must have valid block alignment");
        }

        public float Speed
        {
            get => speed;
            set => speed = Math.Max(0.25f, Math.Min(4.0f, value));
        }

        public WaveFormat WaveFormat => source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = 0;
            int outIndex = offset;

            while (samplesRead < count)
            {
                if (sourceBuffer == null || position >= sourceBufferLength)
                {
                    int samplesRequired = (int)((count - samplesRead) / speed) + source.WaveFormat.BlockAlign;
                    samplesRequired = samplesRequired - (samplesRequired % source.WaveFormat.BlockAlign);

                    sourceBuffer = new float[samplesRequired];
                    int read = source.Read(sourceBuffer, 0, samplesRequired);
                    if (read == 0) break;

                    sourceBufferLength = read;
                    position = 0;
                }

                while (position < sourceBufferLength && samplesRead < count)
                {
                    buffer[outIndex++] = sourceBuffer[(int)position];
                    position += speed;
                    samplesRead++;
                }
            }

            return samplesRead;
        }
    }
}