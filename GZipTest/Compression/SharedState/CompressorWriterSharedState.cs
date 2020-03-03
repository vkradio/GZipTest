using System;
using System.Diagnostics;
using System.Threading;

namespace GZipTest.Compression.SharedState
{
    class CompressorWriterSharedState : ICancellable, IForCompressorThreadOutput, IForWriterThread
    {
        /// <summary>
        /// Events for &quot;producer&quot;
        /// </summary>
        enum EventsForProducer
        {
            /// <summary>
            /// Free space exists
            /// </summary>
            SpaceExists = -1,
            /// <summary>
            /// Free space appeared
            /// </summary>
            SpaceAppeared,
            /// <summary>
            /// Cancel
            /// </summary>
            Cancel
        }

        /// <summary>
        /// Events for &quot;consumer&quot;
        /// </summary>
        enum EventsForConsumer
        {
            /// <summary>
            /// There are packed in the queue
            /// </summary>
            PacketsExists = -1,
            /// <summary>
            /// New packet appeared in the queue
            /// </summary>
            PacketAppeared,
            /// <summary>
            /// All &quot;producer&quot; threads completed their work
            /// </summary>
            ProductionCompleted,
            /// <summary>
            /// Cancel
            /// </summary>
            Cancel
        }

        readonly OrderingQueue queue;
        readonly int numberOfCompressorThreads;
        private bool cancelled;
        int activeCompressorThreads;
        readonly Mutex mutex = new Mutex();
        readonly ManualResetEvent eventForNewSpaceInQueue = new ManualResetEvent(false);
        readonly ManualResetEvent eventForFirstPacketInQueue = new ManualResetEvent(false);
        readonly ManualResetEvent eventForInputCompletion = new ManualResetEvent(false);
        readonly ManualResetEvent eventForWorkCancellation = new ManualResetEvent(false);
        readonly ManualResetEvent[] eventsForProducer = new ManualResetEvent[2];
        readonly ManualResetEvent[] eventsForConsumer = new ManualResetEvent[3];

        CompressorWriterSharedState(int numberOfCompressorThreads, int queueLimit)
        {
            this.numberOfCompressorThreads = numberOfCompressorThreads;
            queue = new OrderingQueue(queueLimit);
            activeCompressorThreads = numberOfCompressorThreads;

            eventsForProducer[(int)EventsForProducer.SpaceAppeared] = eventForNewSpaceInQueue;
            eventsForProducer[(int)EventsForProducer.Cancel] = eventForWorkCancellation;

            eventsForConsumer[(int)EventsForConsumer.PacketAppeared] = eventForFirstPacketInQueue;
            eventsForConsumer[(int)EventsForConsumer.ProductionCompleted] = eventForInputCompletion;
            eventsForConsumer[(int)EventsForConsumer.Cancel] = eventForWorkCancellation;
        }

        #region Public interface
        public static ICancellable Create(int numberOfCompressorThreads, int queueLimit) =>
            new CompressorWriterSharedState(numberOfCompressorThreads, queueLimit);

