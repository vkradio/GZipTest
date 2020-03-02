namespace GZipTest.Compression.SharedState
{
    /// <summary>
    /// Ограничивает управление совместным состоянием для потока, сжимающего файл, на его входе
    /// </summary>
    interface IForCompressorThreadInput
    {
        /// <summary>
        /// Извлечение очередного кусочка несжатого файла
        /// </summary>
        /// <returns>Пакет с частью файла, либо null, если файл кончился</returns>
        /// <param name="cancel">Признак отмены задачи</param>
        FilePacketMemoryStream Consume(out bool cancel);
    }
}
