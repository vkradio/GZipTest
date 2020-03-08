namespace GZipTest.Compression.SharedState
{
    /// <summary>
    /// Restricting control of shared state for thread which can cancel the work
    /// </summary>
    interface ICancellable
    {
        /// <summary>
        /// Cancel work
        /// </summary>
        void Cancel();
    }
}