        /// <summary>
        /// Cancel work
        /// </summary>
        public void Cancel()
        {
            mutex.WaitOne();
            try
            {
                cancelled = true;
                eventForWorkCancellation.Set();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Putting compressed &quot;packet&quot; into the queue
        /// </summary>
        /// <param name="filePacket">Piece of compressed file</param>
        /// <param name="cancel">Cancellation flag</param>
        public void Produce(FilePacketMemoryStream filePacket, out bool cancel)
        {
            Debug.Assert(filePacket != null, $"{nameof(filePacket)} is null");

            var taken = true;
            mutex.WaitOne();
            try
            {
                cancel = cancelled;
                if (cancelled)
                    return;

                var eventType = EventsForProducer.SpaceExists;
                // If queue is full, begin to await for freeing space
                while (!queue.CanEnqueue(filePacket))
                {
                    taken = false;
                    mutex.ReleaseMutex();

                    eventType = (EventsForProducer)WaitHandle.WaitAny(eventsForProducer);
                    mutex.WaitOne();
                    taken = true;

                    if ((eventType == EventsForProducer.SpaceAppeared && queue.CanEnqueue(filePacket)) ||
                        eventType == EventsForProducer.Cancel)
                    {
                        break;
                    }
                }
                switch (eventType)
                {
                    case EventsForProducer.SpaceExists:
                    case EventsForProducer.SpaceAppeared:
                        queue.Enqueue(filePacket);
                        if (queue.EnqueueAddedFirstAvailableItem) // If it is a first awailable packet in the queue, setting the signal to the consumer
                            eventForFirstPacketInQueue.Set();
                        if (eventType == EventsForProducer.SpaceAppeared && !queue.HaveSpace())
                            eventForNewSpaceInQueue.Reset(); // Resetting our signal of new space appearance
                        break;
                    case EventsForProducer.Cancel:
                        cancel = true;
                        break;
                    default:
                        Debug.Write($"Unsupported event type for producer: {Enum.GetName(typeof(EventsForProducer), eventType)}");
                        break;
                }
            }
            finally
            {
                if (taken)
                    mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// The work of one comression thread completed
        /// </summary>
        public void CompressionCompleted()
        {
            mutex.WaitOne();
            try
            {
                activeCompressorThreads--;
                if (activeCompressorThreads == 0)
                    eventForInputCompletion.Set();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Get the next piece of the compressed file
        /// </summary>
        /// <returns>Packet containing part of the compressed file, or null, if file is ended</returns>
        /// <param name="cancel">Cancellation flag</param>
        public FilePacketMemoryStream Consume(out bool cancel)
        {
            FilePacketMemoryStream result = null;

            var taken = true;
            mutex.WaitOne();
            try
            {
                cancel = cancelled;
                if (cancel)
                    return result; // Пустой результат.

                var eventType = EventsForConsumer.PacketsExists;
                // If queue is empty, begin to await when it's filled
                while (queue.CountContiguous == 0 && activeCompressorThreads != 0)
                {
                    taken = false;
                    mutex.ReleaseMutex();

                    eventType = (EventsForConsumer)WaitHandle.WaitAny(eventsForConsumer);
                    mutex.WaitOne();
                    taken = true;

                    if ((eventType == EventsForConsumer.PacketAppeared && queue.CountContiguous != 0) ||
                        eventType == EventsForConsumer.ProductionCompleted ||
                        eventType == EventsForConsumer.Cancel)
                    {
                        break;
                    }
                }

                // If queue is empty and all compressor threads finished their work, we are finishing our work too
                if (queue.CountContiguous == 0 && activeCompressorThreads == 0)
                    return result; // Empty result

                switch (eventType)
                {
                    case EventsForConsumer.PacketsExists:
                    case EventsForConsumer.PacketAppeared:
                        var hasSpace = queue.HaveSpace();
                        result = queue.Dequeue();
                        if (!hasSpace && queue.HaveSpace()) // If one new free space appeared in the queue, send signal to the producer
                            eventForNewSpaceInQueue.Set();
                        if (eventType == EventsForConsumer.PacketAppeared && queue.CountContiguous == 0)
                            eventForFirstPacketInQueue.Reset(); // Resetting our signal about packet appearing
                        break;
                    case EventsForConsumer.ProductionCompleted:
                        // Do nothing, just returning the pre-initialized empty result below
                        break;
                    case EventsForConsumer.Cancel:
                        cancel = true;
                        break;
                    default:
                        Debug.Write($"Unsupported event type for consumer: {Enum.GetName(typeof(EventsForConsumer), eventType)}");
                        break;
                }
            }
            finally
            {
                if (taken)
                    mutex.ReleaseMutex();
            }

            return result;
        }
        #endregion
    }
}
