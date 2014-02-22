using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace CustomBlocks
{
    class Program
    {
        static void Main(string[] args)
        {
            // Demo 1
            //TypeAheadDemo();

            // Demo 2
            FileDropSourceDemo();

            Console.ReadLine();
        }

        private static void TypeAheadDemo()
        {
            var throttle =
                new ThrottleBlock<string>(200);
                //BlockFactory.CreateThrottleBlock<string>(200);

            var logger = new ActionBlock<string>(word => Console.WriteLine(word));

            // Create pipeline.
            throttle.LinkTo(logger);

            // Initialize variables for simulating typing. 
            var random = new Random();
            var fullWord = "SF Code Camp 2014";
            var partialWord = "";

            // Simulate typing by a user.
            foreach (var character in fullWord)
            {
                // Add a character to the partial word and post it.
                partialWord += character;
                throttle.Post(partialWord);

                // Sleep for a random length of time between 0 and 300 milliseconds.
                var i = random.Next(0, 300);
                Thread.Sleep(i);
                Console.WriteLine("slept: " + i);
            }
        }

        private static void FileDropSourceDemo()
        {
            // Create file drop path and ensure directory exists.
            var dropPath = Path.Combine(Environment.CurrentDirectory, "FileDrop");
            Directory.CreateDirectory(dropPath);

            // Create file drop source block and subsequent logger block.
            var fileDrop = new FileDropSourceBlock(dropPath);
            var logger = new ActionBlock<string>(path => Console.WriteLine(path));

            // Create link.
            fileDrop.LinkTo(logger);
        }
    }
}
