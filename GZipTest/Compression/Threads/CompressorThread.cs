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
        /// Отмена с глушением исключений
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
        /// Конструктор
        /// </summary>
        /// <param name="inputBufSize">Размер буфера чтения</param>
        /// <param name="inputState">Разделяемое с потоком чтения состояние</param>
        /// <param name="debugMode">В режиме отладки не жмём блоки, а просто сохраняем их на диск в виде: номер_пакета-номер_потока.txt</param>
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
        /// Исполняемое тело потока
        /// </summary>
        void RunThread()
        {
            try
            {
                FilePacketMemoryStream notCompressedPacket;
                bool cancel;

                #region Нормальный вариант компрессии.
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
                                // Если получили сигнал отмены из выходного состояния, передаём его также во входное,
                                // затем выходим из потока.
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

                #region Отладочный вариант - вместо компрессии просто сохраняем пакеты в файлы с именем, состоящим из номера пакета и id потока.
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
                Console.WriteLine("В потоке компрессии произошла ошибка: " + ex.ToString());
            }

            outputState.CompressionCompleted();
        }

        /// <summary>
        /// Ожидание завершения потока
        /// </summary>
        public void Join() => thread.Join();
    }
}
