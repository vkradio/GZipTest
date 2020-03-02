using GZipTest.Compression.SharedState;
using System;
using System.IO;
using System.Threading;

namespace GZipTest.Compression.Threads
{
    /// <summary>
    /// Поток записи сжатого файла
    /// </summary>
    class WriterThread
    {
        readonly string file;
        readonly int bufSize;
        readonly IForWriterThread inputState;
        readonly Thread thread;

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
        /// Удаление части сжатого файла в случае отмены, с глушением исключений
        /// </summary>
        void SafeDeleteFile()
        {
            if (File.Exists(file))
                try
                {
                    File.Delete(file);
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
        /// <param name="inputState">Разделяемое с потоком компрессии состояние</param>
        public WriterThread(string file, int bufSize, IForWriterThread inputState)
        {
            this.file = file;
            this.bufSize = bufSize;
            this.inputState = inputState;
            thread = new Thread(RunThread) { IsBackground = true, Name = "Writer" };
            thread.Start();
        }

        /// <summary>
        /// Исполняемое тело потока
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
                Console.WriteLine("У вас нет прав на запись в данную папку.");
            }
            catch (PathTooLongException)
            {
                SafeCancel();
                SafeDeleteFile();
                Console.WriteLine("Путь к файлу слишком велик. Пожалуйста, задайте более короткое имя файла.");
            }
            catch (DirectoryNotFoundException)
            {
                SafeCancel();
                SafeDeleteFile();
                Console.WriteLine("Папка назначения не существует.");
            }
            catch (NotSupportedException)
            {
                SafeCancel();
                SafeDeleteFile();
                Console.WriteLine("Недопустимые символы в имени папки или файла назначения.");
            }
            catch (IOException)
            {
                SafeCancel();
                SafeDeleteFile();
                Console.WriteLine("Не удалось записать файл. Возможно, закончилось место на диске или диск отключен.");
            }
            catch (Exception ex)
            {
                SafeCancel();
                SafeDeleteFile();
                Console.WriteLine("В потоке чтения файла произошла ошибка: " + ex.ToString());
            }
        }

        /// <summary>
        /// Ожидание завершения потока
        /// </summary>
        public void Join() => thread.Join();
    }
}
