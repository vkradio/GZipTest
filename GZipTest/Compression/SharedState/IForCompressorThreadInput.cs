namespace GZipTest.Compression.SharedState
{
    /// <summary>
    /// Limiting management of the shared state for the compressor thread at its entry
    /// </summary>
    interface IForCompressorThreadInput
    {
        /// <summary>
        /// Extracting next piece of uncompressed file
        /// </summary>
        /// <returns>Packet containing piece of file, or null, if file is exhausted</returns>
        /// <param name="cancel">Cancellation flag</param>
        FilePacketMemoryStream Consume(out bool cancel);
    }
}
