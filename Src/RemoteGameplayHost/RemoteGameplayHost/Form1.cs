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
        public static bool running = false, closed = false;
        public static string displayport, audioport, localip;
        private static Bitmap screen, screen1;
        public static Bitmap img;
        public static Graphics graphics;
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
                }
            }
        }
        private void RemoteGameplayHost_Shown(object sender, EventArgs e)
        {
            Task.Run(() => CopyScreen());
        }
        private void RemoteGameplayHost_FormClosed(object sender, FormClosedEventArgs e)
        {
            closed = true;
            System.Threading.Thread.Sleep(100);
            img.Dispose();
            graphics.Dispose();
        }
        private void CopyScreen()
        {
            while (!closed)
            {
                img = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
                graphics = Graphics.FromImage(img);
                graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                graphics.SmoothingMode = SmoothingMode.HighSpeed;
                graphics.InterpolationMode = InterpolationMode.Low;
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.CopyFromScreen(0, 0, 0, 0, img.Size);
                screen = img;
                System.Threading.Thread.Sleep(1);
            }
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
                Task.Run(() => LSP1Display.Connect());
                Task.Run(() => LSPAudio.Connect());
            }
            else
            {
                button1.Text = "Start";
                running = false;
                System.Threading.Thread.Sleep(100);
                Task.Run(() => LSP1Display.Disconnect());
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
            public static void InitData()
            {
                rawdataavailable = null;
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
                            LSPAudio.InitData();
                        }
                        catch { }
                    }
                    System.Threading.Thread.Sleep(10);
                }
            }
        }
        public class LSP1Display
        {
            public static string localip;
            public static string port;
            public static WebSocketServer wss;
            public static byte[] rawdataavailable;
            public static int width = 0, height = 0;
            public static ImageCodecInfo myImageCodecInfo;
            public static Encoder myEncoder;
            public static EncoderParameter myEncoderParameter;
            public static EncoderParameters myEncoderParameters;
            public static void Connect()
            {
                try
                {
                    myImageCodecInfo = GetEncoderInfo("image/jpeg");
                    myEncoder = Encoder.Quality;
                    myEncoderParameters = new EncoderParameters(1);
                    myEncoderParameter = new EncoderParameter(myEncoder, 25L);
                    myEncoderParameters.Param[0] = myEncoderParameter;
                    localip = Form1.localip;
                    port = Form1.displayport;
                    String connectionString = "ws://" + localip + ":" + port;
                    wss = new WebSocketServer(connectionString);
                    wss.AddWebSocketService<Display1>("/1Display");
                    wss.Start();
                }
                catch { }
                Task.Run(() => taskSend());
            }
            private static ImageCodecInfo GetEncoderInfo(String mimeType)
            {
                int j;
                ImageCodecInfo[] encoders;
                encoders = ImageCodecInfo.GetImageEncoders();
                for (j = 0; j < encoders.Length; ++j)
                {
                    if (encoders[j].MimeType == mimeType)
                        return encoders[j];
                }
                return null;
            }
            public static void Disconnect()
            {
                wss.RemoveWebSocketService("/1Display");
                wss.Stop();
            }
            public static void taskSend()
            {
                while (Form1.running)
                {
                    try
                    {
                        screen1 = screen;
                        rawdataavailable = BitmapToByteArray(screen1);
                    }
                    catch { }
                    System.Threading.Thread.Sleep(30);
                }
            }
            public static byte[] BitmapToByteArray(Bitmap img)
            {
                using(var stream = new MemoryStream())
                {
                    img.Save(stream, myImageCodecInfo, myEncoderParameters);
                    return stream.ToArray();
                }
            }
            public static void InitData()
            {
                rawdataavailable = null;
            }
        }
        public class Display1 : WebSocketBehavior
        {
            protected override void OnMessage(MessageEventArgs e)
            {
                base.OnMessage(e);
                while (Form1.running)
                {
                    if (LSP1Display.rawdataavailable != null)
                    {
                        try
                        {
                            Send(LSP1Display.rawdataavailable);
                            LSP1Display.InitData();
                        }
                        catch { }
                    }
                    System.Threading.Thread.Sleep(30);
                }
            }
        }
    }
}