namespace GZipTest.Compression.SharedState
{
    /// <summary>
    /// Ограничивает управление совместным состоянием для потока, сжимающего файл, на его входе
    /// </summary>
    interface IForCompressorThreadOutput
    {
        /// <summary>
        /// Постановка в очередь сжатого &quot;пакета&quot;
        /// </summary>
        /// <param name="filePacket">Кусочек сжатого файла</param>
        /// <param name="cancel">Признак отмены задачи</param>
        void Produce(FilePacketMemoryStream filePacket, out bool cancel);

        /// <summary>
        /// Работа потока компрессии завершена
        /// </summary>
        void CompressionCompleted();
    }
}
