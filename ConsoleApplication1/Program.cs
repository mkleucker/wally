using Microsoft.Kinect;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Xaml;

namespace KinectCommunication
{

    /// <summary>
    /// Implements the comunication with a Kinect Sensor and writes the sensor 
    /// data to a MemoryMappedFile.
    /// </summary>
    class Program
    {
        private String processId;

        bool pictureTaken = false;
        bool pictureTransmitted = false;
        String picturePath;
        int colorFrameCounter = 0;

        private KinectSensor sensor;

        private Object[] skelToTransfer;

        static Mutex skeletonMutex;
        private MemoryMappedFile[] skeletonFiles;
        private MemoryMappedViewAccessor[] skeletonWriters;

        static Mutex maskMutex;
        private MemoryMappedFile maskFile;
        private MemoryMappedViewAccessor maskWriter;

        static Mutex pictureMutex;
        private MemoryMappedFile pictureFile;
        private MemoryMappedViewAccessor pictureWriter;

        // Shadowing the users
        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution320x240Fps30;

        private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;

        private DepthImagePixel[] depthPixels;

        private byte[] greenScreenPixelData;

        private byte[] colorPixels;

        private ColorImagePoint[] colorCoordinates;

        private int colorToDepthDivisor;

        private int depthWidth;

        private int depthHeight;

        private byte opaquePixelValue = 255;

        static void Main(string[] args)
        {

            if (args.Length < 2)
            {
                Console.WriteLine("No Sensor specified");
                return;
            }
            else
            {
                Console.WriteLine(args[0] + " " + args[1]);
            }

            string kinectId = args[0];
            string pId = args[1];




            new Program(kinectId, pId);
        }

        public Program(string kinectId, string pId)
        {
            this.processId = pId;
            this.skelToTransfer = new Object[0];

            try
            {
                skeletonMutex = Mutex.OpenExisting("skeletonmutex");
                skeletonFiles = new MemoryMappedFile[2];
                skeletonWriters = new MemoryMappedViewAccessor[2];
                for (int i = 0; i < 2; i++)
                {
                    MemoryMappedFile file = MemoryMappedFile.OpenExisting("skel-" + pId + "-" + i);
                    skeletonFiles[i] = file;
                    MemoryMappedViewAccessor writer = file.CreateViewAccessor();
                    skeletonWriters[i] = writer;
                }

                maskMutex = Mutex.OpenExisting("maskmutex");
                maskFile = MemoryMappedFile.OpenExisting("mask-" + pId);
                maskWriter = maskFile.CreateViewAccessor();

                pictureMutex = Mutex.OpenExisting("picturemutex");
                pictureFile = MemoryMappedFile.OpenExisting("picture-" + pId);
                pictureWriter = pictureFile.CreateViewAccessor();

            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine("Memory-mapped file does not exist.");
            }

            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                Console.WriteLine(potentialSensor.GetHashCode().ToString());
                //Status should e.g. not be "Initializing" or "NotPowered"
                if (potentialSensor.Status == KinectStatus.Connected
                    &&
                    potentialSensor.UniqueKinectId == kinectId)
                {
                    this.sensor = potentialSensor;
                    Console.WriteLine("Connected to " + kinectId);
                    break;
                }
            }

            var mappedfileThread = new Thread(transmitData);
            mappedfileThread.SetApartmentState(ApartmentState.STA);
            mappedfileThread.Start();


            initSensor();

        }

        /// <summary>
        /// Connects to the sensor. 
        /// </summary>
        private void initSensor()
        {

            // Enable various streams
            this.sensor.DepthStream.Enable(DepthFormat);
            this.sensor.ColorStream.Enable(ColorFormat);
            this.sensor.SkeletonStream.Enable(new TransformSmoothParameters()
            {
                Smoothing = 0.5f,
                Correction = 0.1f,
                Prediction = 0.5f,
                JitterRadius = 0.1f,
                MaxDeviationRadius = 0.1f
            });

            // Setup Vars for Depth and Image
            this.depthWidth = this.sensor.DepthStream.FrameWidth;
            this.depthHeight = this.sensor.DepthStream.FrameHeight;
            Console.WriteLine(this.depthHeight);
            int colorWidth = this.sensor.ColorStream.FrameWidth;
            int colorHeight = this.sensor.ColorStream.FrameHeight;
            this.colorToDepthDivisor = colorWidth / this.depthWidth;
            this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];
            this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

            this.greenScreenPixelData = new byte[this.sensor.DepthStream.FramePixelDataLength];
            Console.WriteLine(this.sensor.DepthStream.FramePixelDataLength);
            this.colorCoordinates = new ColorImagePoint[this.sensor.DepthStream.FramePixelDataLength];

            skelToTransfer = new byte[0][];

            this.sensor.AllFramesReady += this.SensorAllFramesReady;

