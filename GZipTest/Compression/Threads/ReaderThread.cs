using GZipTest.Compression.SharedState;
using System;
using System.IO;
using System.Threading;

namespace GZipTest.Compression.Threads
{
    /// <summary>
    /// Поток чтения файла
    /// </summary>
    public class ReaderThread
    {
        readonly string file;
        readonly int bufSize;
        readonly IForReaderThread outputState;
        readonly Thread thread;

        /// <summary>
        /// Отмена с глушением исключений
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
        /// Конструктор
        /// </summary>
        /// <param name="file">Путь к читаемому файлу</param>
        /// <param name="bufSize">Размер буфера чтения</param>
        /// <param name="outputState">Разделяемое с потоком компрессии состояние</param>
        public ReaderThread(string file, int bufSize, IForReaderThread outputState)
        {
            this.file = file;
            this.bufSize = bufSize;
            this.outputState = outputState;
            thread = new Thread(RunThread) { IsBackground = true, Name = "Reader" };
            thread.Start();
        }

        /// <summary>
        /// Исполняемое тело потока
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

                    if (count == 0) // Если файл пустой.
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
                Console.WriteLine("Не удалось прочитать файл.");
            }
            catch (Exception ex)
            {
                SafeCancel();
                Console.WriteLine("В потоке чтения файла произошла ошибка: " + ex.ToString());
            }
        }

        /// <summary>
        /// Ожидание завершения потока
        /// </summary>
        public void Join() => thread.Join();

        /// <summary>
        /// Ожидание завершения потока
        /// </summary>
        public void Join(int millisecondsTimeout) => thread.Join(millisecondsTimeout);
    }
}
