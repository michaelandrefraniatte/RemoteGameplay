using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using WebSocketSharp;
using System.IO;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Windows.Forms;
using SharpDX.Direct2D1;
using SharpDX.MediaFoundation;
using System.Configuration.Internal;
using System.Drawing.Imaging;
using System.Net.Sockets;
using System.Net;
using SharpDX.Win32;

namespace RemoteGameplay
{
    public class Game1 : Game
    {
        private static IPEndPoint ipEnd;
        private static Socket sock;
        private static Socket client;
        public static int port;
        private GraphicsDeviceManager _graphics;
        private Microsoft.Xna.Framework.Graphics.SpriteBatch _spriteBatch;
        private string ip, displayport, audioport;
        public WebSocket wscaudio;
        public BufferedWaveProvider src;
        public WasapiOut soundOut;
        private int width = Screen.PrimaryScreen.Bounds.Width;
        private int height = Screen.PrimaryScreen.Bounds.Height;
        private Texture2D texture = null, texturetemp;
        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = false;
            _graphics.PreferredBackBufferWidth = width;
            _graphics.PreferredBackBufferHeight = height;
            _graphics.IsFullScreen = true;
            Form _GameForm = (Form)Form.FromHandle(Window.Handle);
            _GameForm.Closing += ClosingForm;
            using (StreamReader file = new StreamReader("params.txt"))
            {
                file.ReadLine();
                ip = file.ReadLine();
                file.ReadLine();
                displayport = file.ReadLine();
                file.ReadLine();
                audioport = file.ReadLine();
            }
            ConnectDisplay();
            ConnectAudio();
        }
        public void ClosingForm(object sender, System.ComponentModel.CancelEventArgs e)
        {
            DisconnectDisplay();
            DisconnectAudio();
        }
        protected override void Initialize()
        {
            base.Initialize();
        }
        protected override void LoadContent()
        {
            _spriteBatch = new Microsoft.Xna.Framework.Graphics.SpriteBatch(GraphicsDevice);
        }
        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == Microsoft.Xna.Framework.Input.ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
                Exit();
            base.Update(gameTime);
        }
        protected override void Draw(GameTime gameTime)
        {
            try
            {
                byte[] clientData = new byte[1024 * 300];
                int received = client.Receive(clientData);
                if (received > 0)
                {
                    byte[] Data = TrimEndDisplay(clientData);
                    if (Data.Length > 0)
                    {
                        texture = byteArrayToTexture(Data);
                        texturetemp = texture;
                        GraphicsDevice.Clear(Color.White);
                        _spriteBatch.Begin();
                        _spriteBatch.Draw(texturetemp, new Vector2(0, 0), new Microsoft.Xna.Framework.Rectangle(0, 0, width, height), Microsoft.Xna.Framework.Color.White);
                        _spriteBatch.End();
                        base.Draw(gameTime);
                    }
                }
            }
            catch { }
        }
        public void ConnectAudio()
        {
            String connectionString = "ws://" + ip + ":" + audioport + "/Audio";
            wscaudio = new WebSocket(connectionString);
            wscaudio.OnMessage += Ws_OnMessageAudio;
            while (!wscaudio.IsAlive)
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
            foreach (var mmdevice in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                wasapi = mmdevice;
                break;
            }
            soundOut = new WasapiOut(wasapi, AudioClientShareMode.Exclusive, false, 1);
            src = new BufferedWaveProvider(soundOut.OutputWaveFormat);
            soundOut.Init(src);
            soundOut.Play();
        }
        private void Ws_OnMessageAudio(object sender, MessageEventArgs e)
        {
            byte[] Data = TrimEndAudio(e.RawData);
            if (Data.Length > 0)
                src.AddSamples(Data, 0, Data.Length);
        }
        public byte[] TrimEndAudio(byte[] array)
        {
            int lastIndex = Array.FindLastIndex(array, b => b != 0);
            Array.Resize(ref array, lastIndex + 1);
            return array;
        }
        public void DisconnectAudio()
        {
            wscaudio.Close();
            soundOut.Stop();
        }
        public void ConnectDisplay()
        {
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            while (!client.Connected)
            {
                try
                {
                    port = Convert.ToInt32(displayport);
                    ipEnd = new IPEndPoint(IPAddress.Any, port);
                    sock.Blocking = true;
                    sock.Bind(ipEnd);
                    sock.Listen(100);
                    client.Blocking = true;
                    client = sock.Accept();
                }
                catch { }
                System.Threading.Thread.Sleep(1);
            }
        }
        public byte[] TrimEndDisplay(byte[] array)
        {
            int lastIndex = Array.FindLastIndex(array, b => b != 0);
            Array.Resize(ref array, lastIndex + 1);
            return array;
        }
        public void DisconnectDisplay()
        {
            client.Close();
        }
        private Texture2D byteArrayToTexture(byte[] imageBytes)
        {
            if (imageBytes.Length > 0)
            {
                using (MemoryStream ms = new MemoryStream(imageBytes))
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    Texture2D tx = Texture2D.FromStream(GraphicsDevice, ms);
                    return tx;
                }
            }
            else
            {
                return null;
            }
        }
    }
}