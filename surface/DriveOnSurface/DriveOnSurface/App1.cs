using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Surface;
using Microsoft.Surface.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System.Threading;
using System.Net;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Linq;

namespace DriveOnSurface
{
    /// <summary>
    /// This is the main type for your application.
    /// </summary>
    public class App1 : Microsoft.Xna.Framework.Game
    {
        private readonly GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;

        private TouchTarget touchTarget;
        private Color backgroundColor = new Color(0, 0, 0);
        private bool applicationLoadCompleteSignalled;

        private UserOrientation currentOrientation = UserOrientation.Bottom;
        private Matrix screenTransform = Matrix.Identity;

        // Les objets du jeu

        Dictionary<string, IDrawableObject> DrawableObjects = new Dictionary<string, IDrawableObject>();

        SpriteFont spriteFont;

        OutToFile redir = new OutToFile("log.txt");

        String serverURL;

        Dictionary<String, TouchPoint> TagValues = new Dictionary<string, TouchPoint>();

        enum GameState { menu, play }

        GameState CurrentState = GameState.menu;

        Background.Track selectedTrack = Background.Track.Menu;

        bool debug = false;

        List<RectangleOverlay> rects = new List<RectangleOverlay>();

        /// <summary>
        /// The target receiving all surface input for the application.
        /// </summary>
        protected TouchTarget TouchTarget
        {
            get { return touchTarget; }
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public App1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        #region Initialization

        /// <summary>
        /// Moves and sizes the window to cover the input surface.
        /// </summary>
        private void SetWindowOnSurface()
        {
            System.Diagnostics.Debug.Assert(Window != null && Window.Handle != IntPtr.Zero,
                "Window initialization must be complete before SetWindowOnSurface is called");
            if (Window == null || Window.Handle == IntPtr.Zero)
                return;

            // Get the window sized right.
            Program.InitializeWindow(Window);
            // Set the graphics device buffers.
            graphics.PreferredBackBufferWidth = Program.WindowSize.Width;
            graphics.PreferredBackBufferHeight = Program.WindowSize.Height;
            graphics.ApplyChanges();
            // Make sure the window is in the right location.
            Program.PositionWindow();
        }

        /// <summary>
        /// Initializes the surface input system. This should be called after any window
        /// initialization is done, and should only be called once.
        /// </summary>
        private void InitializeSurfaceInput()
        {
            System.Diagnostics.Debug.Assert(Window != null && Window.Handle != IntPtr.Zero,
                "Window initialization must be complete before InitializeSurfaceInput is called");
            if (Window == null || Window.Handle == IntPtr.Zero)
                return;
            System.Diagnostics.Debug.Assert(touchTarget == null,
                "Surface input already initialized");
            if (touchTarget != null)
                return;

            // Create a target for surface input.
            touchTarget = new TouchTarget(Window.Handle, EventThreadChoice.OnBackgroundThread);
            touchTarget.EnableInput();
        }

        #endregion

        #region Overridden Game Methods

        /// <summary>
        /// Allows the app to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here   
            Console.WriteLine("Initialisation du jeu");

            loadConfig();

            Background bBackground = new Background(new Vector2(0, 0), Background.Track.Menu);
            DrawableObjects.Add("background", bBackground);

            IsMouseVisible = true; // easier for debugging not to "lose" mouse
            SetWindowOnSurface();
            InitializeSurfaceInput();

            // Set the application's orientation based on the orientation at launch
            currentOrientation = ApplicationServices.InitialOrientation;

            // Subscribe to surface window availability events
            ApplicationServices.WindowInteractive += OnWindowInteractive;
            ApplicationServices.WindowNoninteractive += OnWindowNoninteractive;
            ApplicationServices.WindowUnavailable += OnWindowUnavailable;

            // Setup the UI to transform if the UI is rotated.
            // Create a rotation matrix to orient the screen so it is viewed correctly
            // when the user orientation is 180 degress different.
            Matrix inverted = Matrix.CreateRotationZ(MathHelper.ToRadians(180)) *
                       Matrix.CreateTranslation(graphics.GraphicsDevice.Viewport.Width,
                                                 graphics.GraphicsDevice.Viewport.Height,
                                                 0);

            if (currentOrientation == UserOrientation.Top)
            {
                screenTransform = inverted;
            }

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per app and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            // TODO: use this.Content to load your application content here

            foreach (IDrawableObject DObj in DrawableObjects.Values)
            {
                DObj.LoadContent(this.Content);
            }

            spriteFont = Content.Load<SpriteFont>("SpriteFont1");
        }

