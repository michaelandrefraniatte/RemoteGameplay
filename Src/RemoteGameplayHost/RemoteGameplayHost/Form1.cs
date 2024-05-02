using NAudio.Wave;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocketSharp.Server;
using WebSocketSharp;
using System.IO;
using System.Drawing.Imaging;
using DesktopDuplication;

namespace RemoteGameplayHost
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint ms);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint ms);
        [DllImport("ntdll.dll", EntryPoint = "NtSetTimerResolution")]
        private static extern void NtSetTimerResolution(uint DesiredResolution, bool SetResolution, ref uint CurrentResolution);
        private static uint CurrentResolution = 0;
        private static bool running = false, closed = false;
        public static string displayport, audioport, localip;
        private static Bitmap screen, screen1;
        private DesktopDuplicator desktopDuplicator;
        private DesktopFrame frame = null;
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
            if (File.Exists("tempsave"))
            {
                using (StreamReader file = new StreamReader("tempsave"))
                {
                    textBox1.Text = file.ReadLine();
                    textBox2.Text = file.ReadLine();
                    textBox3.Text = file.ReadLine();
                }
            }
        }
        private void RemoteGameplayHost_Shown(object sender, EventArgs e)
        {
            try
            {
                desktopDuplicator = new DesktopDuplicator(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            Task.Run(() => CopyScreen());
        }
        private void RemoteGameplayHost_FormClosed(object sender, FormClosedEventArgs e)
        {
            closed = true;
        }
        private void CopyScreen()
        {
            while (!closed)
            {
                Application.DoEvents();
                frame = null;
                try
                {
                    frame = desktopDuplicator.GetLatestFrame();
                }
                catch
                {
                    desktopDuplicator = new DesktopDuplicator(0);
                    System.Threading.Thread.Sleep(1);
                    continue;
                }
                if (frame != null)
                {
                    screen = frame.DesktopImage;
                }
                System.Threading.Thread.Sleep(1);
            }
        }
        private void RemoteGameplayHost_FormClosing(object sender, FormClosingEventArgs e)
        {
            running = false;
            System.Threading.Thread.Sleep(300);
            using (StreamWriter createdfile = new StreamWriter("tempsave"))
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
            private static string localip;
            private static string port;
            private static WebSocketServer wss;
            public static byte[] rawdataavailable;
            private static byte[] rawdata;
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
            private static void GetAudioByteArray()
            {
                waveIn = new WasapiLoopbackCapture();
                waveIn.DataAvailable += waveIn_DataAvailable;
                waveIn.StartRecording();
            }
            private static void waveIn_DataAvailable(object sender, WaveInEventArgs e)
            {
                rawdata = new byte[e.BytesRecorded];
                Array.Copy(e.Buffer, 0, rawdata, 0, e.BytesRecorded);
                rawdataavailable = rawdata;
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
        public class LSP1Display
        {
            private static string localip;
            private static string port;
            private static WebSocketServer wss;
            public static byte[] rawdataavailable;
            private static ImageCodecInfo myImageCodecInfo;
            private static Encoder myEncoder;
            private static EncoderParameter myEncoderParameter;
            private static EncoderParameters myEncoderParameters;
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
            private static void taskSend()
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
            private static byte[] BitmapToByteArray(Bitmap img)
            {
                using(var stream = new MemoryStream())
                {
                    img.Save(stream, myImageCodecInfo, myEncoderParameters);
                    return stream.ToArray();
                }
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
                            LSP1Display.rawdataavailable = null;
                        }
                        catch { }
                    }
                    System.Threading.Thread.Sleep(30);
                }
            }
        }
    }
}