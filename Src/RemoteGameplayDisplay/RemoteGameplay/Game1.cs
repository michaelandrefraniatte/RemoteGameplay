using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using WebSocketSharp;
using System.IO;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RemoteGameplay
{
    public class Game1 : Game
    {
        static void OnKeyDown(System.Windows.Forms.Keys keyData)
        {
            if (keyData == System.Windows.Forms.Keys.F1)
            {
                const string message = "• Author: Michaël André Franiatte.\n\r\n\r• Contact: michael.franiatte@gmail.com.\n\r\n\r• Publisher: https://github.com/michaelandrefraniatte.\n\r\n\r• Copyrights: All rights reserved, no permissions granted.\n\r\n\r• License: Not open source, not free of charge to use.";
                const string caption = "About";
                System.Windows.Forms.MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint TimeBeginPeriod(uint ms);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        public static extern uint TimeEndPeriod(uint ms);
        [DllImport("ntdll.dll", EntryPoint = "NtSetTimerResolution")]
        public static extern void NtSetTimerResolution(uint DesiredResolution, bool SetResolution, ref uint CurrentResolution);
        public static uint CurrentResolution = 0;
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private string ip, displayport;
        private WebSocket wsc1display;
        private int width;
        private int height;
        private Texture2D texture1 = null, texture1temp = null;
        private bool closed = false;
        public Game1()
        {
            TimeBeginPeriod(1);
            NtSetTimerResolution(1, true, ref CurrentResolution);
            using (StreamReader file = new StreamReader("params.txt"))
            {
                file.ReadLine();
                ip = file.ReadLine();
                file.ReadLine();
                displayport = file.ReadLine();
                file.ReadLine();
                width = Convert.ToInt32(file.ReadLine());
                file.ReadLine();
                height = Convert.ToInt32(file.ReadLine());
                _graphics = new GraphicsDeviceManager(this);
                Content.RootDirectory = "Content";
                IsMouseVisible = false;
                _graphics.PreferredBackBufferWidth = width;
                _graphics.PreferredBackBufferHeight = height;
                _graphics.IsFullScreen = true;
                Exiting += Shutdown;
                Connect1Display();
            }
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
            if (Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftAlt) & Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F4))
            {
                Disconnect1Display(); 
            }
            base.Update(gameTime);
        }
        public void Shutdown(object sender, EventArgs e)
        {
            Disconnect1Display();
        }
        protected override void Draw(GameTime gameTime)
        {
            try
            {
                texture1temp = texture1; 
                GraphicsDevice.Clear(Color.White);
                _spriteBatch.Begin();
                _spriteBatch.Draw(texture1temp, new Vector2(0, 0), new Rectangle(0, 0, width, height), Color.White);
                _spriteBatch.End();
                base.Draw(gameTime);
            }
            catch { }
        }
        public void Connect1Display()
        {
            String connectionString = "ws://" + ip + ":" + displayport + "/1Display";
            wsc1display = new WebSocket(connectionString);
            wsc1display.OnMessage += Ws_OnMessage1Display;
            while (!wsc1display.IsAlive & !closed)
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
            texture1 = byteArrayToTexture(e.RawData);
        }
        public void Disconnect1Display()
        {
            closed = true;
            wsc1display.Close();
            Exit();
            Environment.Exit(0);
        }
        private Texture2D byteArrayToTexture(byte[] imageBytes)
        {
            using (var stream = new MemoryStream(imageBytes))
            {
                stream.Seek(0, SeekOrigin.Begin);
                var tx = Texture2D.FromStream(GraphicsDevice, stream);
                return tx;
            }
        }
    }
}