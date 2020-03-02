namespace GZipTest.Compression.SharedState
{
    /// <summary>
    /// Ограничивает управление совместным состоянием для потока, записываюего файл
    /// </summary>
    interface IForWriterThread
    {
        /// <summary>
        /// Извлечение очередного кусочка сжатого файла
        /// </summary>
        /// <returns>Пакет с частью сжатого файла, либо null, если файл кончился</returns>
        /// <param name="cancel">Признак отмены задачи</param>
        FilePacketMemoryStream Consume(out bool cancel);
    }
}
