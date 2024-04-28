using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using WebSocketSharp;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Windows.Forms;
using SharpDX.Direct3D9;

namespace RemoteGameplay
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private Microsoft.Xna.Framework.Graphics.SpriteBatch _spriteBatch;
        private string ip, displayport, audioport;
        private WebSocket wscaudio, wsc1display, wsc2display;
        private BufferedWaveProvider src;
        private WasapiOut soundOut;
        private int width = Screen.PrimaryScreen.Bounds.Width;
        private int height = Screen.PrimaryScreen.Bounds.Height;
        private Texture2D texture1 = null, texture1temp = null, texture2 = null, texture2temp = null;
        private byte[] Data1Display = null, Data2Display = null, DataAudio = null;
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
            Connect1Display();
            Connect2Display();
            ConnectAudio();
        }
        public void ClosingForm(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Disconnect1Display();
            Disconnect2Display();
            DisconnectAudio();
        }
        protected override void Initialize()
        {
            base.Initialize();
        }
        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
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
                texture1temp = texture1;
                texture2temp = texture2;
                GraphicsDevice.Clear(Color.White);
                _spriteBatch.Begin();
                _spriteBatch.Draw(texture1temp, new Vector2(0, 0), new Rectangle(0, 0, width, height / 2), Color.White);
                _spriteBatch.Draw(texture2temp, new Vector2(0, height / 2), new Rectangle(0, 0, width, height / 2), Color.White);
                _spriteBatch.End();
                base.Draw(gameTime);
                System.Threading.Thread.Sleep(30);
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
            foreach (var mmdevice in enumerator.EnumerateAudioEndPoints(DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active))
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
            DataAudio = e.RawData;
            if (DataAudio.Length > 0)
                src.AddSamples(DataAudio, 0, DataAudio.Length);
        }
        public void DisconnectAudio()
        {
            wscaudio.Close();
            soundOut.Stop();
        }
        public void Connect1Display()
        {
            String connectionString = "ws://" + ip + ":" + displayport + "/1Display";
            wsc1display = new WebSocket(connectionString);
            wsc1display.OnMessage += Ws_OnMessage1Display;
            while (!wsc1display.IsAlive)
            {
                try
                {
                    wsc1display.Connect();
                    wsc1display.Send("Hello from client");
                }
                catch { }
                System.Threading.Thread.Sleep(1);
            }
        }
        private void Ws_OnMessage1Display(object sender, MessageEventArgs e)
        {
            Data1Display = e.RawData;
            if (Data1Display.Length > 0)
                texture1 = byteArrayToTexture(Data1Display);
        }
        public void Disconnect1Display()
        {
            wsc1display.Close();
        }
        public void Connect2Display()
        {
            String connectionString = "ws://" + ip + ":" + (Convert.ToInt32(displayport) + 1).ToString() + "/2Display";
            wsc2display = new WebSocket(connectionString);
            wsc2display.OnMessage += Ws_OnMessage2Display;
            while (!wsc2display.IsAlive)
            {
                try
                {
                    wsc2display.Connect();
                    wsc2display.Send("Hello from client");
                }
                catch { }
                System.Threading.Thread.Sleep(1);
            }
        }
        private void Ws_OnMessage2Display(object sender, MessageEventArgs e)
        {
            Data2Display = e.RawData;
            if (Data2Display.Length > 0)
                texture2 = byteArrayToTexture(Data2Display);
        }
        public void Disconnect2Display()
        {
            wsc2display.Close();
        }
        private Texture2D byteArrayToTexture(byte[] imageBytes)
        {
            if (imageBytes.Length > 0)
            {
                using (var stream = new MemoryStream(imageBytes))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    var tx = Texture2D.FromStream(GraphicsDevice, stream);
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