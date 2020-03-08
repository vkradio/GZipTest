using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace GZipTest.Compression.SharedState
{
    /// <summary>
    /// Shared state for reading and compressing threads
    /// </summary>
    class ReaderCompressorSharedState : ICancellable, IForReaderThread, IForCompressorThreadInput
    {
        /// <summary>
        /// Events for &quot;producer&quot;
        /// </summary>
        enum EventsForProducer
        {
            /// <summary>
            /// There is free space
            /// </summary>
            SpaceExists = -1,
            /// <summary>
            /// The free space has been appeared
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
            /// There are packets in the queue
            /// </summary>
            PacketsExists = -1,
            /// <summary>
            /// Packet has been appeared in the queue
            /// </summary>
            PacketAppeared,
            /// <summary>
            /// File read completed
            /// </summary>
            ReadCompleted,
            /// <summary>
            /// Cancel
            /// </summary>
            Cancel
        }

        #region State of the queue
        readonly int queueDepth;
        readonly FilePacketArray[] packetQueue;
        int queueFilledCount;
        readonly int packetSize;
        int nextBufIndexToProduce; // Next buffer for produce
        int nextBufIndexToConsume; // Next buffer for read
        #endregion

        readonly Mutex mutex = new Mutex(); // Mutex for protecting the overall state
        readonly ManualResetEvent eventForNewSpaceInQueue = new ManualResetEvent(false);
        readonly ManualResetEvent eventForFirstPacketInQueue = new ManualResetEvent(false);
        readonly ManualResetEvent eventForInputCompletion = new ManualResetEvent(false);
        readonly ManualResetEvent eventForWorkCancellation = new ManualResetEvent(false);
        readonly ManualResetEvent[] eventsForProducer = new ManualResetEvent[2];
        readonly ManualResetEvent[] eventsForConsumer = new ManualResetEvent[3];
        bool readCompleted;
        bool cancelled;

        ReaderCompressorSharedState(int queueDepth, int packetSize)
        {
            this.queueDepth = queueDepth;
            this.packetSize = packetSize;
            packetQueue = new FilePacketArray[queueDepth];
            for (var i = 0; i < queueDepth; i++)
                packetQueue[i] = new FilePacketArray(0, packetSize);

            eventsForProducer[(int)EventsForProducer.SpaceAppeared] = eventForNewSpaceInQueue;
            eventsForProducer[(int)EventsForProducer.Cancel] = eventForWorkCancellation;

            eventsForConsumer[(int)EventsForConsumer.PacketAppeared] = eventForFirstPacketInQueue;
            eventsForConsumer[(int)EventsForConsumer.ReadCompleted] = eventForInputCompletion;
            eventsForConsumer[(int)EventsForConsumer.Cancel] = eventForWorkCancellation;
        }

        void Enqueue(FilePacketArray filePacket)
        {
            filePacket.CopyTo(packetQueue[nextBufIndexToProduce]);
            nextBufIndexToProduce++;
            if (nextBufIndexToProduce == queueDepth)
                nextBufIndexToProduce = 0;
            queueFilledCount++;
        }

        FilePacketMemoryStream Dequeue()
        {
            var buf = new byte[packetQueue[nextBufIndexToConsume].FilledLength];
            System.Buffer.BlockCopy(packetQueue[nextBufIndexToConsume].Buffer, 0, buf, 0, buf.Length);
            var result = new FilePacketMemoryStream
            {
                Index = packetQueue[nextBufIndexToConsume].Index,
                MemoryStream = new MemoryStream(buf)
            };
            nextBufIndexToConsume++;
            if (nextBufIndexToConsume == queueDepth)
                nextBufIndexToConsume = 0;
            queueFilledCount--;
            return result;
        }

        #region Public interface
        public static ICancellable Create(int queueDepth, int packetSize) =>
            new ReaderCompressorSharedState(queueDepth, packetSize);

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
        /// Saving &quot;packet&quot; of file
        /// </summary>
        /// <param name="filePacket">Piece of source file</param>
        /// <param name="cancel">Cancellation flag</param>
        public void Produce(FilePacketArray filePacket, out bool cancel)
        {
            Debug.Assert(filePacket != null, $"{nameof(filePacket)} is null");
            Debug.Assert(filePacket.Index >= 0, $"{nameof(filePacket)}.{nameof(filePacket.Index)} must be not negative zero-based index");

            var taken = true;
            mutex.WaitOne();
            try
            {
                cancel = cancelled;
                if (cancelled)
                    return;

                var eventType = EventsForProducer.SpaceExists;
                // If queue is full, start awaiting for free space
                while (queueFilledCount == queueDepth)
                {
                    taken = false;
                    mutex.ReleaseMutex();

                    eventType = (EventsForProducer)WaitHandle.WaitAny(eventsForProducer);
                    mutex.WaitOne();
                    taken = true;

                    if ((eventType == EventsForProducer.SpaceAppeared && queueFilledCount != queueDepth) ||
                        eventType == EventsForProducer.Cancel)
                    {
                        break;
                    }
                }
                switch (eventType)
                {
                    case EventsForProducer.SpaceExists:
                    case EventsForProducer.SpaceAppeared:
                        Enqueue(filePacket);
                        if (queueFilledCount == 1) // If it is a first packet in queue, setting signal for consumers
                            eventForFirstPacketInQueue.Set();
                        if (eventType == EventsForProducer.SpaceAppeared && queueFilledCount == queueDepth)
                            eventForNewSpaceInQueue.Reset(); // Resetting our signal about appearing of free space
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
        /// Reading thread finished it&apos;s work
        /// </summary>
        public void ReadCompleted()
        {
            mutex.WaitOne();
            try
            {
                readCompleted = true;
                eventForInputCompletion.Set();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Consuming the next piece of uncompressed file
        /// </summary>
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
                    return result;

                var eventType = EventsForConsumer.PacketsExists;
                // If queue if empty, begin to await for it's replenishment
                while (queueFilledCount == 0 && !readCompleted)
                {
                    taken = false;
                    mutex.ReleaseMutex();

                    eventType = (EventsForConsumer)WaitHandle.WaitAny(eventsForConsumer);
                    mutex.WaitOne();
                    taken = true;

                    if ((eventType == EventsForConsumer.PacketAppeared && queueFilledCount != 0) ||
                        eventType == EventsForConsumer.ReadCompleted ||
                        eventType == EventsForConsumer.Cancel)
                    {
                        break;
                    }
                }
                if (queueFilledCount == 0 && readCompleted)
                    return result;
                switch (eventType)
                {
                    case EventsForConsumer.PacketsExists:
                    case EventsForConsumer.PacketAppeared:
                        result = Dequeue();
                        if (queueFilledCount == queueDepth - 1) // If there is new free space appeared in the queue, signaling producer about it
                            eventForNewSpaceInQueue.Set();
                        if (eventType == EventsForConsumer.PacketAppeared && queueFilledCount == 0)
                            eventForFirstPacketInQueue.Reset(); // Resetting our signal about new packet appering
                        break;
                    case EventsForConsumer.ReadCompleted:
                        // Do nothing, just below we'll return the empty pre-initialized result
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
