using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using NAudio.Wave;
using NAudio.Dsp;
using SoundTouch.Net;
using SoundTouch.Net.NAudioSupport;

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

        public Form1()
        {
            InitializeComponent();

            trackBar2.Scroll += trackBar2_Scroll;
            // Устанавливаем минимальное и максимальное значение
            trackBar2.Minimum = -100;
            trackBar2.Maximum = 100;
            trackBar2.Value = 0;  // Начальное значение - 0 (середина)

            // Опционально: настроить шаг изменения
            trackBar2.SmallChange = 5;
            trackBar2.LargeChange = 10;

            progressTimer = new Timer();
            progressTimer.Interval = 50;
            progressTimer.Tick += ProgressTimer_Tick;

            trackBar1.Scroll += trackBar1_Scroll;
            trackBar1.MouseDown += (s, e) => userIsDraggingTrackBar = true;
            trackBar1.MouseUp += (s, e) => userIsDraggingTrackBar = false;

            panel1.Paint += Panel1_Paint;
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

                    // Запускаем воспроизведение сразу после загрузки
                    outputDevice.Play();
                    button2.Text = "Pause";
                    progressTimer.Start();

                    panel1.Invalidate();
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

                // Рисуем текущую позицию воспроизведения
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

            button2.Enabled = false;
            button2.Text = "Play";
            trackBar1.Enabled = false;
            trackBar1.Value = 0;
            label1.Text = "Файл не загружен";
            label2.Text = "00:00";
            label3.Text = "00:00";

            panel1.Invalidate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            CleanUpResources();
            progressTimer.Dispose();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (outputDevice != null)
            {
                if (outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    outputDevice.Pause();
                    button2.Text = "Play";
                    progressTimer.Stop();
                }
                else
                {
                    outputDevice.Play();
                    button2.Text = "Pause";
                    progressTimer.Start();
                }
            }
        }

        private void panel1_Paint_1(object sender, PaintEventArgs e)
        {

        }
        
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
                set => speed = Math.Max(0.25f, Math.Min(4.0f, value)); // Диапазон 25%-400%
            }

            public WaveFormat WaveFormat => source.WaveFormat;

            public int Read(float[] buffer, int offset, int count)
            {
                int samplesRead = 0;
                int outIndex = offset;

                while (samplesRead < count)
                {
                    // Если буфер пуст или достигнут его конец
                    if (sourceBuffer == null || position >= sourceBufferLength)
                    {
                        // Вычисляем сколько нужно прочитать с учетом скорости
                        int samplesRequired = (int)((count - samplesRead) / speed) + source.WaveFormat.BlockAlign;
                        samplesRequired = samplesRequired - (samplesRequired % source.WaveFormat.BlockAlign);

                        sourceBuffer = new float[samplesRequired];
                        int read = source.Read(sourceBuffer, 0, samplesRequired);
                        if (read == 0) break; // Конец потока

                        sourceBufferLength = read;
                        position = 0;
                    }

                    // Копируем данные с учетом скорости
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
    
}