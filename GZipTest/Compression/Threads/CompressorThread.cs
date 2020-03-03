using GZipTest.Compression.SharedState;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest.Compression.Threads
{
    class CompressorThread
    {
        readonly int inputBufSize;
        readonly IForCompressorThreadInput inputState;
        readonly IForCompressorThreadOutput outputState;
        readonly Thread thread;
        readonly bool debugMode;

        /// <summary>
        /// Cancel with suppressing any exceptions
        /// </summary>
        void SafeCancel()
        {
            try
            {
                ((ICancellable)inputState).Cancel();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="inputBufSize">Input buffer size</param>
        /// <param name="inputState">State, shared with reading thread</param>
        /// <param name="debugMode">In debug mode we are not compressing blocks, but instead save them to disk in the form: packet_number-thread_number.txt</param>
        public CompressorThread(int inputBufSize, IForCompressorThreadInput inputState, IForCompressorThreadOutput outputState, bool debugMode = false)
        {
            this.inputBufSize = inputBufSize;
            this.inputState = inputState;
            this.outputState = outputState;
            this.debugMode = debugMode;
            thread = new Thread(RunThread) { IsBackground = true, Name = "Compressor" };
            thread.Start();
        }

        /// <summary>
        /// Thread&amp;s executing body
        /// </summary>
        void RunThread()
        {
            try
            {
                FilePacketMemoryStream notCompressedPacket;
                bool cancel;

                #region Normal (production) compression mode
                if (!debugMode)
                {
                    while ((notCompressedPacket = inputState.Consume(out cancel)) != null && !cancel)
                    {
                        try
                        {
                            var compressedMS = new MemoryStream();
                            byte[] arr;
                            try
                            {
                                using (var compressionStream = new GZipStream(compressedMS, CompressionMode.Compress))
                                {
                                    notCompressedPacket.MemoryStream.CopyTo(compressionStream);
                                }
                                arr = compressedMS.ToArray();
                            }
                            catch
                            {
                                compressedMS.Dispose();
                                throw;
                            }

                            outputState.Produce(
                                new FilePacketMemoryStream
                                {
                                    Index = notCompressedPacket.Index,
                                    MemoryStream = new MemoryStream(arr)
                                },
                                out cancel);

                            if (cancel)
                            {
                                // If got a signal to exit from the output state, hand over it to the input state too,
                                // and then quit from the thread.
                                ((ICancellable)inputState).Cancel();
                                return;
                            }
                        }
                        finally
                        {
                            notCompressedPacket.MemoryStream.Dispose();
                        }
                    }
                }
                #endregion

                #region Debug mode: instrad of compression, simply save packets to files with names, consisting of packet number and thread id
                else
                {
                    while ((notCompressedPacket = inputState.Consume(out cancel)) != null && !cancel)
                    {
                        try
                        {
                            using (var writer = File.Create($"{(notCompressedPacket.Index + 1)}-{thread.ManagedThreadId}.txt"))
                            {
                                notCompressedPacket.MemoryStream.CopyTo(writer);
                                writer.Flush();
                            }
                        }
                        finally
                        {
                            notCompressedPacket.MemoryStream.Dispose();
                        }
                    }
                }
                #endregion
            }
            catch (Exception ex)
            {
                SafeCancel();
                Console.WriteLine("Error in compression thread: " + ex.ToString());
            }

            outputState.CompressionCompleted();
        }

        /// <summary>
        /// Await for thread ending
        /// </summary>
        public void Join() => thread.Join();
    }
}
