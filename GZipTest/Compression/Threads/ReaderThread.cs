using GZipTest.Compression.SharedState;
using System;
using System.IO;
using System.Threading;

namespace GZipTest.Compression.Threads
{
    /// <summary>
    /// File reader thread
    /// </summary>
    public class ReaderThread
    {
        readonly string file;
        readonly int bufSize;
        readonly IForReaderThread outputState;
        readonly Thread thread;

        /// <summary>
        /// Cancelling with jamming potential exceptions
        /// </summary>
        void SafeCancel()
        {
            try
            {
                ((ICancellable)outputState).Cancel();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="file">Path to file which need to be read</param>
        /// <param name="bufSize">Read buffer size</param>
        /// <param name="outputState">State, shared with compression thread</param>
        public ReaderThread(string file, int bufSize, IForReaderThread outputState)
        {
            this.file = file;
            this.bufSize = bufSize;
            this.outputState = outputState;
            thread = new Thread(RunThread) { IsBackground = true, Name = "Reader" };
            thread.Start();
        }

        /// <summary>
        /// Thread&apos;s execution body
        /// </summary>
        void RunThread()
        {
            try
            {
                using (var fileStream = File.OpenRead(file))
                {
                    int bytesRead;
                    var packet = new FilePacketArray(0, bufSize);
                    var count = 0;

                    while ((bytesRead = fileStream.Read(packet.Buffer, 0, bufSize)) > 0)
                    {
                        packet.FilledLength = bytesRead;
                        outputState.Produce(packet, out var cancel);
                        if (cancel)
                            return;
                        packet.IncIndex();
                        count++;
                    }

                    if (count == 0) // If file is empty
                    {
                        packet.FilledLength = 0;
                        outputState.Produce(packet, out var cancel);
                    }

                    outputState.ReadCompleted();
                }
            }
            catch (IOException)
            {
                SafeCancel();
                Console.WriteLine("Could not read the file.");
            }
            catch (Exception ex)
            {
                SafeCancel();
                Console.WriteLine("Error in file reading thread: " + ex.ToString());
            }
        }

        /// <summary>
        /// Awaiting for thread finish
        /// </summary>
        public void Join() => thread.Join();

        /// <summary>
        /// Awaiting for thread finish
        /// </summary>
        public void Join(int millisecondsTimeout) => thread.Join(millisecondsTimeout);
    }
}
