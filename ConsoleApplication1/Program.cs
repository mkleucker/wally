using Microsoft.Kinect;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KinectCommunication
{

    /// <summary>
    /// Implements the comunication with a Kinect Sensor and writes the sensor 
    /// data to a MemoryMappedFile.
    /// </summary>
    class Program
    {
        private String processId;

        static Mutex mutex;
        static MemoryMappedFile file;
        static MemoryMappedViewAccessor writer;

        private KinectSensor sensor;

        private Object[] skelToTransfer;

        private MemoryMappedFile[] files;
        private MemoryMappedViewAccessor[] writers;

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
                mutex = Mutex.OpenExisting("skeletonmutex");
                files = new MemoryMappedFile[2];
                writers = new MemoryMappedViewAccessor[2];
                for (int i = 0; i < 2; i++)
                {
                    MemoryMappedFile file = MemoryMappedFile.OpenExisting("skel-" + pId + "-" + i);
                    files[i] = file;
                    MemoryMappedViewAccessor writer = file.CreateViewAccessor();
                    writers[i] = writer;
                }

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

            var mappedfileThread = new Thread(transmitSkeletons);
            mappedfileThread.SetApartmentState(ApartmentState.STA);
            mappedfileThread.Start();


            initSensor();

        }

        /// <summary>
        /// Connects to the sensor. 
        /// </summary>
        private void initSensor()
        {
            this.sensor.SkeletonStream.Enable(new TransformSmoothParameters()
            {
                Smoothing = 0.5f,
                Correction = 0.1f,
                Prediction = 0.5f,
                JitterRadius = 0.1f,
                MaxDeviationRadius = 0.1f
            });

            skelToTransfer = new byte[0][];

            this.sensor.SkeletonFrameReady += this.SkeletonFrameReady;

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


        }

        /// <summary>
        /// Push the local Skeleton Data to a MemoryMappedFile.
        /// </summary>
        private void transmitSkeletons()
        {
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

                    mutex.WaitOne();

                    foreach (MemoryMappedViewAccessor writer in this.writers)
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
                            byte[] empty = new byte[1] { 0 };
                            writer.WriteArray<byte>(0, empty, 0, empty.Length);

                            // Console.WriteLine("Empty transmitted");
                            continue;

                        }




                        i++;
                    }

                    mutex.ReleaseMutex();



                    DateTime after = DateTime.Now;
                    int delay = after.Millisecond - before.Millisecond;
                    int fill = 100 - delay;
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
