using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.IO;
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
        private string ip, audioport;
        private WebSocket wscaudio;
        private BufferedWaveProvider src;
        private WasapiOut soundOut;
        private bool closed = false;
        private void Form1_Load(object sender, EventArgs e)
        {
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
            soundOut = new WasapiOut(wasapi, AudioClientShareMode.Exclusive, false, 2);
            src = new BufferedWaveProvider(soundOut.OutputWaveFormat);
            src.DiscardOnBufferOverflow = true;
            src.BufferDuration = TimeSpan.FromMilliseconds(80);
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