using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            /* Other Threads or Processes*/
            var mutex = Mutex.OpenExisting("mymutex");
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
