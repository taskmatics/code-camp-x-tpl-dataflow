using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace CustomBlocks
{
    public class ThrottleBlock<T> : IPropagatorBlock<T, T>
    {
        private readonly int _timeoutMilliseconds;
        private readonly ITargetBlock<T> _actionBlock;
        private readonly IPropagatorBlock<T, T> _bufferBlock;
        private readonly Timer _timer;
        private T _currentMessage;
        private readonly object _messageLock;

        public ThrottleBlock(int timeoutMilliseconds)
        {
            // Store timeout.
            _timeoutMilliseconds = timeoutMilliseconds;

            // Create the incoming block that stores the message and (re-)starts the timer.
            _actionBlock = new ActionBlock<T>(new Action<T>(ThrottleInput));

            // Create a buffer to store messages that will propagate out.
            _bufferBlock = new BufferBlock<T>();

            // Create a timer that will propagate the last message to the buffer after the timeout.
            _timer = new Timer(PostMessageToBuffer);

            // Create lock to ensure thread safety when accessing the latest message.
            _messageLock = new object();
        }

        private void ThrottleInput(T message)
        {
            lock (_messageLock)
            {
                // Save latest message and (re-)start timer.
                _currentMessage = message;
                _timer.Change(_timeoutMilliseconds, Timeout.Infinite);
            }
        }

        private void PostMessageToBuffer(object state)
        {
            lock (_messageLock)
                // Push latest message to the outgoing buffer.
                _bufferBlock.Post(_currentMessage);
        }

        #region IPropagatorBlock Implementation
        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, T messageValue, ISourceBlock<T> source, bool consumeToAccept)
        {
            return _actionBlock.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public void Complete()
        {
            _actionBlock.Complete();
            _actionBlock.Completion.ContinueWith(t => _bufferBlock.Complete());
        }

        public System.Threading.Tasks.Task Completion
        {
            get { return _bufferBlock.Completion; }
        }

        public void Fault(Exception exception)
        {
            _actionBlock.Fault(exception);
            _actionBlock.Completion.ContinueWith(t => _bufferBlock.Fault(exception));
        }

        public T ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<T> target, out bool messageConsumed)
        {
            return _bufferBlock.ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public IDisposable LinkTo(ITargetBlock<T> target, DataflowLinkOptions linkOptions)
        {
            return _bufferBlock.LinkTo(target, linkOptions);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<T> target)
        {
            _bufferBlock.ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<T> target)
        {
            return _bufferBlock.ReserveMessage(messageHeader, target);
        } 
        #endregion
    }
}
