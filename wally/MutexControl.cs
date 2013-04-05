using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace wally
{
    public class MutexControl
    {
        private MainWindow parent;
        private static int Runs = 0;

        public MutexControl(MainWindow parent)
        {
            this.parent = parent;


        }

        public void mtThreading()
        {
            var mutex = new Mutex(true, "mymutex");
            mutex.ReleaseMutex(); //Releasing at first

            while (true)
            {
                mutex.WaitOne();
                try
                {
                    Runs++;
                    Console.WriteLine(Runs);
                }
                catch (Exception ex) { }
                mutex.ReleaseMutex();
            }
        }
    }
}
