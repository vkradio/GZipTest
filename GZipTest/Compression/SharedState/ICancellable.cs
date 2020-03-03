namespace GZipTest.Compression.SharedState
{
    /// <summary>
    /// Limiting management of the shared state for the thread which can cancel the work
    /// </summary>
    interface ICancellable
    {
        /// <summary>
        /// Cancel work
        /// </summary>
        void Cancel();
    }
}
