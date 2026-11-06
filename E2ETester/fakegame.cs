using System;
using System.Threading;

namespace FakeGame
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Fake game running...");
            Thread.Sleep(20000); // run for 20 seconds
        }
    }
}
