using System;
using System.Diagnostics;
using System.Threading;

namespace GZipTest.Compression.SharedState
{
    class CompressorWriterSharedState : ICancellable, IForCompressorThreadOutput, IForWriterThread
    {
        /// <summary>
        /// События для &quot;производителя&quot;
        /// </summary>
        enum EventsForProducer
        {
            /// <summary>
            /// Свободное место существует
            /// </summary>
            SpaceExists = -1,
            /// <summary>
            /// Свободное место появилось
            /// </summary>
            SpaceAppeared,
            /// <summary>
            /// Отмена
            /// </summary>
            Cancel
        }

        /// <summary>
        /// События для &quot;потребителя&quot;
        /// </summary>
        enum EventsForConsumer
        {
            /// <summary>
            /// В очереди есть пакеты
            /// </summary>
            PacketsExists = -1,
            /// <summary>
            /// В очереди появился пакет
            /// </summary>
            PacketAppeared,
            /// <summary>
            /// Работа всех потоков &quot;производителя&quot; завершена
            /// </summary>
            ProductionCompleted,
            /// <summary>
            /// Отмена
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

        #region Публичный интерфейс.
        public static ICancellable Create(int numberOfCompressorThreads, int queueLimit) =>
            new CompressorWriterSharedState(numberOfCompressorThreads, queueLimit);

        /// <summary>
        /// Отменить выполняемую работу
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
        /// Постановка в очередь сжатого &quot;пакета&quot;
        /// </summary>
        /// <param name="filePacket">Кусочек сжатого файла</param>
        /// <param name="cancel">Признак отмены задачи</param>
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
                // Если очередь заполнена, сваливаемся в ожидание освобождения места.
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
                        if (queue.EnqueueAddedFirstAvailableItem) // Если это первый доступный пакет в очереди, ставим сигнал для потребителей.
                            eventForFirstPacketInQueue.Set();
                        if (eventType == EventsForProducer.SpaceAppeared && !queue.HaveSpace())
                            eventForNewSpaceInQueue.Reset(); // Сбрасываем свой сигнал появления места.
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
        /// Работа (одного) потока компрессии завершена
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
        /// Извлечение очередного кусочка сжатого файла
        /// </summary>
        /// <returns>Пакет с частью сжатого файла, либо null, если файл кончился</returns>
        /// <param name="cancel">Признак отмены задачи</param>
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
                // Если очередь пуста, сваливаемся в ожидание пополнения очереди.
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

                // Если очередь пуста и все потоки компрессора завершили работу, значит
                // и мы тоже завершаем работу.
                if (queue.CountContiguous == 0 && activeCompressorThreads == 0)
                    return result; // Пустой результат.

                switch (eventType)
                {
                    case EventsForConsumer.PacketsExists:
                    case EventsForConsumer.PacketAppeared:
                        var hasSpace = queue.HaveSpace();
                        result = queue.Dequeue();
                        if (!hasSpace && queue.HaveSpace()) // Если в очереди появилось одно свободное место, сигналим об этом производителю.
                            eventForNewSpaceInQueue.Set();
                        if (eventType == EventsForConsumer.PacketAppeared && queue.CountContiguous == 0)
                            eventForFirstPacketInQueue.Reset(); // Сбрасываем свой сигнал появления пакета.
                        break;
                    case EventsForConsumer.ProductionCompleted:
                        // Ничего не делаем, просто вернем внизу ранее инициализированный пустой результат.
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
