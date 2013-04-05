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

        private KinectSensor sensor;
        private int DeviceCount;

        private DrawingGroup drawingGroup; //for skeleton rendering output

        private DrawingImage imageSource; //draw image that we will display

        /// <summary>
        /// Format we will use for the depth stream
        /// </summary>
        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution320x240Fps30;

        /// <summary>
        /// Format we will use for the color stream
        /// </summary>
        private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;
        
        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Bitmap that will hold opacity mask information
        /// </summary>
        private WriteableBitmap playerOpacityMaskImage = null;

        /// <summary>
        /// Intermediate storage for the depth data received from the sensor
        /// </summary>
        private DepthImagePixel[] depthPixels;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] colorPixels;

        /// <summary>
        /// Intermediate storage for the green screen opacity mask
        /// </summary>
        private int[] greenScreenPixelData;

        /// <summary>
        /// Intermediate storage for the depth to color mapping
        /// </summary>
        private ColorImagePoint[] colorCoordinates;

        /// <summary>
        /// Inverse scaling factor between color and depth
        /// </summary>
        private int colorToDepthDivisor;

        /// <summary>
        /// Width of the depth image
        /// </summary>
        private int depthWidth;

        /// <summary>
        /// Height of the depth image
        /// </summary>
        private int depthHeight;

        /// <summary>
        /// Indicates opaque in an opacity mask
        /// </summary>
        private int opaquePixelValue = -1;

        // Mutex
        static long MemoryMappedFileCapacitySkeleton = 168; //10MB in Byte
        static Mutex mutex1;
        static MemoryMappedFile file1;

        private int[] mmf_ints;

        public MainWindow()
        {
            InitializeComponent();
            //this.WindowStyle = WindowStyle.None;
            //this.WindowState = WindowState.Maximized;
            //this.Cursor = System.Windows.Input.Cursors.None;
        }


        //Execute startup tasks here
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            mmf_ints = new int[6];

            this.myPonyLines = new ArrayList();

            this.DeviceCount = KinectSensor.KinectSensors.Count;

            this.drawingGroup = new DrawingGroup(); //we will use for drawing
            this.imageSource = new DrawingImage(this.drawingGroup); //imagesource we can use in our image control
            MyImage.Source = this.imageSource; //display the drawing to use our image control

            // Look through all sensors and start the first connected one.
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                //Status should e.g. not be "Initializing" or "NotPowered"
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            /// MUTEX Zeuchs
            //System.Collections.ArrayList processes = new System.Collections.ArrayList();
            //for (int i = 0; i < this.DeviceCount; i++)
            //{
            //    Process p = new System.Diagnostics.Process();
            //    p.StartInfo.CreateNoWindow = true;
            //    p.StartInfo.FileName
            //      = Environment.CurrentDirectory + "\\ConsoleApplication1.exe";
            //    p.StartInfo.WindowStyle
            //      = System.Diagnostics.ProcessWindowStyle.Normal;
            //    p.Start();
            //    processes.Add(p);
            //}

            //var mappedfileThread = new Thread(MemoryMapData);
            //mappedfileThread.SetApartmentState(ApartmentState.STA);
            //mappedfileThread.Start();

            //Console.WriteLine("created MutexThread");


            if (null != this.sensor)
            {
                //this.sensor.SkeletonStream.Enable(); //ohne Smoothing
                this.sensor.SkeletonStream.Enable(new TransformSmoothParameters()
                {
                    Smoothing = 0.5f,
                    Correction = 0.1f,
                    Prediction = 0.5f,
                    JitterRadius = 0.1f,
                    MaxDeviationRadius = 0.1f
                });

                // Turn on the depth stream to receive depth frames
                this.sensor.DepthStream.Enable(DepthFormat);

                this.depthWidth = this.sensor.DepthStream.FrameWidth;

                this.depthHeight = this.sensor.DepthStream.FrameHeight;

                this.sensor.ColorStream.Enable(ColorFormat);

                int colorWidth = this.sensor.ColorStream.FrameWidth;
                int colorHeight = this.sensor.ColorStream.FrameHeight;

                this.colorToDepthDivisor = colorWidth / this.depthWidth;

                //Init Polyline
                myPolyline = new Polyline();
                myPolyline.Stroke = System.Windows.Media.Brushes.White;
                myPolyline.StrokeThickness = 2;
                myPolyline.FillRule = FillRule.EvenOdd;
                myPonyLines.Add(myPolyline);
                currentLine = (Polyline)myPonyLines[myPonyLines.Count - 1];
                myGrid.Children.Add((Polyline)myPonyLines[myPonyLines.Count - 1]);

                //Add an event handler to be called whenever there is new skeleton frame...
                this.sensor.SkeletonFrameReady += this.SkeletonFrameReady;


                // Allocate space to put the depth pixels we'll receive
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                // Allocate space to put the color pixels we'll create
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                this.greenScreenPixelData = new int[this.sensor.DepthStream.FramePixelDataLength];

                this.colorCoordinates = new ColorImagePoint[this.sensor.DepthStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.MaskedColor.Source = this.colorBitmap;

                // Add an event handler to be called whenever there is new depth frame data
                this.sensor.AllFramesReady += this.SensorAllFramesReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }
        }

        /// <summary>
        /// Skeleton
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                //Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {


                            this.DrawBonesAndJoints(skel, dc);
                            dc.DrawEllipse(
                                this.centerPointBrush,
                                null,
                                this.SkeletonPointToScreen(skel.Joints[JointType.HandRight].Position),
                                BodyCenterThickness * skel.Joints[JointType.HandRight].Position.Z,
                                BodyCenterThickness * skel.Joints[JointType.HandRight].Position.Z);

                            System.Windows.Point Point1 = this.SkeletonPointToScreen(skel.Joints[JointType.HandRight].Position);

                            //As long as the hand is nearer to the screen than the user's body -> painting 
                            //As soon as the hand is further away from the screen than the user's body (or the same level) -> not painting
                            if (skel.Joints[JointType.HandRight].Position.Z > skel.Position.Z - 0.1 && currentLine.Points.Count > 1)
                            {
                                DrawLine(currentLine.Stroke, 5);
                            }

                            //When the hand is near to the screen (if-case) the line gets thicker

                            if (skel.Joints[JointType.HandRight].Position.Z > skel.Position.Z - 0.8 && skel.Joints[JointType.HandRight].Position.Z < skel.Position.Z - 0.3 && currentLine.Points.Count > 1)
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
                            else if (skel.Joints[JointType.HandRight].Position.Z > skel.Position.Z - 0.3 && currentLine.Points.Count > 1)
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


                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Manage the selection of different colors
        /// </summary>
        private void ColorSelection(Skeleton skel) {

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

                if (leftHandY > 3 * yCoordSteps && currentLine.Stroke != System.Windows.Media.Brushes.Blue)
                {
                    DrawLine(System.Windows.Media.Brushes.Blue, currentLine.StrokeThickness);
                }
                if (leftHandY > 2 * yCoordSteps && leftHandY < 3 * yCoordSteps && currentLine.Stroke != System.Windows.Media.Brushes.Red)
                {
                    DrawLine(System.Windows.Media.Brushes.Red, currentLine.StrokeThickness);
                }
                if (leftHandY > yCoordSteps && leftHandY < 2 * yCoordSteps && currentLine.Stroke != System.Windows.Media.Brushes.Yellow)
                {
                    DrawLine(System.Windows.Media.Brushes.Yellow, currentLine.StrokeThickness);
                }
                if (leftHandY > 0 && leftHandY < yCoordSteps && currentLine.Stroke != System.Windows.Media.Brushes.White)
                {
                    DrawLine(System.Windows.Media.Brushes.White, currentLine.StrokeThickness);
                }
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

            currentLine = (Polyline)myPonyLines[myPonyLines.Count - 1];
            Console.WriteLine("Current Line Points:" + currentLine.Points.Count);
            Console.WriteLine("Number of Lines:" + myPonyLines.Count);
            Polyline newLine = new Polyline();
            newLine.Stroke = lineColor;
            newLine.StrokeThickness = lineThickness;
            myPonyLines.Add(newLine);
            myGrid.Children.Add((Polyline)myPonyLines[myPonyLines.Count - 1]);
            currentLine = newLine;
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
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
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

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        private void MemoryMapData()
        {
            /*Create MemoryMappedfile for Kinect Data Exchange */
            var file1 = MemoryMappedFile.CreateNew(
            "SkeletonExchange", MemoryMappedFileCapacitySkeleton);

            /*Mutual Exclusion between 3 processes*/
            var mutex = new Mutex(true, "mappedfilemutex");
            mutex.ReleaseMutex(); //Freigabe gleich zu Beginn

            using (var accessor = file1.CreateViewAccessor())
            {

                int[] ints = new int[6];

                for (; ; ) /*Skeleton-Receive Loop*/
                {
                    DateTime before = DateTime.Now;
                    mutex.WaitOne();

                    try
                    {
                        accessor.ReadArray(
                           0, mmf_ints, 0, ints.Length);
                        Array.Copy(mmf_ints, 0,
                           ints, 0, mmf_ints.Length);
                        mutex.ReleaseMutex();

                        Console.WriteLine(mmf_ints[0]);

                        DateTime after = DateTime.Now;
                        int delay = after.Millisecond - before.Millisecond;
                        int fill = 10 - delay;
                        if (fill > 0) Thread.Sleep(fill);

                    }
                    catch (Exception ex) { }
                }
            }
        }
        /// <summary>
        /// Event handler for Kinect sensor's DepthFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            // in the middle of shutting down, so nothing to do
            if (null == this.sensor)
            {
                return;
            }

            bool depthReceived = false;
            bool colorReceived = false;

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (null != depthFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

                    depthReceived = true;
                }
            }

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (null != colorFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    colorReceived = true;
                }
            }

            // do our processing outside of the using block
            // so that we return resources to the kinect as soon as possible
            if (true == depthReceived)
            {
                this.sensor.CoordinateMapper.MapDepthFrameToColorFrame(
                    DepthFormat,
                    this.depthPixels,
                    ColorFormat,
                    this.colorCoordinates);

                Array.Clear(this.greenScreenPixelData, 0, this.greenScreenPixelData.Length);

                // loop over each row and column of the depth
                for (int y = 0; y < this.depthHeight; ++y)
                {
                    for (int x = 0; x < this.depthWidth; ++x)
                    {
                        // calculate index into depth array
                        int depthIndex = x + (y * this.depthWidth);

                        DepthImagePixel depthPixel = this.depthPixels[depthIndex];

                        int player = depthPixel.PlayerIndex;

                        // if we're tracking a player for the current pixel, do green screen
                        if (player > 0)
                        {
                            // retrieve the depth to color mapping for the current depth pixel
                            ColorImagePoint colorImagePoint = this.colorCoordinates[depthIndex];

                            // scale color coordinates to depth resolution
                            int colorInDepthX = colorImagePoint.X / this.colorToDepthDivisor;
                            int colorInDepthY = colorImagePoint.Y / this.colorToDepthDivisor;

                            // make sure the depth pixel maps to a valid point in color space
                            // check y > 0 and y < depthHeight to make sure we don't write outside of the array
                            // check x > 0 instead of >= 0 since to fill gaps we set opaque current pixel plus the one to the left
                            // because of how the sensor works it is more correct to do it this way than to set to the right
                            if (colorInDepthX > 0 && colorInDepthX < this.depthWidth && colorInDepthY >= 0 && colorInDepthY < this.depthHeight)
                            {
                                // calculate index into the green screen pixel array
                                int greenScreenIndex = colorInDepthX + (colorInDepthY * this.depthWidth);

                                // set opaque
                                this.greenScreenPixelData[greenScreenIndex] = opaquePixelValue;

                                // compensate for depth/color not corresponding exactly by setting the pixel 
                                // to the left to opaque as well
                                this.greenScreenPixelData[greenScreenIndex - 1] = opaquePixelValue;
                            }
                        }
                    }
                }
            }

            // do our processing outside of the using block
            // so that we return resources to the kinect as soon as possible
            if (true == colorReceived)
            {
                // Write the pixel data into our bitmap
                this.colorBitmap.WritePixels(
                    new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                    this.colorPixels,
                    this.colorBitmap.PixelWidth * sizeof(int),
                    0);

                if (this.playerOpacityMaskImage == null)
                {
                    this.playerOpacityMaskImage = new WriteableBitmap(
                        this.depthWidth,
                        this.depthHeight,
                        96,
                        96,
                        PixelFormats.Bgra32,
                        null);

                    MaskedColor.OpacityMask = new ImageBrush { ImageSource = this.playerOpacityMaskImage };
                }

                this.playerOpacityMaskImage.WritePixels(
                    new Int32Rect(0, 0, this.depthWidth, this.depthHeight),
                    this.greenScreenPixelData,
                    this.depthWidth * ((this.playerOpacityMaskImage.Format.BitsPerPixel + 7) / 8),
                    0);
            }
        }

    }
}
