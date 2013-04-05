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
        public MutexControl()
        {

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
                    //Critical Section
                }
                catch (Exception ex) { }
                mutex.ReleaseMutex();
            }
        }
    }
}
