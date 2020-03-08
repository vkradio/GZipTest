namespace GZipTest.Compression.SharedState
{
    /// <summary>
    /// Restricting control of shared state for compressor thread at it&apos;s input
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
