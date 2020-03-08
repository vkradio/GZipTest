namespace GZipTest.Compression.SharedState
{
    /// <summary>
    /// Restricting control of shared state for thread which compresses file, on it&apos;s input
    /// </summary>
    interface IForCompressorThreadOutput
    {
        /// <summary>
        /// Setting compressed packet to the queye
        /// </summary>
        /// <param name="filePacket">Piece of compressed file</param>
        /// <param name="cancel">Cancellation flag</param>
        void Produce(FilePacketMemoryStream filePacket, out bool cancel);

        /// <summary>
        /// Compressing thread has finished it&apos;s work
        /// </summary>
        void CompressionCompleted();
    }
}
