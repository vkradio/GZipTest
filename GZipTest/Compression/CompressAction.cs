using GZipTest.Compression.SharedState;
using GZipTest.Compression.Threads;
using System;
using System.IO;
using System.Linq;

namespace GZipTest.Compression
{
    class CompressAction : ActionWithSourceAndDestination
    {
        const int c_readFileChunkSize = 4 * 1024 * 1024; // 4 MB

        public CompressAction(string src, string dest) : base(src, dest) { }

        protected override void ExecuteConcrete()
        {
            #region Extension of the compressed file
            try
            {
                var extension = Path.GetExtension(destination);
                if (!extension.Equals(".gz", StringComparison.CurrentCultureIgnoreCase))
                    destination += ".gz";
            }
            catch
            {
                Console.WriteLine("Resulting file name contrains invalid characters.");
                return;
            }
            #endregion

            #region Checking and re-writing existing file
            try
            {
                if (File.Exists(destination))
                {
                    Console.WriteLine($"The file {destination} already exists. Rewrite it? y/n");
                    var answer = Console.ReadLine();
                    if (!answer.Equals("y", StringComparison.CurrentCultureIgnoreCase))
                    {
                        return;
                    }
                    else
                    {
                        try
                        {
                            File.Delete(destination);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            Console.WriteLine("You have no rights to overwrite the file, or this file is in use by another app.");
                            return;
                        }
                        catch (IOException)
                        {
                            Console.WriteLine("The file is in use by another app, so it is unable to overwrite it.");
                            return;
                        }
                        catch
                        {
                            Console.WriteLine("Unable to overwrite this file. Probably you've set an invalid file name.");
                            return;
                        }
                    }
                }
            }
            catch
            {
                Console.WriteLine("Unknown error. Please try set another resulting file name.");
            }
            #endregion

            var numberOfCompressorThreads = Environment.ProcessorCount;
            var sharedStateAfterReadFile = ReaderCompressorSharedState.Create(numberOfCompressorThreads, c_readFileChunkSize);
            var sharedStateAfterCompression = CompressorWriterSharedState.Create(numberOfCompressorThreads, numberOfCompressorThreads * 2);
            var reader = new ReaderThread(source, c_readFileChunkSize, (IForReaderThread)sharedStateAfterReadFile);
            var compressors = Enumerable
                .Range(0, numberOfCompressorThreads)
                .Select(x => new CompressorThread(c_readFileChunkSize, (IForCompressorThreadInput)sharedStateAfterReadFile, (IForCompressorThreadOutput)sharedStateAfterCompression))
                .ToList();
            var writer = new WriterThread(destination, c_readFileChunkSize, (IForWriterThread)sharedStateAfterCompression);

            //Console.WriteLine("Press 'c' to cancel.");
            //if (Console.ReadKey(true).KeyChar == 'c')
            //{
            //    sharedStateAfterReadFile.Cancel();
            //}
            reader.Join();
            compressors.ForEach(x => x.Join());
            writer.Join();

            Console.WriteLine("Finished. Press enter to exit.");
            Console.ReadLine();
        }
    }
}
