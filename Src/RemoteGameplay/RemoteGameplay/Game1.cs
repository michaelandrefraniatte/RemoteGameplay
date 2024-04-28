using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using WebSocketSharp;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Windows.Forms;

namespace RemoteGameplay
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private Microsoft.Xna.Framework.Graphics.SpriteBatch _spriteBatch;
        private string ip, displayport, audioport;
        private WebSocket wscaudio, wscdisplay;
        private BufferedWaveProvider src;
        private WasapiOut soundOut;
        private int width = Screen.PrimaryScreen.Bounds.Width;
        private int height = Screen.PrimaryScreen.Bounds.Height;
        private Texture2D texture = null;
        private byte[] DataDisplay = null, DataAudio = null;
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
                if (DataDisplay.Length > 0)
                    texture = byteArrayToTexture(DataDisplay);
                GraphicsDevice.Clear(Color.White);
                _spriteBatch.Begin();
                _spriteBatch.Draw(texture, new Vector2(0, 0), new Microsoft.Xna.Framework.Rectangle(0, 0, width, height), Color.White);
                _spriteBatch.End();
                base.Draw(gameTime);
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
            soundOut = new WasapiOut(wasapi, AudioClientShareMode.Exclusive, false, 0);
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
        public void ConnectDisplay()
        {
            String connectionString = "ws://" + ip + ":" + displayport + "/Display";
            wscdisplay = new WebSocket(connectionString);
            wscdisplay.OnMessage += Ws_OnMessageDisplay;
            while (!wscdisplay.IsAlive)
            {
                try
                {
                    wscdisplay.Connect();
                    wscdisplay.Send("Hello from client");
                }
                catch { }
                System.Threading.Thread.Sleep(1);
            }
        }
        private void Ws_OnMessageDisplay(object sender, MessageEventArgs e)
        {
            DataDisplay = e.RawData;
        }
        public void DisconnectDisplay()
        {
            wscdisplay.Close();
        }
        private Texture2D byteArrayToTexture(byte[] imageBytes)
        {
            try
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
            catch 
            {  
                return null; 
            }
        }
    }
}