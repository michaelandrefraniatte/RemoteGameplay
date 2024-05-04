using NAudio.Wave;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocketSharp.Server;
using WebSocketSharp;
using System.IO;
using SharpDX.DXGI;
using SharpDX;
using SharpDX.Direct3D11;
using System.Drawing.Imaging;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using System.Threading;

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
        private static int width = Screen.PrimaryScreen.Bounds.Width, height = Screen.PrimaryScreen.Bounds.Height;
        private Device mDevice;
        private Texture2DDescription mTextureDesc;
        private OutputDescription mOutputDesc;
        private OutputDuplication mDeskDupl;
        private Texture2D desktopImageTexture = null;
        private OutputDuplicateFrameInformation frameInfo = new OutputDuplicateFrameInformation();
        private System.Drawing.Bitmap finalImage1, finalImage2;
        private bool isFinalImage1 = false;
        public static byte[] rawdataavailable;
        private System.Drawing.Bitmap FinalImage
        {
            get
            {
                return isFinalImage1 ? finalImage1 : finalImage2;
            }
            set
            {
                if (isFinalImage1)
                {
                    finalImage2 = value;
                    if (finalImage1 != null) finalImage1.Dispose();
                }
                else
                {
                    finalImage1 = value;
                    if (finalImage2 != null) finalImage2.Dispose();
                }
                isFinalImage1 = !isFinalImage1;
            }
        }
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
            InitCaptureScreen();
            Task.Run(() => CopyScreen());
        }
        private void RemoteGameplayHost_FormClosed(object sender, FormClosedEventArgs e)
        {
            closed = true;
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
        private void CopyScreen()
        {
            while (!closed)
            {
                CaptureScreen();
                System.Threading.Thread.Sleep(1);
            }
        }
        public void InitCaptureScreen()
        {
            Adapter1 adapter = null;
            try
            {
                adapter = new SharpDX.DXGI.Factory1().GetAdapter1(0);
            }
            catch { }
            this.mDevice = new Device(adapter);
            Output output = null;
            try
            {
                output = adapter.GetOutput(0);
            }
            catch { }
            var output1 = output.QueryInterface<Output1>();
            this.mOutputDesc = output.Description;
            this.mTextureDesc = new Texture2DDescription()
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            try
            {
                this.mDeskDupl = output1.DuplicateOutput(mDevice);
            }
            catch
            {
            }
        }
        public void CaptureScreen()
        {
            RetrieveFrame();
            try
            {
                ProcessFrame();
            }
            catch
            {
                ReleaseFrame();
            }
            try
            {
                ReleaseFrame();
            }
            catch
            {
            }
        }
        private void RetrieveFrame()
        {
            if (desktopImageTexture == null)
                desktopImageTexture = new Texture2D(mDevice, mTextureDesc);
            SharpDX.DXGI.Resource desktopResource = null;
            frameInfo = new OutputDuplicateFrameInformation();
            try
            {
                mDeskDupl.AcquireNextFrame(500, out frameInfo, out desktopResource);
            }
            catch { }
            using (var tempTexture = desktopResource.QueryInterface<Texture2D>())
                mDevice.ImmediateContext.CopyResource(tempTexture, desktopImageTexture);
            desktopResource.Dispose();
        }
        private void ProcessFrame()
        {
            MemoryStream file = new MemoryStream();
            var mapSource = mDevice.ImmediateContext.MapSubresource(desktopImageTexture, 0, MapMode.Read, MapFlags.None);
            FinalImage = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            var boundsRect = new System.Drawing.Rectangle(0, 0, width, height);
            var mapDest = FinalImage.LockBits(boundsRect, ImageLockMode.WriteOnly, FinalImage.PixelFormat);
            var sourcePtr = mapSource.DataPointer;
            var destPtr = mapDest.Scan0;
            for (int y = 0; y < height; y++)
            {
                Utilities.CopyMemory(destPtr, sourcePtr, width * 4);
                sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                destPtr = IntPtr.Add(destPtr, mapDest.Stride);
            }
            FinalImage.UnlockBits(mapDest);
            mDevice.ImmediateContext.UnmapSubresource(desktopImageTexture, 0);
            FinalImage.Save(file, System.Drawing.Imaging.ImageFormat.Jpeg);
            rawdataavailable = file.ToArray();
        }
        private void ReleaseFrame()
        {
            try
            {
                mDeskDupl.ReleaseFrame();
            }
            catch { }
        }
        public class LSPAudio
        {
            private static string localip;
            private static string port;
            private static Audio audio = new Audio();
            private static WebSocketServer wss;
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
            private static void GetAudioByteArray()
            {
                waveIn = new WasapiLoopbackCapture();
                waveIn.DataAvailable += audio.waveIn_DataAvailable;
                waveIn.StartRecording();
            }
            public static void Disconnect()
            {
                wss.RemoveWebSocketService("/Audio");
                wss.Stop();
                waveIn.Dispose();
            }
        }
        public class Audio : WebSocketBehavior
        {
            private static byte[] rawdataavailable = null, raw = null;
            protected override void OnMessage(MessageEventArgs e)
            {
                base.OnMessage(e);
                while (Form1.running)
                {
                    if (rawdataavailable != null)
                        Send(rawdataavailable);
                    rawdataavailable = null;
                    Thread.Sleep(1);
                }
            }
            public void waveIn_DataAvailable(object sender, WaveInEventArgs e)
            {
                raw = e.Buffer;
                Array.Resize(ref raw, e.BytesRecorded);
                rawdataavailable = raw;
            }
        }
        public class LSP1Display
        {
            private static string localip;
            private static string port;
            private static WebSocketServer wss;
            public static void Connect()
            {
                try
                {
                    localip = Form1.localip;
                    port = Form1.displayport;
                    String connectionString = "ws://" + localip + ":" + port;
                    wss = new WebSocketServer(connectionString);
                    wss.AddWebSocketService<Display1>("/1Display");
                    wss.Start();
                }
                catch { }
            }
            public static void Disconnect()
            {
                wss.RemoveWebSocketService("/1Display");
                wss.Stop();
            }
        }
        public class Display1 : WebSocketBehavior
        {
            protected override void OnMessage(MessageEventArgs e)
            {
                base.OnMessage(e);
                while (Form1.running)
                {
                    try
                    {
                        Send(Form1.rawdataavailable);
                    }
                    catch { }
                    System.Threading.Thread.Sleep(50);
                }
            }
        }
    }
}