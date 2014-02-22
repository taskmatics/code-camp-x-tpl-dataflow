using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace CustomBlocks
{
    public static class BlockFactory
    {
        public static IPropagatorBlock<T, T> CreateThrottleBlock<T>(int timeoutMilliseconds)
        {
            // Create variables to store the last posted message and a lock for it.
            var messageLock = new object();
            T currentMessage = default(T);

            // Create a buffer to store messages that will propagate out.
            var bufferBlock = new BufferBlock<T>();

            // Create a timer that will propagate the last message to the buffer after the timeout.
            var timer = new Timer(_ =>
            {
                lock (messageLock)
                    bufferBlock.Post(currentMessage);
            });

            // Create the incoming block that stores the message and (re-)starts the timer.
            var actionBlock = new ActionBlock<T>(new Action<T>(
                message =>
                {
                    lock (messageLock)
                    {
                        currentMessage = message;
                        timer.Change(timeoutMilliseconds, Timeout.Infinite);
                    }
                }));

            // Tell the buffer to complete after the action block completes.
            actionBlock.Completion.ContinueWith(t => bufferBlock.Complete());

            // Create a wrapper that implements IPropagatorBlock<T, T>.
            return DataflowBlock.Encapsulate(actionBlock, bufferBlock);
        }

        public static IPropagatorBlock<T, T> CreateDelayBlock<T>(int timeoutMilliseconds)
        {
            // Create a buffer to store messages that will propagate out.
            var bufferBlock = new BufferBlock<T>();

            // Create the incoming block that asynchronously delays and then propagates the message to the buffer.
            var actionBlock = new ActionBlock<T>(new Func<T, Task>(
                async message =>
                {
                    await Task.Delay(timeoutMilliseconds);
                    bufferBlock.Post(message);
                }),
                // Allow a high degree of parallelism because actual CPU time is low in the action.
                new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Int32.MaxValue });

            // Tell the buffer to complete after the action block completes.
            actionBlock.Completion.ContinueWith(t => bufferBlock.Complete());

            // Create a wrapper that implements IPropagatorBlock<T, T>.
            return DataflowBlock.Encapsulate(actionBlock, bufferBlock);
        }
    }
}
