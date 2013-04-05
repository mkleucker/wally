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


namespace wally
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        //Width and Height of our drawing output
        private const float RenderWidth = 640.0f;
        private const float RenderHeight = 480.0f;

        //Line that is drawn by right hand of the user
        private Polyline myPolyline;
        private Polyline currentLine;

        private int currentStroke = 2; //1 = thick 2 = thin

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

            System.Collections.ArrayList processes = new System.Collections.ArrayList();
            for (int i = 0; i < this.DeviceCount; i++)
            {
                Process p = new System.Diagnostics.Process();
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.FileName
                  = Environment.CurrentDirectory + "\\ConsoleApplication1.exe";
                p.StartInfo.WindowStyle
                  = System.Diagnostics.ProcessWindowStyle.Normal;
                p.Start();
                processes.Add(p);
            }

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
                            if (skel.Joints[JointType.HandRight].Position.Z > skel.Position.Z && currentLine.Points.Count > 1)
                            {
                                DrawLine(System.Windows.Media.Brushes.White, 5);
                            }

                            //When the hand is near to the screen (if-case) the line gets thicker

                            if (skel.Joints[JointType.HandRight].Position.Z > skel.Position.Z - 0.8 && skel.Joints[JointType.HandRight].Position.Z < skel.Position.Z - 0.3 && currentLine.Points.Count > 1)
                            {
                                System.Console.WriteLine("Thick Line");
                                if (currentStroke == 2)
                                {
                                    DrawLine(System.Windows.Media.Brushes.White, 20);
                                    currentStroke = 1;
                                }
                                else
                                    currentLine.StrokeThickness = 20;

                            }

                            //When the hand is further away from the screen (else if - case) the line gets thinner
                            else if (skel.Joints[JointType.HandRight].Position.Z > skel.Position.Z - 0.3 && currentLine.Points.Count > 1)
                            {
                                System.Console.WriteLine("Thin Line");
                                if (currentStroke == 1)
                                {
                                    DrawLine(System.Windows.Media.Brushes.White, 5);
                                    currentStroke = 2;
                                }
                                else
                                    currentLine.StrokeThickness = 5;

                            }


                            if (skel.Joints[JointType.HandRight].Position.Z < skel.Position.Z - 0.1)
                            {
                                currentLine.Points.Add(Point1);
                            }

                            //Polyline currentLine = (Polyline)myPonyLines[myPonyLines.Count - 1];

                            //double leftHandY = skel.Joints[JointType.HandLeft].Position.Y;
                            //SolidColorBrush color = System.Windows.Media.Brushes.Blue;

                            //Console.WriteLine(Point1);

                            //if (leftHandY > 0)
                            //{
                            //    color = System.Windows.Media.Brushes.Red;
                            //}
                            //currentLine.Stroke = color;

                            //if (currentLine.Points.Count > 100)
                            //{
                            //    Random random = new Random();
                            //    int randomNumber = random.Next(1, 10);
                            //    Polyline newLine = new Polyline();
                            //    newLine.Stroke = System.Windows.Media.Brushes.White;
                            //    newLine.StrokeThickness = randomNumber;
                            //    newLine.FillRule = FillRule.EvenOdd;
                            //    newLine.StrokeStartLineCap = PenLineCap.Round;
                            //    newLine.StrokeEndLineCap = PenLineCap.Round;
                            //    myPonyLines.Add(newLine);
                            //    myGrid.Children.Add((Polyline)myPonyLines[myPonyLines.Count - 1]);
                            //    currentLine = newLine;
                            //}

                            //currentLine.Points.Add(Point1);


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
        private void DrawLine(System.Windows.Media.Brush lineColor, int lineThickness)
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

    }
}
