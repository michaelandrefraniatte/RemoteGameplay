using NAudio.Wave;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocketSharp.Server;
using WebSocketSharp;
using System.IO;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace RemoteGameplayHost
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
        [DllImport("User32.dll")]
        public static extern bool GetCursorPos(out int x, out int y);
        [DllImport("user32.dll")]
        public static extern void SetCursorPos(int X, int Y);
        [DllImport("ntdll.dll", EntryPoint = "NtSetTimerResolution")]
        public static extern void NtSetTimerResolution(uint DesiredResolution, bool SetResolution, ref uint CurrentResolution);
        public static uint CurrentResolution = 0;
        public static bool running = false;
        public static string displayport, audioport, localip;
        public static int width = 0, height = 0;
        private void RemoteGameplayHost_Load(object sender, EventArgs e)
        {
            TimeBeginPeriod(1);
            NtSetTimerResolution(1, true, ref CurrentResolution);
            if (System.IO.File.Exists("tempsave"))
            {
                using (System.IO.StreamReader file = new System.IO.StreamReader("tempsave"))
                {
                    textBox1.Text = file.ReadLine();
                    textBox2.Text = file.ReadLine();
                    textBox3.Text = file.ReadLine();
                    textBox4.Text = file.ReadLine();
                    textBox5.Text = file.ReadLine();
                }
            }
        }
        private void RemoteGameplayHost_Shown(object sender, EventArgs e)
        {
        }
        private void RemoteGameplayHost_FormClosed(object sender, FormClosedEventArgs e)
        {
        }
        private void RemoteGameplayHost_FormClosing(object sender, FormClosingEventArgs e)
        {
            running = false;
            System.Threading.Thread.Sleep(300);
            using (System.IO.StreamWriter createdfile = new System.IO.StreamWriter("tempsave"))
            {
                createdfile.WriteLine(textBox1.Text);
                createdfile.WriteLine(textBox2.Text);
                createdfile.WriteLine(textBox3.Text);
                createdfile.WriteLine(textBox4.Text);
                createdfile.WriteLine(textBox5.Text);
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (!running)
            {
                button1.Text = "Stop";
                running = true;
                localip = textBox1.Text;
                displayport = textBox2.Text;
                audioport = textBox3.Text;
                width = Convert.ToInt32(textBox4.Text);
                height = Convert.ToInt32(textBox5.Text);
                Task.Run(() => LSPDisplay.Connect());
                Task.Run(() => LSPAudio.Connect());
            }
            else
            {
                button1.Text = "Start";
                running = false;
                System.Threading.Thread.Sleep(100);
                Task.Run(() => LSPDisplay.Disconnect());
                Task.Run(() => LSPAudio.Disconnect());
            }
        }
        public class LSPAudio
        {
            public static string localip;
            public static string port;
            public static WebSocketServer wss;
            public static byte[] rawdataavailable;
            private static WasapiLoopbackCapture waveIn = null;
            public static void Connect()
            {
                try
                {
                    localip = Form1.localip;
                    port = Form1.audioport;
                    String connectionString = "ws://" + localip + ":" + port;
                    wss = new WebSocketServer(connectionString);
                    wss.AddWebSocketService<Audio>("/Audio");
                    wss.Start();
                    GetAudioByteArray();
                }
                catch { }
            }
            public static void Disconnect()
            {
                wss.RemoveWebSocketService("/Audio");
                wss.Stop();
                waveIn.Dispose();
            }
            public static void GetAudioByteArray()
            {
                waveIn = new WasapiLoopbackCapture();
                waveIn.DataAvailable += waveIn_DataAvailable;
                waveIn.StartRecording();
            }
            private static void waveIn_DataAvailable(object sender, WaveInEventArgs e)
            {
                if (e.BytesRecorded > 0)
                {
                    byte[] rawdata = new byte[e.BytesRecorded];
                    Array.Copy(e.Buffer, 0, rawdata, 0, e.BytesRecorded);
                    rawdataavailable = rawdata;
                }
            }
        }
        public class Audio : WebSocketBehavior
        {
            protected override void OnMessage(MessageEventArgs e)
            {
                base.OnMessage(e);
                while (Form1.running)
                {
                    if (LSPAudio.rawdataavailable != null)
                    {
                        try
                        {
                            Send(LSPAudio.rawdataavailable);
                            LSPAudio.rawdataavailable = null;
                        }
                        catch { }
                    }
                    System.Threading.Thread.Sleep(1);
                }
            }
        }
        public class LSPDisplay
        {
            public static string localip;
            public static string port;
            public static WebSocketServer wss;
            public static byte[] rawdataavailable;
            public static int width = 0, height = 0;
            public static void Connect()
            {
                try
                {
                    width = Form1.width;
                    height = Form1.height;
                    localip = Form1.localip;
                    port = Form1.displayport;
                    String connectionString = "ws://" + localip + ":" + port;
                    wss = new WebSocketServer(connectionString);
                    wss.AddWebSocketService<Display>("/Display");
                    wss.Start();
                }
                catch { }
                Task.Run(() => taskSend());
            }
            public static void Disconnect()
            {
                wss.RemoveWebSocketService("/Display");
                wss.Stop();
            }
            public static void taskSend()
            {
                while (Form1.running)
                {
                    try
                    {
                        Bitmap img = new Bitmap(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height);
                        Graphics graphics = Graphics.FromImage(img as System.Drawing.Image);
                        graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                        graphics.SmoothingMode = SmoothingMode.HighSpeed;
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.CompositingQuality = CompositingQuality.HighSpeed;
                        graphics.CopyFromScreen(0, 0, 0, 0, img.Size);
                        Bitmap output = new Bitmap(width, height);
                        Graphics g = Graphics.FromImage(output);
                        g.DrawImage(img, 0, 0, width, height);
                        rawdataavailable = BitmapToByteArray(output);
                        img.Dispose();
                        graphics.Dispose();
                        output.Dispose();
                        g.Dispose();
                    }
                    catch { }
                    System.Threading.Thread.Sleep(40);
                }
            }
            public static byte[] BitmapToByteArray(Bitmap img)
            {
                using(var stream = new MemoryStream())
                {
                    img.Save(stream, ImageFormat.Png);
                    return stream.ToArray();
                }
            }
        }
        public class Display : WebSocketBehavior
        {
            protected override void OnMessage(MessageEventArgs e)
            {
                base.OnMessage(e);
                while (Form1.running)
                {
                    if (LSPDisplay.rawdataavailable != null)
                    {
                        try
                        {
                            Send(LSPDisplay.rawdataavailable);
                            LSPDisplay.rawdataavailable = null;
                        }
                        catch { }
                    }
                    System.Threading.Thread.Sleep(1);
                }
            }
        }
    }
}