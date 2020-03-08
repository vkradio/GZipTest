namespace GZipTest.Compression.SharedState
{
    /// <summary>
    /// Restricting control of shared state for thread which writes file
    /// </summary>
    interface IForWriterThread
    {
        /// <summary>
        /// Extracting of the next piece of compressed file
        /// </summary>
        /// <returns>Packet containing part of compressed file, or null, if file is ended</returns>
        /// <param name="cancel">Cancellation flag</param>
        FilePacketMemoryStream Consume(out bool cancel);
    }
}
