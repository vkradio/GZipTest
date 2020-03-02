using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace GZipTest.Compression.SharedState
{
    /// <summary>
    /// Состояние, общее для потоков чтения и компрессии файла
    /// </summary>
    class ReaderCompressorSharedState : ICancellable, IForReaderThread, IForCompressorThreadInput
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
            /// Чтение файла завершено
            /// </summary>
            ReadCompleted,
            /// <summary>
            /// Отмена
            /// </summary>
            Cancel
        }

        #region Состояние очереди.
        readonly int queueDepth;
        readonly FilePacketArray[] packetQueue;
        int queueFilledCount;
        readonly int packetSize;
        int nextBufIndexToProduce; // Следующий буфер для заполнения
        int nextBufIndexToConsume; // Следующих буфер для чтения
        #endregion

        readonly Mutex mutex = new Mutex(); // Мутекс, защищающий всё состояние
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

        #region Публичный интерфейс.
        public static ICancellable Create(int queueDepth, int packetSize) =>
            new ReaderCompressorSharedState(queueDepth, packetSize);

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
        /// Сохранение прочитанного &quot;пакета&quot; файла
        /// </summary>
        /// <param name="filePacket">Кусочек исходного файла</param>
        /// <param name="cancel">Признак отмены задачи</param>
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
                // Если очередь заполнена, сваливаемся в ожидание освобождения места.
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
                        if (queueFilledCount == 1) // Если это первый пакет в очереди, ставим сигнал для потребителей.
                            eventForFirstPacketInQueue.Set();
                        if (eventType == EventsForProducer.SpaceAppeared && queueFilledCount == queueDepth)
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
        /// Работа потока чтения завершена
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
        /// Извлечение очередного кусочка несжатого файла
        /// </summary>
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
                    return result;

                var eventType = EventsForConsumer.PacketsExists;
                // Если очередь пуста, сваливаемся в ожидание пополнения очереди.
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
                        if (queueFilledCount == queueDepth - 1) // Если в очереди появилось одно свободное место, сигналим об этом производителю.
                            eventForNewSpaceInQueue.Set();
                        if (eventType == EventsForConsumer.PacketAppeared && queueFilledCount == 0)
                            eventForFirstPacketInQueue.Reset(); // Сбрасываем свой сигнал появления пакета.
                        break;
                    case EventsForConsumer.ReadCompleted:
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
