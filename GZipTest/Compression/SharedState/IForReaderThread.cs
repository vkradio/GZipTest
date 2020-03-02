namespace GZipTest.Compression.SharedState
{
    /// <summary>
    /// Ограничивает управление совместным состоянием для потока, читающего файл
    /// </summary>
    public interface IForReaderThread
    {
        /// <summary>
        /// Сохранение прочитанного &quot;пакета&quot; файла
        /// </summary>
        /// <param name="filePacket">Кусочек исходного файла</param>
        /// <param name="cancel">Признак отмены задачи</param>
        void Produce(FilePacketArray filePacket, out bool cancel);

        /// <summary>
        /// Работа потока чтения завершена
        /// </summary>
        void ReadCompleted();
    }
}
