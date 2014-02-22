using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace CustomBlocks
{
    public class FileDropSourceBlock : ISourceBlock<string>
    {
        private readonly FileSystemWatcher _watcher;
        private readonly IPropagatorBlock<string, string> _source;
        private readonly ActionBlock<string> _checker;
        private readonly IPropagatorBlock<string, string> _delay;

        public FileDropSourceBlock(string path, string filter = null, bool includeSubdirectories = false)
        {
            // Create a buffer to store messages that will propagate out.
            _source = new BufferBlock<string>();

            // Create a processor that checks file access and decides how to propagate message.
            _checker = new ActionBlock<string>(new Action<string>(CheckFileAccess));
            _checker.Completion.ContinueWith(t => _source.Complete());

            // Create a delay block that delays the file path for 1 second before propagating to the checker.
            _delay = BlockFactory.CreateDelayBlock<string>(1000);
            _delay.LinkTo(_checker, new DataflowLinkOptions { PropagateCompletion = true });

            // Create a file system watcher to listen for events from the file system.
            _watcher = new FileSystemWatcher(path);
            _watcher.Filter = filter;
            _watcher.IncludeSubdirectories = includeSubdirectories;

            // Fault this block if an error occurs in the file system watcher.
            _watcher.Error += (s, e) => Fault(e.GetException());

            // Push the newly created file's path to the delay block.
            _watcher.Created += (s, e) => _delay.Post(e.FullPath);
            _watcher.EnableRaisingEvents = true;
        }

        private void CheckFileAccess(string path)
        {
            try
            {
                // Check if the file is accessible by opening it.
                using (File.Open(path, FileMode.Open, FileAccess.Read)) { }
                _source.Post(path);
            }
            catch
            {
                // If the file is inaccessible, post the file path back to the delay block.
                // If delay has been completed, send to the buffer.
                if (!_delay.Post(path))
                    _source.Post(path);
            }
        }

        private void DisposeWatcher()
        {
            // Turn off and dispose the file system watcher.
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }

        #region ISourceBlock Implementation
        public string ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<string> target, out bool messageConsumed)
        {
            return _source.ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public IDisposable LinkTo(ITargetBlock<string> target, DataflowLinkOptions linkOptions)
        {
            return _source.LinkTo(target, linkOptions);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<string> target)
        {
            _source.ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<string> target)
        {
            return _source.ReserveMessage(messageHeader, target);
        }

        public void Complete()
        {
            DisposeWatcher();
            _delay.Complete();
        }

        public Task Completion
        {
            get { return _source.Completion; }
        }

        public void Fault(Exception exception)
        {
            DisposeWatcher();
            _delay.Fault(exception);
        }
        #endregion
    }
}
