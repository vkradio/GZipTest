using GZipTest.Compression.SharedState;
using System;
using System.IO;
using System.Threading;

namespace GZipTest.Compression.Threads
{
    /// <summary>
    /// Compressed file writer thread
    /// </summary>
    class WriterThread
    {
        readonly string file;
        readonly int bufSize;
        readonly IForWriterThread inputState;
        readonly Thread thread;

        /// <summary>
        /// Cancelling with jamming potential exceptions
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
        /// Removing part of compressed file in case of cancellation, with jamming of potential exceptions
        /// </summary>
        void SafeDeleteFile()
        {
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="file">Path to file to be read</param>
        /// <param name="bufSize">Read buffer size</param>
        /// <param name="inputState">State, shared with compression thread</param>
        public WriterThread(string file, int bufSize, IForWriterThread inputState)
        {
            this.file = file;
            this.bufSize = bufSize;
            this.inputState = inputState;
            thread = new Thread(RunThread) { IsBackground = true, Name = "Writer" };
            thread.Start();
        }

        /// <summary>
        /// Thread&apos;s execution body
        /// </summary>
        void RunThread()
        {
            try
            {
                using (var fileStream = File.Create(file, bufSize))
                {
                    bool cancel;
                    FilePacketMemoryStream packet;

                    while ((packet = inputState.Consume(out cancel)) != null && !cancel)
                    {
                        try
                        {
                            packet.MemoryStream.CopyTo(fileStream);
                        }
                        finally
                        {
                            packet.MemoryStream.Dispose();
                        }
                    }

                    if (cancel)
                    {
                        fileStream.Flush();
                        SafeDeleteFile();
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                SafeCancel();
                SafeDeleteFile();
                Console.WriteLine("You have no rights to write to this folder.");
            }
            catch (PathTooLongException)
            {
                SafeCancel();
                SafeDeleteFile();
                Console.WriteLine("File path too long. Please set a shorter file name.");
            }
            catch (DirectoryNotFoundException)
            {
                SafeCancel();
                SafeDeleteFile();
                Console.WriteLine("Destination folder not exists.");
            }
            catch (NotSupportedException)
            {
                SafeCancel();
                SafeDeleteFile();
                Console.WriteLine("Invalid characters in destination foder or file name.");
            }
            catch (IOException)
            {
                SafeCancel();
                SafeDeleteFile();
                Console.WriteLine("Unable to save file. Possibly there is no enough free disk space, or disk was detached.");
            }
            catch (Exception ex)
            {
                SafeCancel();
                SafeDeleteFile();
                Console.WriteLine("Error in file read thread: " + ex.ToString());
            }
        }

        /// <summary>
        /// Awaiting for thread ending
        /// </summary>
        public void Join() => thread.Join();
    }
}
