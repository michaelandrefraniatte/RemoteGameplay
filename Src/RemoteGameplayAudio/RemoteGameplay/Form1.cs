using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocketSharp;

namespace RemoteGameplay
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint TimeBeginPeriod(uint ms);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        public static extern uint TimeEndPeriod(uint ms);
        [DllImport("ntdll.dll", EntryPoint = "NtSetTimerResolution")]
        public static extern void NtSetTimerResolution(uint DesiredResolution, bool SetResolution, ref uint CurrentResolution);
        public static uint CurrentResolution = 0;
        private string ip, audioport;
        private WebSocket wscaudio;
        private BufferedWaveProvider src;
        private WasapiOut soundOut;
        private bool closed = false;
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            OnKeyDown(e.KeyData);
        }
        private void OnKeyDown(Keys keyData)
        {
            if (keyData == Keys.F1)
            {
                const string message = "• Author: Michaël André Franiatte.\n\r\n\r• Contact: michael.franiatte@gmail.com.\n\r\n\r• Publisher: https://github.com/michaelandrefraniatte.\n\r\n\r• Copyrights: All rights reserved, no permissions granted.\n\r\n\r• License: Not open source, not free of charge to use.";
                const string caption = "About";
                MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            if (keyData == Keys.Escape)
            {
                this.Close();
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            TimeBeginPeriod(1);
            NtSetTimerResolution(1, true, ref CurrentResolution);
            using (StreamReader file = new StreamReader("params.txt"))
            {
                file.ReadLine();
                ip = file.ReadLine();
                file.ReadLine();
                audioport = file.ReadLine();
            }
        }
        private void Form1_Shown(object sender, EventArgs e)
        {
            Task.Run(() => Start());
        }
        private void Start()
        {
            ConnectAudio();
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            DisconnectAudio();
        }
        public void ConnectAudio()
        {
            String connectionString = "ws://" + ip + ":" + audioport + "/Audio";
            wscaudio = new WebSocket(connectionString);
            wscaudio.OnMessage += Ws_OnMessageAudio;
            while (!wscaudio.IsAlive & !closed)
            {
                try
                {
                    wscaudio.Connect();
                    wscaudio.Send("Hello from client");
                }
                catch { }
                System.Threading.Thread.Sleep(1);
            }
            var enumerator = new MMDeviceEnumerator();
            MMDevice wasapi = null;
            foreach (var mmdevice in enumerator.EnumerateAudioEndPoints(DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active))
            {
                wasapi = mmdevice;
                break;
            }
            WaveFormat waveformat = (new WasapiLoopbackCapture()).WaveFormat;
            soundOut = new WasapiOut(wasapi, AudioClientShareMode.Shared, false, 2);
            src = new BufferedWaveProvider(WaveFormat.CreateCustomFormat(waveformat.Encoding, waveformat.SampleRate, waveformat.Channels, waveformat.AverageBytesPerSecond, waveformat.BlockAlign, waveformat.BitsPerSample));
            src.DiscardOnBufferOverflow = true;
            src.BufferLength = waveformat.AverageBytesPerSecond * 80 / 1000;
            soundOut.Init(src);
            soundOut.Play();
        }
        private void Ws_OnMessageAudio(object sender, MessageEventArgs e)
        {
            src.AddSamples(e.RawData, 0, e.RawData.Length);
        }
        public void DisconnectAudio()
        {
            closed = true;
            wscaudio.Close();
            soundOut.Stop();
        }
    }
}