        /// <summary>
        /// UnloadContent will be called once per app and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the app to run logic such as updating the world,
        /// checking for collisions, gathering input and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            if (ApplicationServices.WindowAvailability != WindowAvailability.Unavailable)
            {
                if (ApplicationServices.WindowAvailability == WindowAvailability.Interactive)
                {
                    // TODO: Process touches, 
                    // use the following code to get the state of all current touch points.
                    // ReadOnlyTouchPointCollection touches = touchTarget.GetState();

                    ReadOnlyTouchPointCollection touches = touchTarget.GetState();
                    if (CurrentState == GameState.play)
                    {
                        refreshGameState();

                        List<String> detectedTags = new List<string>();

                        foreach (var t in touches) // cr�ation de la liste des tags pos�s
                        {
                            if (t.IsTagRecognized)
                            {
                                detectedTags.Add(t.Tag.Value.ToString());
                                if (!TagValues.ContainsKey(t.Tag.Value.ToString()))
                                {
                                    TagValues.Add(t.Tag.Value.ToString(), t);
                                    WebRequest wrGETURL = WebRequest.Create(serverURL + "put_tag/"
                                        + t.Tag.Value.ToString() + "/" + t.CenterX / 16 + "/" + t.CenterY / 16 + "/" + t.Orientation);
                                }
                            }
                            else if (! t.IsFingerRecognized) // ni un doigt, ni un tag : c'est donc un blob
                            {
                                //TODO envoyer coordonn�es du blob au serveur
                            }
                        }

                        List<string> tagsToDelete = new List<string>();

                        foreach (String tagV in TagValues.Keys) // et on supprime ceux qui ont �t� enlev�s
                        {
                            if (!detectedTags.Contains(tagV))
                            {
                                tagsToDelete.Add(tagV);
                                WebRequest wrGETURL = WebRequest.Create(serverURL + "removed_tag/"
                                    + tagV);
                            }
                        }

                        foreach (String tagV in tagsToDelete)
                        {
                            TagValues.Remove(tagV);
                        }

                    }
                    else if (CurrentState == GameState.menu)
                    {
                        bool isTrackSelected = false;
                        foreach (var t in touches)
                        {
                            //spriteBatch.DrawString(spriteFont, "x : " + t.X + " y : " + t.Y, new Vector2(t.X, t.Y), Color.Black);
                            if (t.Y > 200 && t.Y < 400) //Premi�re ligne
                            {
                                if (t.X > 100 && t.X < 600) // 1ere colonne
                                {
                                    selectedTrack = Background.Track.Classic;
                                    isTrackSelected = true;
                                }
                                else if (t.X > 700 && t.X < 1200)
                                {
                                    selectedTrack = Background.Track.RainbowRoad;
                                    isTrackSelected = true;
                                }
                                else if (t.X > 1300 && t.X < 1850)
                                {
                                    selectedTrack = Background.Track.City;
                                    isTrackSelected = true;
                                }
                            }
                            else if (t.Y > 600 && t.Y < 875) //deuxi�me ligne
                            {
                                if (t.X > 100 && t.X < 600) // 1ere colonne
                                {
                                }
                                else if (t.X > 700 && t.X < 1200)
                                {
                                }
                                else if (t.X > 1300 && t.X < 1850)
                                {
                                }
                            }
                        }

                        if (isTrackSelected)
                        {
                            CurrentState = GameState.play;
                            Background trackBackground = new Background(new Vector2(0, 0), selectedTrack);
                            trackBackground.LoadContent(this.Content);
                            DrawableObjects["background"] = trackBackground;
                            //TODO envoyer circuit choisi au serveur. (/track/nomDuCircuit)
                        }
                    }

                }

                // TODO: Add your update logic here

            }

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the app should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            if (!applicationLoadCompleteSignalled)
            {
                // Dismiss the loading screen now that we are starting to draw
                ApplicationServices.SignalApplicationLoadComplete();
                applicationLoadCompleteSignalled = true;
            }

