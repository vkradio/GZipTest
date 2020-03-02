namespace GZipTest.Compression.SharedState
{
    /// <summary>
    /// Ограничивает управление совместным состоянием для потока, который может отменять работу
    /// </summary>
    interface ICancellable
    {
        /// <summary>
        /// Отменить выполняемую работу
        /// </summary>
        void Cancel();
    }
}
