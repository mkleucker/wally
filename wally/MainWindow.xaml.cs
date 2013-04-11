﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using System.IO;
using System.Globalization;
using System.Diagnostics;
using System.Threading;
using wally;
using System.IO.MemoryMappedFiles;
using System.Windows.Media.Animation;
using System.Timers;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Threading;


namespace wally
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private static int Runs = 0;

        //Width and Height of our drawing output
        private const float TargetWidth = 3412.0f;
        private const float TargetHeight = 480.0f;

        //Width and Height of Colorpalette (Change here if necessary!!)
        private float bucketsWidth = 150;
        private float bucketsHeight = 675;

        //Line that is drawn by right hand of the user
        // private Polyline myPolyline;
        // private Polyline currentLine;

        private ArrayList playerCanvases;

        private ArrayList players;

        private bool PaintingTimeOver = false;

        private Canvas canvas1;
        private Canvas canvas2;
        private Canvas canvas3;
        private Canvas canvas4;

        private Player player1;
        private Player player2;
        private Player player3;
        private Player player4;

        //private int currentStroke = 2; //1 = thick 2 = thin

        //private ArrayList myPonyLines;

        //Thickness of drawn joint lines and of body center elipse
        private const double JointThickness = 3;
        private const double BodyCenterThickness = 10;
        private const double ClipBoundsThickness = 10;
        //Brush malt Punkte/Ellipsen, ist mehr ein Füllwerkzeug, Pen malt Linien und Verbindungen
        private readonly Brush centerPointBrush = Brushes.Blue; //to draw centerpoint
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68)); //to draw tracked joints
        private readonly Brush inferredJointBrush = Brushes.Yellow; //to draw inferred joints
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6); // to draw tracked bones
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1); // to draw inferred bones

        private ArrayList sensors;
        private int DeviceCount;

        private DrawingGroup drawingGroup;

        private DrawingImage imageSource; //draw image that we will display


        ///// <summary>
        ///// Bitmap that will hold color information
        ///// </summary>
        private WriteableBitmap shadowBitmap;

        private BitmapImage canImg;
        private BitmapImage paintingColorsImg;



        // Using Two Kinects
        private ArrayList processes;


        // INTERNAL DATA STRUCTURE

        // Mutex
        static long MemoryMappedFileCapacitySkeleton = 2255;
        static long MemoryMappedFileCapacityMask = 307200;
        private byte[] mmf_result;
        private byte[] mmf_mask;

        private MemoryMappedFile[,] skelFiles;
        private MemoryMappedViewAccessor[,] skelAccess;

        private ArrayList skelData;

        static Mutex maskMutex;
        private MemoryMappedFile[] maskFiles;
        private MemoryMappedViewAccessor[] maskAccess;
        private ArrayList maskData;

        public MainWindow()
        {
            InitializeComponent();

            //this.WindowStyle = WindowStyle.None;
            //this.WindowState = WindowState.Maximized;
            this.Cursor = System.Windows.Input.Cursors.None;
        }


        //Execute startup tasks here
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            //int size = ;
            mmf_result = new byte[MemoryMappedFileCapacitySkeleton];
            mmf_mask = new byte[MemoryMappedFileCapacityMask];

            //init players, max 4
            this.players = new ArrayList();
            this.player1 = new Player();
            this.player2 = new Player();
            this.player3 = new Player();
            this.player4 = new Player();
            this.players.Add(player1);
            this.players.Add(player2);
            this.players.Add(player3);
            this.players.Add(player4);


            this.DeviceCount = KinectSensor.KinectSensors.Count;

            //Each player gets own canvas, max 4
            this.playerCanvases = new ArrayList();
            this.canvas1 = new Canvas();
            this.canvas2 = new Canvas();
            this.canvas3 = new Canvas();
            this.canvas4 = new Canvas();
            this.playerCanvases.Add(canvas1);
            this.playerCanvases.Add(canvas2);
            this.playerCanvases.Add(canvas3);
            this.playerCanvases.Add(canvas4);
            myGrid.Children.Add(canvas1);
            myGrid.Children.Add(canvas2);
            myGrid.Children.Add(canvas3);
            myGrid.Children.Add(canvas4);



            // Dispatcher.Invoke(DispatcherPriority.Send,
            //               new Action(PaintingTimer));

            this.drawingGroup = new DrawingGroup(); //we will use for drawing
            this.imageSource = new DrawingImage(this.drawingGroup); //imagesource we can use in our image control
            MyImage.Source = this.imageSource; //display the drawing to use our image control

            // Look through all sensors and start the first connected one.
            this.sensors = new ArrayList();
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                //Status should e.g. not be "Initializing" or "NotPowered"
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensors.Add(potentialSensor);
                }
            }


            var mappedfileThread = new Thread(MemoryMapData);
            mappedfileThread.SetApartmentState(ApartmentState.STA);
            mappedfileThread.Start();

            /// MUTEX Stuff
            this.processes = new System.Collections.ArrayList();
            int i = 0;
            foreach (KinectSensor sensor in this.sensors)
            {
                Process p = new System.Diagnostics.Process();
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.FileName
                  = Environment.CurrentDirectory + "\\ConsoleApplication1.exe";
                p.StartInfo.WindowStyle
                  = System.Diagnostics.ProcessWindowStyle.Normal;
                p.StartInfo.Arguments = sensor.UniqueKinectId + " " + i;
                p.Start();
                this.processes.Add(p);
                i++;
            }

            this.windowSetUp();

            //Init Polyline
            ////myPolyline = new Polyline();
            ////myPolyline.Stroke = System.Windows.Media.Brushes.White;
            ////myPolyline.StrokeThickness = 2;
            ////myPolyline.FillRule = FillRule.EvenOdd;
            ////myPonyLines.Add(myPolyline);
            ////currentLine = (Polyline)myPonyLines[myPonyLines.Count - 1];
            ////myCanvas.Children.Add((Polyline)myPonyLines[myPonyLines.Count - 1]);


            this.shadowBitmap = new WriteableBitmap(320, 240, 96.0, 96.0, PixelFormats.Bgra32, null);

            this.canImg = new BitmapImage(new Uri("Resources/Can.png", UriKind.Relative));
            this.paintingColorsImg = new BitmapImage(new Uri("Resources/paintingColorsImg.png", UriKind.Relative));

        }

        /// <summary>
        /// Set Correct Sizes to Window
        /// </summary>
        private void windowSetUp()
        {

            double screenWidth = System.Windows.SystemParameters.VirtualScreenWidth;
            double screenHeight = System.Windows.SystemParameters.VirtualScreenHeight;

            this.Width = 3412;
            this.Height = 480;


            this.Background = new RadialGradientBrush(Color.FromRgb(100, 100, 100), Color.FromRgb(50, 50, 50));

            this.WindowStyle = WindowStyle.None;
            this.WindowState = WindowState.Maximized;
            this.Cursor = System.Windows.Input.Cursors.None;
        }

        private void PaintingTimer()
        {
            CountDownClock(60, TimeSpan.FromSeconds(1), cur => myTimer.Text = cur.ToString());
        }

        ///// <summary>
        ///// Timer counting down 60 seconds
        ///// </summary>
        private void CountDownClock(int count, TimeSpan interval, Action<int> ts)
        {
            var dispatchTimer = new System.Windows.Threading.DispatcherTimer();
            dispatchTimer.Interval = interval;
            dispatchTimer.Tick += (_, a) =>
            {
                if (count-- == 0)
                {
                    PaintingTimeOver = true;
                    dispatchTimer.Stop();
                }
                else
                {
                    ts(count);
                }
            };
            ts(count);
            dispatchTimer.Start();

            //String timerText = timerValue.ToString();
            //TextBlock myTimer = new TextBlock();
            //myTimer.Height = 50;
            //myTimer.Width = 200;
            //myTimer.Text = timerText;
            //myTimer.Foreground = new SolidColorBrush(Colors.Black);
            //myCanvas.Children.Add(myTimer);
            //timerValue--;
        }

        /// <summary>
        /// Saves the drawn lines as a png-File to improve performance
        /// </summary>
        private void SaveLinesAsImage()
        {

            foreach (Player player in players)
            {
                if (player.getState())
                {
                    RenderTargetBitmap targetBitmap = new RenderTargetBitmap((int)player.getMyCanvas().ActualWidth,
                                     (int)player.getMyCanvas().ActualHeight,
                                     96d, 96d,
                                     PixelFormats.Default);

                    targetBitmap.Render(player.getMyCanvas());

                    // create a png bitmap encoder which knows how to save a .png file
                    BitmapEncoder encoder = new PngBitmapEncoder();

                    // create frame from the writable bitmap and add to encoder
                    encoder.Frames.Add(BitmapFrame.Create(targetBitmap));

                    //only filename construction
                    string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);
                    string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures); //Eigene Dateien->Bilder
                    string path = System.IO.Path.Combine
                        (myPhotos, "KinectSnapshot-" + time + "player" + player.GetHashCode() + ".png");
                    player.setLastPngImg(path);

                    // write the new file to disk
                    try
                    {
                        using (FileStream fs = new FileStream(path, FileMode.Create))
                        {
                            encoder.Save(fs);
                        }
                    }
                    catch (IOException)
                    {
                    }
                }

            }
        }


        /// <summary>
        /// Manage the selection of different colors
        /// </summary>
        private void ColorSelection()
        {
            foreach (Player player in players)
            {

                if (player.getState())
                {
                    Skeleton skel = player.getSkeleton();
                    double leftHandY = stretchPointToScreen(SkeletonPointToScreen(skel.Joints[JointType.HandLeft].Position)).Y;
                    double leftHandX = stretchPointToScreen(SkeletonPointToScreen(skel.Joints[JointType.HandLeft].Position)).X;
                    Point playerPosition = stretchPointToScreen(this.SkeletonPointToScreen(skel.Position),
                        player.getPlayersKinectId());
                    double xCoord1 = playerPosition.X - 100 * 3;
                    double xCoord2 = playerPosition.X - 100 * 3 - bucketsWidth;
                    double yCoordSteps = bucketsHeight / 6; //amount of colors used, currently 6 
                    double yCoordStart = 0;

                    if (leftHandY >= yCoordStart && leftHandY < yCoordStart + yCoordSteps
                        && leftHandX < xCoord1 && leftHandX > xCoord2
                        && player.getCurrentColor() != System.Windows.Media.Brushes.White)
                    {
                        DrawLine(System.Windows.Media.Brushes.White, player.getCurrentStroke(), player);
                    }
                    if (leftHandY >= yCoordStart + yCoordSteps && leftHandY < yCoordStart + 2 * yCoordSteps
                        && leftHandX < xCoord1 && leftHandX > xCoord2 &&
                        player.getCurrentColor() != System.Windows.Media.Brushes.Blue)
                    {
                        DrawLine(System.Windows.Media.Brushes.Blue, player.getCurrentStroke(), player);
                    }
                    if (leftHandY >= yCoordStart + 2 * yCoordSteps && leftHandY < yCoordStart + 3 * yCoordSteps
                        && leftHandX < xCoord1 && leftHandX > xCoord2 &&
                        player.getCurrentColor() != System.Windows.Media.Brushes.Yellow)
                    {
                        DrawLine(System.Windows.Media.Brushes.Yellow, player.getCurrentStroke(), player);
                    }
                    if (leftHandY >= yCoordStart + 3 * yCoordSteps && leftHandY < yCoordStart + 4 * yCoordSteps
                        && leftHandX < xCoord1 && leftHandX > xCoord2 &&
                        player.getCurrentColor() != System.Windows.Media.Brushes.Green)
                    {
                        DrawLine(System.Windows.Media.Brushes.Green, player.getCurrentStroke(), player);
                    }
                    if (leftHandY >= yCoordStart + 4 * yCoordSteps && leftHandY < yCoordStart + 5 * yCoordSteps
                        && leftHandX < xCoord1 && leftHandX > xCoord2 &&
                        player.getCurrentColor() != System.Windows.Media.Brushes.Red)
                    {
                        DrawLine(System.Windows.Media.Brushes.Red, player.getCurrentStroke(), player);
                    }
                    if (leftHandY >= yCoordStart + 5 * yCoordSteps && leftHandY < yCoordStart + 6 * yCoordSteps
                        && leftHandX < xCoord1 && leftHandX > xCoord2 &&
                        player.getCurrentColor() != System.Windows.Media.Brushes.Black)
                    {
                        DrawLine(System.Windows.Media.Brushes.Black, player.getCurrentStroke(), player);
                    }
                }
            }
        }

        private void Painting()
        {
            foreach (Player player in players)
            {
                if (player.getState())
                {
                    Skeleton skel = player.getSkeleton();
                    Polyline myPolyline;
                    if (player.getCurrentLine() == null)
                    {
                        myPolyline = new Polyline();
                        myPolyline.StrokeThickness = 5;
                        player.addLine(myPolyline);
                        player.getMyCanvas().Children.Add(player.getCurrentLine());
                    }
                    else
                    {
                        myPolyline = player.getCurrentLine();
                    }

                    myPolyline.Stroke = System.Windows.Media.Brushes.White;
                    myPolyline.StrokeThickness = 5;


                    if (skel.Position.Z > 0.7 && skel.Position.Z <= 2.0)
                    {

                        System.Windows.Point Point1 = this.SkeletonPointToScreen(skel.Joints[JointType.HandRight].Position);

                        Point1 = this.stretchPointToScreen(Point1, player.getPlayersKinectId());

                        //As long as the hand is nearer to the screen than the user's body -> painting 
                        //As soon as the hand is further away from the screen than the user's body  -> not painting
                        if (skel.Joints[JointType.HandRight].Position.Z > skel.Position.Z - 0.1 && player.getCurrentLine().Points.Count > 1)
                        {
                            DrawLine(player.getCurrentColor(), 5, player);
                        }

                        //When the hand is near to the screen (if-case) the line gets thicker

                        if (skel.Joints[JointType.HandRight].Position.Z > skel.Position.Z - 0.8
                            && skel.Joints[JointType.HandRight].Position.Z < skel.Position.Z - 0.3
                            && player.getCurrentLine().Points.Count > 1)
                        {
                            if (player.getCurrentStroke() == 5)
                            {
                                DrawLine(player.getCurrentColor(), 20, player);
                            }
                        }

                        //When the hand is further away from the screen (else if - case) the line gets thinner
                        else if (skel.Joints[JointType.HandRight].Position.Z > skel.Position.Z - 0.3
                            && player.getCurrentLine().Points.Count > 1)
                        {
                            if (player.getCurrentStroke() == 20)
                            {
                                DrawLine(player.getCurrentColor(), 5, player);
                            }
                        }


                        if (skel.Joints[JointType.HandRight].Position.Z < skel.Position.Z - 0.1)
                        {
                            player.addPointToCurrentLine(Point1);

                        }

                        // Changing of stroke color with the left hand
                        ColorSelection();
                    }
                }
            }
        }

        private void DrawSkeleton()
        {
            using (DrawingContext dc = this.drawingGroup.Open())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, this.Width, this.Height));

                foreach (Player player in this.players)
                {
                    if (player.getState())
                    {
                        Skeleton skel = player.getSkeleton();

                        Point p = this.SkeletonPointToScreen(skel.Joints[JointType.HandRight].Position);
                        Point playerPosition = stretchPointToScreen(this.SkeletonPointToScreen(skel.Position));

                        p = this.stretchPointToScreen(p, player.getPlayersKinectId());

                        dc.DrawImage(
                                this.canImg,
                                new Rect(p.X - 50, p.Y, 50, 50)
                            );
                        dc.DrawImage(
                                this.paintingColorsImg,
                                new Rect(playerPosition.X - 500, 0, bucketsWidth, bucketsHeight)
                            );
                    }
                }



            }
        }



        /// <summary>
        /// Creates a new line object.
        /// </summary>
        /// <param name="lineColor">Color for the new line</param>
        /// <param name="lineThickness">Thickness of the new line</param>
        private void DrawLine(System.Windows.Media.Brush lineColor, double lineThickness, Player player)
        {

            if (PaintingTimeOver)
            {
                SaveLinesAsImage();
                // Create new image and set source path
                Image image = new Image();
                image.Source = new BitmapImage(new Uri(player.getLastPngImg()));

                // Place image 
                image.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                image.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                image.Margin = new Thickness(0, 0, 0, 0); // origin
                player.getMyCanvas().Width = this.ActualWidth;
                player.getMyCanvas().Height = this.ActualHeight;
                player.getMyCanvas().Children.Add(image);

                for (int i = 0; i < player.getPonyCount() - 1; i++)
                {
                    ArrayList ponyLines = player.getPonyLines();
                    player.getMyCanvas().Children.Remove((Polyline)ponyLines[i]);
                }
                player.getPonyLines().Clear();

                Polyline newLine = new Polyline();
                newLine.Stroke = lineColor;
                newLine.StrokeThickness = lineThickness;
                player.addLine(newLine);
                player.getMyCanvas().Children.Add(player.getCurrentLine());
            }

            else
            {
                Polyline newLine = new Polyline();
                newLine.Stroke = lineColor;
                newLine.StrokeThickness = lineThickness;
                player.addLine(newLine);
                player.getMyCanvas().Children.Add(player.getCurrentLine());
            }
        }


        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            ColorImagePoint depthPoint = ((KinectSensor)this.sensors[0]).CoordinateMapper.MapSkeletonPointToColorPoint(skelpoint, ColorImageFormat.RgbResolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        private Point stretchPointToScreen(Point point)
        {
            return this.stretchPointToScreen(point, 0);
        }

        private Point stretchPointToScreen(Point point, int kinect)
        {
            Point screenPoint = new Point();
            int multiplicator = 4 / this.processes.Count;
            screenPoint.X = point.X * (this.Width / 640) * multiplicator;
            //Console.WriteLine(kinect + " : " + screenPoint.X);
            if (kinect > 0)
            {
                screenPoint.X += TargetWidth / (kinect + 1);
            }
            screenPoint.Y = point.Y * (this.Height / 480);
            return screenPoint;
        }


        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Terminates the child process and sensor communication.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                foreach (Process process in this.processes)
                {
                    process.Kill();
                }
                foreach (KinectSensor sensor in this.sensors)
                {
                    sensor.Stop();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error on Closing: " + ex.Message);
            }

        }


        /// <summary>
        /// Thread for MemoryMapConnection
        /// </summary>
        private void MemoryMapData()
        {


            var skeletonMutex = new Mutex(true, "skeletonmutex");
            skeletonMutex.ReleaseMutex();

            var maskMutex = new Mutex(true, "maskmutex");
            maskMutex.ReleaseMutex();

            // Create for each Kinect Sensor in the Child Processes  
            this.initMemoryFiles();

            // ****************
            // SKELETON-MAPPING
            // ***************


            bool empty = false;

            Stopwatch stopwatch = new Stopwatch();

            while (true)
            {

                stopwatch.Reset();
                stopwatch.Start();

                // Gather the skeletons
                this.skelData = new ArrayList();
                if (this.skelAccess != null)
                {

                    for (int i = 0; i < this.skelAccess.GetLength(0); i++) //i = process j= channel
                    {
                        for (int j = 0; j < 2; j++)
                        {
                            MemoryMappedViewAccessor accessor = this.skelAccess[i, j];
                            byte[] temp = new byte[mmf_result.Length];

                            skeletonMutex.WaitOne();

                            accessor.ReadArray<byte>(0, mmf_result, 0, mmf_result.Length);

                            Array.Copy(mmf_result, temp, mmf_result.Length);

                            empty = temp.All(B => B == default(Byte));

                            skeletonMutex.ReleaseMutex();


                            if (!empty)
                            {
                                try
                                {
                                    BinaryFormatter bf = new BinaryFormatter();
                                    MemoryStream ms = new MemoryStream(mmf_result);
                                    Skeleton skelNew = (Skeleton)bf.Deserialize(ms);

                                    this.skelData.Add(skelNew);
                                    bool playerRecognized = false;

                                    foreach (Player player in players)
                                    {
                                        if (player.getState())
                                        {
                                            if (skelNew.TrackingId == player.getSkeleton().TrackingId)
                                            {
                                                //old player recognized, create nothing
                                                player.setSkeleton(skelNew);
                                                playerRecognized = true;
                                            }

                                        }
                                    }
                                    if (!playerRecognized)
                                    {
                                        for (int k = 0; k < players.Count; k++)
                                        {
                                            if (((Player)players[k]).getState() == false)
                                            {
                                                //new player recognized
                                                ((Player)players[k]).activatePlayer(i, skelNew, null, (Canvas)this.playerCanvases[k]);
                                                break;
                                            }
                                        }



                                    }
                                }
                                catch (Exception e)
                                {

                                }

                            }




                        }

                    }


                }

                long trackDelay = stopwatch.ElapsedMilliseconds;
                // Get The Mask

                maskData.Clear();
                for (int i = 0; i < maskAccess.Length; i++)
                {
                    MemoryMappedViewAccessor reader = maskAccess[i];

                    byte[] temp = new byte[mmf_mask.Length];

                    maskMutex.WaitOne();

                    reader.ReadArray<byte>(0, mmf_mask, 0, mmf_mask.Length);

                    Array.Copy(mmf_mask, temp, mmf_mask.Length);

                    maskMutex.ReleaseMutex();

                    maskData.Add(temp);


                }


                if (stopwatch.ElapsedMilliseconds < 33)
                {
                    Thread.Sleep((int)(33.0 - stopwatch.ElapsedMilliseconds));
                }

                // After all: REPAINT TIME!!!
                Dispatcher.Invoke(DispatcherPriority.Send,
                             new Action(DrawTimer));
            }



        }

        /// <summary>
        /// Creates the Memory-Mapped Files and their Accessor's.
        /// </summary>
        private void initMemoryFiles()
        {
            int count = this.sensors.Count;

            this.skelFiles = new MemoryMappedFile[count, 2];
            this.skelAccess = new MemoryMappedViewAccessor[count, 2];

            this.maskFiles = new MemoryMappedFile[count];
            this.maskAccess = new MemoryMappedViewAccessor[count];
            this.maskData = new ArrayList();

            string filename;

            for (int p = 0; p < count; p++)
            {
                // Maximum of two Skeleton. So two Skeletonfiles
                for (int i = 0; i < 2; i++)
                {
                    filename = "skel-" + p + "-" + i;
                    Console.WriteLine("Created : " + filename);
                    MemoryMappedFile skelFileTmp = MemoryMappedFile.CreateNew(filename, MemoryMappedFileCapacitySkeleton);

                    this.skelFiles[p, i] = skelFileTmp;
                    this.skelAccess[p, i] = skelFileTmp.CreateViewAccessor();
                }

                filename = "mask-" + p;
                Console.WriteLine("Created : " + filename);
                this.maskFiles[p] = MemoryMappedFile.CreateNew(filename, MemoryMappedFileCapacityMask);
                this.maskAccess[p] = this.maskFiles[p].CreateViewAccessor();

                // ... more Memory Files for other Channels.
            }
        }



        private void DrawTimer()
        {


            this.DrawMask();
            this.Painting();
            this.DrawSkeleton();
        }

        private void DrawMask()
        {



            double dpi = 96;
            int origWidth = 320;
            int width = origWidth * this.maskData.Count;
            int height = 240;
            int stride = (width * PixelFormats.Bgra32.BitsPerPixel) / 8;
            byte[] pixelData = new byte[height * stride];

            // Prepare MaskArray


            byte[] incomingMask = (byte[])this.maskData[0];
            if (this.maskData.Count > 1)
            {
                incomingMask = new byte[incomingMask.Length * this.maskData.Count];

                int line = 0;
                for (int i = 0; i < incomingMask.Length; i++)
                {

                    int currentLine = i % width;
                    int position = line * origWidth + (currentLine % origWidth);
                    if (currentLine >= origWidth)
                    {
                        incomingMask[i] = ((byte[])this.maskData[1])[position];
                    }
                    else
                    {
                        incomingMask[i] = ((byte[])this.maskData[0])[position];
                    }
                    if (i > 0 && i % width == 0)
                    {
                        line++;
                    }
                }
            }

            int j = 0;
            for (int i = 0; i < height * stride; i += (PixelFormats.Bgra32.BitsPerPixel / 8))
            {
                pixelData[i] = (byte)255;  // BLUE
                pixelData[i + 1] = (byte)255; // GREEN
                pixelData[i + 2] = (byte)255; // RED
                if (incomingMask[j] != (byte)0)
                {
                    pixelData[i + 3] = (byte)100; // ALPHA
                }
                else
                {
                    pixelData[i + 3] = (byte)0;
                }

                j++;
            }



            this.MaskedColor.Source = BitmapSource.Create(
                width,
                height,
                dpi,
                dpi,
                PixelFormats.Bgra32,
                null,
                pixelData,
                stride);

        }



        /// <summary>
        /// Keyhandler to kill the application. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void KeyDownEventHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                foreach (Process p in this.processes)
                {
                    p.Kill();
                }
                Application.Current.Shutdown();
                Environment.Exit(0);
            }
        }


    }
}