            //TODO: Rotate the UI based on the value of screenTransform here if desired

            GraphicsDevice.Clear(backgroundColor);

            //TODO: Add your drawing code here            

            spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend);

            //Console.WriteLine(gameTime.ElapsedGameTime);


            foreach (IDrawableObject DObj in DrawableObjects.Values)
            {
                DObj.Draw(this.spriteBatch);
                if (DObj is IMovableObject)
                {
                    //Console.WriteLine("==========================\nDraw object at " + ((IMovableObject)DObj).getPosition().Y);
                }
            }

            foreach (RectangleOverlay r in rects)
            {
                r.Draw(this.spriteBatch);
                //Console.WriteLine("drawing rect at : " + r.dummyRectangle.Center);
            }

            spriteBatch.End();

            //TODO: Avoid any expensive logic if application is neither active nor previewed

            base.Draw(gameTime);
        }

        #endregion

        #region Application Event Handlers

        /// <summary>
        /// This is called when the user can interact with the application's window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWindowInteractive(object sender, EventArgs e)
        {
            //TODO: Enable audio, animations here

            //TODO: Optionally enable raw image here
        }

        /// <summary>
        /// This is called when the user can see but not interact with the application's window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWindowNoninteractive(object sender, EventArgs e)
        {
            //TODO: Disable audio here if it is enabled

            //TODO: Optionally enable animations here
        }

        /// <summary>
        /// This is called when the application's window is not visible or interactive.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWindowUnavailable(object sender, EventArgs e)
        {
            //TODO: Disable audio, animations here

            //TODO: Disable raw image if it's enabled
        }

        #endregion

        #region IDisposable

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Release managed resources.
                IDisposable graphicsDispose = graphics as IDisposable;
                if (graphicsDispose != null)
                {
                    graphicsDispose.Dispose();
                }
                if (touchTarget != null)
                {
                    touchTarget.Dispose();
                    touchTarget = null;
                }
            }

            // Release unmanaged Resources.

            //redir.Dispose();

            // Set large objects to null to facilitate garbage collection.

            base.Dispose(disposing);
        }

        #endregion

        public void refreshGameState()
        {
            int scale = 16; // echelle m�tres * scale -> pixels

            using (var w = new WebClient())
            {
                var json_data = string.Empty;
                // attempt to download JSON data as a string
                try
                {
                    json_data = w.DownloadString(serverURL + "state");

                    JObject o = JObject.Parse(json_data);

                    List<string> objectsIdToKeep = new List<string>();
                    objectsIdToKeep.Add("background");

                    try
                    {
                        // recuperation des joueurs
                        foreach (JObject player in o["joueurs"])
                        {

                            objectsIdToKeep.Add((string)player["pseudo"]);

                            if (DrawableObjects.Keys.Contains((string)player["pseudo"]))
                            {
                                //Console.WriteLine("Updating car position : " + player["pseudo"] + "( " + (int)player["position_x"] + ", " + (int)player["position_y"] + ")");
                                Car car = (Car)DrawableObjects[(string)player["pseudo"]];
                                car.setPosition((int)((float)player["position_x"] * scale), (int)((float)player["position_y"] * scale));
                                car.setRotation((float)player["angle"]);
                            }
                            else
                            {
                                //Console.WriteLine("Creating new car : " + pseudo);
                                Car.CColor CarColor;
                                switch ((string)player["color"])
                                {
                                    case "Blue":
                                        CarColor = Car.CColor.Blue;
                                        break;
                                    case "Yellow":
                                        CarColor = Car.CColor.Yellow;
                                        break;
                                    case "Green":
                                        CarColor = Car.CColor.Green;
                                        break;
                                    case "Red":
                                        CarColor = Car.CColor.Red;
                                        break;
                                    default:
                                        CarColor = Car.CColor.None;
                                        break;
                                }
                                if (CarColor != Car.CColor.None)
                                {
                                    Car car = new Car((string)player["pseudo"], CarColor);
                                    car.LoadContent(this.Content);
                                    car.setPosition((int)(((float)player["position_x"]) * scale), (int)(((float)player["position_y"]) * scale));
                                    car.setRotation((float)player["angle"]);
                                    DrawableObjects.Add((string)player["pseudo"], car);
                                    //Console.WriteLine("new Car : " + car.getPosition());
                                }
                            }
                        }
                    }
                    catch { }

                    //recuperation des feux de d�part    
                    try
                    {
                        foreach (JObject greenlights in o["starting_lights"])
                        {
                            objectsIdToKeep.Add("greenlights");

                            GreenLights gl;

                            if (DrawableObjects.Keys.Contains("greenlights"))
                            {
                                gl = (GreenLights)DrawableObjects["greenlights"];
                            }
                            else
                            {
                                gl = new GreenLights();
                                gl.LoadContent(this.Content);
                                DrawableObjects.Add("greenlights", gl);
                            }

                            switch ((string)greenlights["state"])
                            {
                                case "3":
                                    gl.currentState = GreenLights.GLState.R3;
                                    break;
                                case "2":
                                    gl.currentState = GreenLights.GLState.R2;
                                    break;
                                case "1":
                                    gl.currentState = GreenLights.GLState.R1;
                                    break;
                                default:
                                    gl.currentState = GreenLights.GLState.GO;
                                    break;
                            }
                        }
                    }
                    catch { }

                    if (debug)
                    {
                        try
                        {
                            foreach (JObject prop in o["props"])
                            {
                                
                                int width = ((int)prop["size"][0] * scale);
                                int h = ((int)prop["size"][1] * scale);
                                int x = (int)prop["position"][0] * scale - width/2;
                                int y = (int)prop["position"][1] * scale - h/2;
                                float angle = (float)prop["angle"];

                                RectangleOverlay r = new RectangleOverlay(new Rectangle(x, y, width, h), Color.White, this, angle);
                                r.Initialize();
                                r.LoadContent();
                                rects.Add(r);
                                //Console.WriteLine("newRect : " + x + ", " + y);
                            }

                            debug = false;
                        }
                        catch { }
                    }

                    // suppression des objets qui n'existent plus

                    List<String> objectsToRemove = new List<string>();
                    foreach (string id in DrawableObjects.Keys)
                    {
                        if (!objectsIdToKeep.Contains(id))
                        {
                            objectsToRemove.Add(id);
                        }
                    }

                    foreach (string id in objectsToRemove)
                    {
                        DrawableObjects.Remove(id);
                    }


                    /*Console.WriteLine("============================Start refresh============================");
                    while (reader.Read())
                    {
                        if (reader.Value != null)
                            Console.WriteLine("Token: {0}, Value: {1}", reader.TokenType, reader.Value);
                        else
                            Console.WriteLine("Token: {0}", reader.TokenType);
                    }
                    Console.WriteLine("============================END====================================");*/

                }
                catch (Exception e) { Console.WriteLine(e.Message); }


            }
        }

        public void loadConfig()
        {
            try
            {
                using (StreamReader sr = new StreamReader("config.txt"))
                {
                    String line = sr.ReadToEnd();
                    JObject o = JObject.Parse(line);
                    serverURL = (string)o["server-url"];
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("The config file could not be read:");
                Console.WriteLine(e.Message);
            }
        }
    }
}
