using System;
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
        private const float RenderWidth = 640.0f;
        private const float RenderHeight = 480.0f;

        //Line that is drawn by right hand of the user
        private Polyline myPolyline;
        private Polyline currentLine;

        private bool PaintingTimeOver = false;

        private String lastPngImage;

        private int currentStroke = 2; //1 = thick 2 = thin

        private bool colorChangingMode = false;

        private ArrayList myPonyLines;

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

        private DrawingGroup drawingGroup; //for skeleton rendering output

        private DrawingImage imageSource; //draw image that we will display


        //########## OLD Greenscreen Stuff
        //###########################
        ///// <summary>
        ///// Format we will use for the depth stream
        ///// </summary>
        //private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution320x240Fps30;

        ///// <summary>
        ///// Format we will use for the color stream
        ///// </summary>
        //private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;

        ///// <summary>
        ///// Bitmap that will hold color information
        ///// </summary>
        private WriteableBitmap shadowBitmap;

        ///// <summary>
        ///// Bitmap that will hold opacity mask information
        ///// </summary>
        //private WriteableBitmap playerOpacityMaskImage = null;

        ///// <summary>
        ///// Intermediate storage for the depth data received from the sensor
        ///// </summary>
        //private DepthImagePixel[] depthPixels;

        ///// <summary>
        ///// Intermediate storage for the color data received from the camera
        ///// </summary>
        //private byte[] colorPixels;

        ///// <summary>
        ///// Intermediate storage for the green screen opacity mask
        ///// </summary>
        //private int[] greenScreenPixelData;

        ///// <summary>
        ///// Intermediate storage for the depth to color mapping
        ///// </summary>
        //private ColorImagePoint[] colorCoordinates;

        ///// <summary>
        ///// Inverse scaling factor between color and depth
        ///// </summary>
        //private int colorToDepthDivisor;

        ///// <summary>
        ///// Width of the depth image
        ///// </summary>
        //private int depthWidth;

        ///// <summary>
        ///// Height of the depth image
        ///// </summary>
        //private int depthHeight;

        ///// <summary>
        ///// Indicates opaque in an opacity mask
        ///// </summary>
        //private int opaquePixelValue = -1;


        private BitmapImage canImg;



        // Using Two Kinects
        private ArrayList processes;

        private Skeleton skel;


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

            this.myPonyLines = new ArrayList();

            this.DeviceCount = KinectSensor.KinectSensors.Count;


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
            myPolyline = new Polyline();
            myPolyline.Stroke = System.Windows.Media.Brushes.White;
            myPolyline.StrokeThickness = 2;
            myPolyline.FillRule = FillRule.EvenOdd;
            myPonyLines.Add(myPolyline);
            currentLine = (Polyline)myPonyLines[myPonyLines.Count - 1];
            myCanvas.Children.Add((Polyline)myPonyLines[myPonyLines.Count - 1]);


            this.shadowBitmap = new WriteableBitmap(320, 240, 96.0, 96.0, PixelFormats.Bgra32, null);

            this.canImg = new BitmapImage(new Uri("Resources/Can.png", UriKind.Relative));

        }

        /// <summary>
        /// Set Correct Sizes to Window
        /// </summary>
        private void windowSetUp()
        {

            double screenWidth = System.Windows.SystemParameters.VirtualScreenWidth;
            double screenHeight = System.Windows.SystemParameters.VirtualScreenHeight;

            this.Width = screenWidth;
            this.Height = screenHeight;

            this.Background = new RadialGradientBrush(Color.FromRgb(100, 100, 100), Color.FromRgb(50, 50, 50));

            this.myGrid.Width = screenWidth;
            this.myGrid.Height = screenHeight;
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

            RenderTargetBitmap targetBitmap = new RenderTargetBitmap((int)myCanvas.ActualWidth,
                             (int)myCanvas.ActualHeight,
                             96d, 96d,
                             PixelFormats.Default);

            targetBitmap.Render(myCanvas);

            // create a png bitmap encoder which knows how to save a .png file
            BitmapEncoder encoder = new PngBitmapEncoder();

            // create frame from the writable bitmap and add to encoder
            encoder.Frames.Add(BitmapFrame.Create(targetBitmap));

            //only filename construction
            string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);
            string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures); //Eigene Dateien->Bilder
            string path = System.IO.Path.Combine(myPhotos, "KinectSnapshot-" + time + ".png");
            lastPngImage = path;

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

        /// <summary>
        /// Animate the "Paintbuckets" to stay near the user
        /// </summary>
        /// <param name="newPosition">Position of the Skeleton, to attach Paintbuckets to it </param>
        private void AnimateColors(Point newPosition)
        {
            // Create a new animation with start and end values, duration and
            // an optional completion handler.
            DoubleAnimation da = new DoubleAnimation();
            da.From = 0;
            da.To = (newPosition.X - 200);
            // da.Duration = new Duration(TimeSpan.FromSeconds(1));
            //da.Completed += new EventHandler(animationCompleted); // easy syntax without parameters
            da.Completed += (sender, eArgs) => animationCompleted(ColorCanvas, newPosition); // syntax with parameters

            // Start animating the x-translation 
            TranslateTransform rt = new TranslateTransform();
            ColorCanvas.RenderTransform = rt;
            rt.BeginAnimation(TranslateTransform.XProperty, da);
        }

        /// <summary>
        /// EventHandler for the ColorBucket-Animation 
        /// </summary>
        /// <param name="myCanvas">Canvas that is animated, containing the paintbucket ellipses</param>
        /// <param name="newPosition">Position of the Skeleton, to attach Paintbuckets to it </param>
        private void animationCompleted(Canvas myCanvas, Point newPosition)
        {
            myCanvas.Margin = new Thickness((newPosition.X - 200), 0, 0, 0);
        }

        /// <summary>
        /// Manage the selection of different colors
        /// </summary>
        private void ColorSelection(Skeleton skel)
        {

            double leftHandY = SkeletonPointToScreen(skel.Joints[JointType.HandLeft].Position).Y;
            double leftHandX = SkeletonPointToScreen(skel.Joints[JointType.HandLeft].Position).X;
            double windowHeight = this.ActualHeight;
            double windowWidth = this.ActualWidth;
            double xCoordPart = windowWidth / 5; //Area for Colorselection
            double yCoordSteps = windowHeight / 4; //amount of colors used, currently 4 

            if (leftHandX < xCoordPart)
            {
                colorChangingMode = true;
            }
            else if (leftHandX >= xCoordPart)
            {
                colorChangingMode = false;
            }

            if (colorChangingMode)
            {

                if (leftHandY >= 3 * yCoordSteps && currentLine.Stroke != System.Windows.Media.Brushes.Blue)
                {
                    DrawLine(System.Windows.Media.Brushes.Blue, currentLine.StrokeThickness);
                }
                if (leftHandY >= 2 * yCoordSteps && leftHandY < 3 * yCoordSteps && currentLine.Stroke != System.Windows.Media.Brushes.Red)
                {
                    DrawLine(System.Windows.Media.Brushes.Red, currentLine.StrokeThickness);
                }
                if (leftHandY >= yCoordSteps && leftHandY < 2 * yCoordSteps && currentLine.Stroke != System.Windows.Media.Brushes.Yellow)
                {
                    DrawLine(System.Windows.Media.Brushes.Yellow, currentLine.StrokeThickness);
                }
                if (leftHandY >= 0 && leftHandY < yCoordSteps && currentLine.Stroke != System.Windows.Media.Brushes.White)
                {
                    DrawLine(System.Windows.Media.Brushes.White, currentLine.StrokeThickness);
                }
            }

        }

        private void Painting()
        {


            foreach (Skeleton skel in this.skelData)
            {

                if (skel.Position.Z > 0.7 && skel.Position.Z <= 2.0)
                {

                    // AnimateColors(this.SkeletonPointToScreen(skel.Position));

                    System.Windows.Point Point1 = this.SkeletonPointToScreen(skel.Joints[JointType.HandRight].Position);

                    Point1 = this.stretchPointToScreen(Point1);

                    //As long as the hand is nearer to the screen than the user's body -> painting 
                    //As soon as the hand is further away from the screen than the user's body  -> not painting
                    if (skel.Joints[JointType.HandRight].Position.Z > skel.Position.Z - 0.1 && currentLine.Points.Count > 1)
                    {
                        DrawLine(currentLine.Stroke, 5);
                    }

                    //When the hand is near to the screen (if-case) the line gets thicker

                    if (skel.Joints[JointType.HandRight].Position.Z > skel.Position.Z - 0.8
                        && skel.Joints[JointType.HandRight].Position.Z < skel.Position.Z - 0.3
                        && currentLine.Points.Count > 1)
                    {
                        if (currentStroke == 2)
                        {
                            DrawLine(currentLine.Stroke, 20);
                            currentStroke = 1;
                        }
                        else
                            currentLine.StrokeThickness = 20;

                    }

                    //When the hand is further away from the screen (else if - case) the line gets thinner
                    else if (skel.Joints[JointType.HandRight].Position.Z > skel.Position.Z - 0.3
                        && currentLine.Points.Count > 1)
                    {
                        if (currentStroke == 1)
                        {
                            DrawLine(currentLine.Stroke, 5);
                            currentStroke = 2;
                        }
                        else
                            currentLine.StrokeThickness = 5;

                    }


                    if (skel.Joints[JointType.HandRight].Position.Z < skel.Position.Z - 0.1)
                    {
                        currentLine.Points.Add(Point1);
                    }

                    // Changing of stroke color with the left hand
                    ColorSelection(skel);
                }
            }
        }

        private void DrawSkeleton()
        {
            using (DrawingContext dc = this.drawingGroup.Open())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                foreach (Skeleton skel in this.skelData)
                {
                    //this.DrawBonesAndJoints(skel, dc);
                    //dc.DrawEllipse(
                    //    this.centerPointBrush,
                    //    null,
                    //    this.SkeletonPointToScreen(skel.Joints[JointType.HandRight].Position),
                    //    BodyCenterThickness * skel.Joints[JointType.HandRight].Position.Z,
                    //    BodyCenterThickness * skel.Joints[JointType.HandRight].Position.Z);
                    Point p = this.SkeletonPointToScreen(skel.Joints[JointType.HandRight].Position);

                    p = this.stretchPointToScreen(p);
                    dc.DrawImage(
                            this.canImg,
                            new Rect(p.X - 50, p.Y, 50, 50)
                        );

                }

                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.Width, this.Height));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.HandRight);
            this.DrawBone(skeleton, drawingContext, JointType.HandRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Creates a new line object.
        /// </summary>
        /// <param name="lineColor">Color for the new line</param>
        /// <param name="lineThickness">Thickness of the new line</param>
        private void DrawLine(System.Windows.Media.Brush lineColor, double lineThickness)
        {

            //System.Console.WriteLine(myPonyLines.Count);
            if (PaintingTimeOver)
            {
                SaveLinesAsImage();
                // Create new image and set source path
                Image image = new Image();
                image.Source = new BitmapImage(new Uri(lastPngImage));

                // Place image 
                image.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                image.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                image.Margin = new Thickness(0, 0, 0, 0); // origin
                myCanvas.Children.Add(image); // MainGrid is defined in xaml

                for (int i = 0; i < myPonyLines.Count - 1; i++)
                {
                    myCanvas.Children.Remove((Polyline)myPonyLines[i]);
                }
                myPonyLines.Clear();

                myPolyline = new Polyline();
                myPolyline.Stroke = System.Windows.Media.Brushes.White;
                myPolyline.StrokeThickness = 2;
                myPolyline.FillRule = FillRule.EvenOdd;
                myPonyLines.Add(myPolyline);
                currentLine = (Polyline)myPonyLines[myPonyLines.Count - 1];
                myGrid.Children.Add((Polyline)myPonyLines[myPonyLines.Count - 1]);
            }

            else
            {

                currentLine = (Polyline)myPonyLines[myPonyLines.Count - 1];
                //Console.WriteLine("Current Line Points:" + currentLine.Points.Count);
                //Console.WriteLine("Number of Lines:" + myPonyLines.Count);
                Polyline newLine = new Polyline();
                newLine.Stroke = lineColor;
                newLine.StrokeThickness = lineThickness;
                myPonyLines.Add(newLine);
                myCanvas.Children.Add((Polyline)myPonyLines[myPonyLines.Count - 1]);
                currentLine = newLine;
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
            DepthImagePoint depthPoint = ((KinectSensor)this.sensors[0]).CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        private Point stretchPointToScreen(Point point)
        {
            Point screenPoint = new Point();
            screenPoint.X = point.X * this.ActualWidth / 640.0;
            screenPoint.Y = point.Y * this.ActualHeight / 480.0;
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

                    for (int i = 0; i < this.skelAccess.GetLength(0); i++)
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
                                }
                                catch (Exception e)
                                {

                                }

                            }
                            else
                            {
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

            this.DrawSkeleton();
            this.DrawMask();
            this.Painting();

        }

        private void DrawMask()
        {



            double dpi = 96;
            int width = 320;
            int height = 240;
            int stride = (width * PixelFormats.Bgra32.BitsPerPixel) / 8;
            byte[] pixelData = new byte[height * stride * this.maskData.Count];

            // Prepare MaskArray

            for (int mask = 0; mask < this.maskData.Count; mask++)
            {
                int factor = (mask > 0) ? mask * stride : 0;

                byte[] incomingMask = (byte[])this.maskData[mask];

                int j = 0;
                for (int i = 0; i < height * stride; i += (PixelFormats.Bgra32.BitsPerPixel / 8))
                {
                    pixelData[factor + i] = (byte)100;  // BLUE
                    pixelData[factor + i + 1] = (byte)100; // GREEN
                    pixelData[factor + i + 2] = (byte)100; // RED
                    if (incomingMask[j] != (byte)0)
                    {
                        pixelData[factor + i + 3] = (byte)100; // ALPHA
                    }
                    else
                    {
                        pixelData[factor + i + 3] = (byte)0;
                    }

                    j++;
                }
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
        /// <summary>
        /// Event handler for Kinect sensor's DepthFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        //private void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        //{
        //    // in the middle of shutting down, so nothing to do
        //    if (null == this.sensor)
        //    {
        //        return;
        //    }

        //    bool depthReceived = false;
        //    bool colorReceived = false;

        //    using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
        //    {
        //        if (null != depthFrame)
        //        {
        //            // Copy the pixel data from the image to a temporary array
        //            depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

        //            depthReceived = true;
        //        }
        //    }

        //    using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
        //    {
        //        if (null != colorFrame)
        //        {
        //            // Copy the pixel data from the image to a temporary array
        //            colorFrame.CopyPixelDataTo(this.colorPixels);

        //            colorReceived = true;
        //        }
        //    }

        //    // do our processing outside of the using block
        //    // so that we return resources to the kinect as soon as possible
        //    if (true == depthReceived)
        //    {
        //        this.sensor.CoordinateMapper.MapDepthFrameToColorFrame(
        //            DepthFormat,
        //            this.depthPixels,
        //            ColorFormat,
        //            this.colorCoordinates);

        //        Array.Clear(this.greenScreenPixelData, 0, this.greenScreenPixelData.Length);

        //        // loop over each row and column of the depth
        //        for (int y = 0; y < this.depthHeight; ++y)
        //        {
        //            for (int x = 0; x < this.depthWidth; ++x)
        //            {
        //                // calculate index into depth array
        //                int depthIndex = x + (y * this.depthWidth);

        //                DepthImagePixel depthPixel = this.depthPixels[depthIndex];

        //                int player = depthPixel.PlayerIndex;

        //                // if we're tracking a player for the current pixel, do green screen
        //                if (player > 0)
        //                {
        //                    // retrieve the depth to color mapping for the current depth pixel
        //                    ColorImagePoint colorImagePoint = this.colorCoordinates[depthIndex];

        //                    // scale color coordinates to depth resolution
        //                    int colorInDepthX = colorImagePoint.X / this.colorToDepthDivisor;
        //                    int colorInDepthY = colorImagePoint.Y / this.colorToDepthDivisor;

        //                    // make sure the depth pixel maps to a valid point in color space
        //                    // check y > 0 and y < depthHeight to make sure we don't write outside of the array
        //                    // check x > 0 instead of >= 0 since to fill gaps we set opaque 
        // current pixel plus the one to the left
        //                    // because of how the sensor works it is more correct to do it this way than to set to the right
        //                    if (colorInDepthX > 0 && colorInDepthX < this.depthWidth && colorInDepthY >= 0 && colorInDepthY < this.depthHeight)
        //                    {
        //                        // calculate index into the green screen pixel array
        //                        int greenScreenIndex = colorInDepthX + (colorInDepthY * this.depthWidth);

        //                        // set opaque
        //                        this.greenScreenPixelData[greenScreenIndex] = opaquePixelValue;

        //                        // compensate for depth/color not corresponding exactly by setting the pixel 
        //                        // to the left to opaque as well
        //                        this.greenScreenPixelData[greenScreenIndex - 1] = opaquePixelValue;
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    // do our processing outside of the using block
        //    // so that we return resources to the kinect as soon as possible
        //    if (true == colorReceived)
        //    {

        //        for (int i = 0; i < colorPixels.Length - 1; i++) {
        //            colorPixels[i] = 0x77;
        //        } 

        //        // Write the pixel data into our bitmap
        //        this.colorBitmap.WritePixels(
        //            new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
        //            this.colorPixels,
        //            this.colorBitmap.PixelWidth * sizeof(int),
        //            0);

        //        if (this.playerOpacityMaskImage == null)
        //        {
        //            this.playerOpacityMaskImage = new WriteableBitmap(
        //                this.depthWidth,
        //                this.depthHeight,
        //                96,
        //                96,
        //                PixelFormats.Bgra32,
        //                null);

        //            MaskedColor.OpacityMask = new ImageBrush { ImageSource = this.playerOpacityMaskImage };
        //        }

        //        this.playerOpacityMaskImage.WritePixels(
        //            new Int32Rect(0, 0, this.depthWidth, this.depthHeight),
        //            this.greenScreenPixelData,
        //            this.depthWidth * ((this.playerOpacityMaskImage.Format.BitsPerPixel + 7) / 8),
        //            0);
        //    }
        //}

    }
}