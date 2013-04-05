using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    class Program
    {
        static long MemoryMappedFileCapacitySkeleton = 168; //10MB in Byte
        static Mutex mutex;
        static MemoryMappedFile file;
        static MemoryMappedViewAccessor writer;

        static void Main(string[] args)
        {

            try
            {
                mutex = Mutex.OpenExisting("mappedfilemutex");
                file = MemoryMappedFile.OpenExisting("SkeletonExchange");
                writer = file.CreateViewAccessor();
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine("Memory-mapped file does not exist.");
            }

            while (true)
            {
                mutex.WaitOne();
                try
                {
                    Random rand = new Random();

                    int[] test = new int[]{
                            rand.Next(0, 100),
                            rand.Next(0, 100),
                            rand.Next(0, 100),
                            rand.Next(0, 100),
                            rand.Next(0, 100),
                            rand.Next(0, 100)
                    };
                    writer.WriteArray(0, test, 0, test.Length);

                }
                catch (Exception ex) { }
                mutex.ReleaseMutex();
            }
        }
    }
}
