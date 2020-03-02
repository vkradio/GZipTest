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
            #region Расширение архивного файла.
            try
            {
                var extension = Path.GetExtension(destination);
                if (!extension.Equals(".gz", StringComparison.CurrentCultureIgnoreCase))
                    destination += ".gz";
            }
            catch
            {
                Console.WriteLine("Имя результирующего файла содержит неверные символы.");
                return;
            }
            #endregion

            #region Проверка и перезапись существующего файла.
            try
            {
                if (File.Exists(destination))
                {
                    Console.WriteLine($"Файл {destination} существует. Перезаписать? y/n");
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
                            Console.WriteLine("У вас нет прав на перезапись файла, либо файл используется другим приложением.");
                            return;
                        }
                        catch (IOException)
                        {
                            Console.WriteLine("Файл используется другим приложением, поэтому его невозможно перезаписать.");
                            return;
                        }
                        catch
                        {
                            Console.WriteLine("Невозможно перезаписать файл. Скорее всего, вы задали неверное наименование.");
                            return;
                        }
                    }
                }
            }
            catch
            {
                Console.WriteLine("Произошла неизвестная ошибка. Попробуйте задать другое имя результирующего файла.");
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