            try
            {
                this.sensor.Start();
            }
            catch (IOException)
            {
                this.sensor = null;
            }

        }

        /// <summary>
        /// Skeleton
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
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


            // PROCESS THE SKELETONS
            if (skeletons.Length != 0)
            {

                /**
                 * Serialize the Skeletons for Transfer.
                 **/

                ArrayList localTransfer = new ArrayList();

                int i = 0;
                foreach (Skeleton skel in skeletons)
                {
                    if (skel.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        MemoryStream ms = new MemoryStream();
                        bf.Serialize(ms, skel);

                        byte[] test = ms.ToArray();
                        localTransfer.Add(test);
                        ms.Close();

                        i++;
                    }

                }

                if (localTransfer.Count > 0)
                {

                    this.skelToTransfer = localTransfer.ToArray();
                }
                else
                {
                    this.skelToTransfer = new byte[0][];
                }

            }

            bool depthReceived = false;

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (null != depthFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

                    depthReceived = true;
                }
            }

            bool colorReceived = false;

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (null != colorFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    colorReceived = true;
                    colorFrameCounter++;
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

            if (true == colorReceived && colorFrameCounter > 210 && false == pictureTaken)
            {

                Console.WriteLine("ColorReceived");

                WriteableBitmap targetBitmap = new WriteableBitmap(640,
                                     480,
                                     96d, 96d,
                                     PixelFormats.Bgr32, null);

                targetBitmap.WritePixels(new Int32Rect(0, 0, targetBitmap.PixelWidth, targetBitmap.PixelHeight),
                        this.colorPixels, targetBitmap.PixelWidth * sizeof(int), 0);

                // create a png bitmap encoder which knows how to save a .png file
                BitmapEncoder encoder = new PngBitmapEncoder();

                // create frame from the writable bitmap and add to encoder
                encoder.Frames.Add(BitmapFrame.Create(targetBitmap));

                //only filename construction
                string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);
                string myPhotos = Environment.CurrentDirectory + "\\Resources"; //Im Build-Ordner ablegen
                string path = System.IO.Path.Combine
                    (myPhotos, "KinectSnapshot-" + time + "kinect" + processId + ".png");
                picturePath = path;

                // write the new file to disk
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        encoder.Save(fs);
                        pictureTaken = true;
                    }
                }
                catch (IOException)
                {
                }
            }

        }

        /// <summary>
        /// Push the local Skeleton Data to a MemoryMappedFile.
        /// </summary>
        private void transmitData()
        {

            byte[] emptyByteSkeleton = new byte[2255];
            byte[] emptyByteMask = new byte[307200];
            char[] emptyChar = new char[300];

            while (true)
            {

                DateTime before = DateTime.Now;


                try
                {
                    int i = 0;

                    if (skelToTransfer == null)
                    {
                        //Console.WriteLine("No Skeletons yet.");
                    }
                    else
                    {
                        //Console.WriteLine("Skeletons to Transmit: " + skelToTransfer.Length);
                    }

                    skeletonMutex.WaitOne();

                    foreach (MemoryMappedViewAccessor writer in this.skeletonWriters)
                    {
                        // No Skeleton Available
                        if (skelToTransfer != null && skelToTransfer.Length >= i + 1 && skelToTransfer[i] != null)
                        {
                            byte[] skeleton = (byte[])skelToTransfer[i];

                            writer.WriteArray<byte>(0, skeleton, 0, skeleton.Length);

                            //  Console.WriteLine("Skeleton Transmitted");

                        }
                        else
                        {
                            //writer.Flush();
                            writer.WriteArray<byte>(0, emptyByteSkeleton, 0, emptyByteSkeleton.Length);

                            // Console.WriteLine("Empty transmitted");
                            continue;

                        }

                        i++;
                    }

                    skeletonMutex.ReleaseMutex();

                    // TRANSFER THE MASK
                    maskMutex.WaitOne();

                    if (this.greenScreenPixelData != null && this.greenScreenPixelData.Length > 0)
                    {
                        maskWriter.WriteArray<byte>(0,
                            this.greenScreenPixelData,
                            0,
                            this.greenScreenPixelData.Length);
                    }
                    else
                    {
                        maskWriter.WriteArray<byte>(0,
                            emptyByteMask,
                            0,
                            emptyByteMask.Length
                        );
                    }
                    maskMutex.ReleaseMutex();

                    // TRANSFER THE Picture-Path
                    pictureMutex.WaitOne();

                    if (this.picturePath != null)
                    {
                        char[] buffer = picturePath.ToCharArray();
                        pictureWriter.WriteArray<char>(0,
                            buffer,
                            0,
                            buffer.Length);
                        pictureTransmitted = true;
                    }
                    else
                    {
                        pictureWriter.WriteArray<char>(0,
                            emptyChar,
                            0,
                            emptyChar.Length
                        );
                    }
                    pictureMutex.ReleaseMutex();

                    DateTime after = DateTime.Now;
                    int delay = after.Millisecond - before.Millisecond;
                    int fill = 33 - delay;
                    if (fill > 0) Thread.Sleep(fill);

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error on transmitting Skeletons to Mutex");
                    Console.WriteLine(ex.StackTrace);
                }

            }

        }

    }
}
