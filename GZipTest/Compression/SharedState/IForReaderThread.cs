namespace GZipTest.Compression.SharedState
{
    /// <summary>
    /// Restricting control of shared state for thread which reads file
    /// </summary>
    public interface IForReaderThread
    {
        /// <summary>
        /// Saving &quot;packet&quot; of file
        /// </summary>
        /// <param name="filePacket">Piece of source file</param>
        /// <param name="cancel">Cancellation token</param>
        void Produce(FilePacketArray filePacket, out bool cancel);

        /// <summary>
        /// Reading thread has finished it&apos;s work
        /// </summary>
        void ReadCompleted();
    }
}
