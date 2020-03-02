using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace GZipTest.Compression
{
    /// <summary>
    /// Упорядочивающая очередь
    /// </summary>
    class OrderingQueue
    {
        public readonly int SoftLimit;
        int targetIndexForFirstPacket;
        readonly List<FilePacketMemoryStream> storage;
        bool enqueueAddedFirstAvailableItem;

        public OrderingQueue(int limit)
        {
            Debug.Assert(limit > 0, $"{nameof(limit)} must be positive number");

            SoftLimit = limit;
            storage = new List<FilePacketMemoryStream>(SoftLimit * 2);
        }

        public bool CanEnqueue(FilePacketMemoryStream item)
        {
            Debug.Assert(item != null, $"{nameof(item)} is null");

            var canEnqueue = storage.Count < SoftLimit;
            if (!canEnqueue) // Если лимит заполнен, всё же можно добавлять в очередь, если новый индекс входит в нижний диапазон.
                canEnqueue = item.Index < storage[storage.Count - 1].Index;
            return canEnqueue;
        }

        public bool HaveSpace() =>
            storage.Count < SoftLimit ||
            storage[0].Index != targetIndexForFirstPacket;

        public bool Enqueue(FilePacketMemoryStream item)
        {
            Debug.Assert(item != null, $"{nameof(item)} is null");

            var hadCountiguousItems = CountContiguous != 0;

            if (!CanEnqueue(item))
                return false;

            if (storage.Count != 0 && item.Index < storage[0].Index) // Если новый индекс меньше самого маленького, то просто вставляем его вперед.
            {
                storage.Insert(0, item);
            }
            else // Если же нет, то добавляем, а затем пересортируем хранилище.
            {
                storage.Add(item);
                storage.Sort((i1, i2) => i1.Index.CompareTo(i2.Index));
            }

            enqueueAddedFirstAvailableItem = !hadCountiguousItems && CountContiguous != 0;

            return true;
        }

        public FilePacketMemoryStream Dequeue()
        {
            enqueueAddedFirstAvailableItem = false;

            if (storage.Count != 0 && storage[0].Index == targetIndexForFirstPacket)
            {
                var result = storage[0];
                targetIndexForFirstPacket++;
                storage.RemoveAt(0);
                return result;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Общее количество элементов
        /// </summary>
        public int Count { get => storage.Count; }

        /// <summary>
        /// Количество элементов в очереди, которые можно упорядоченно извлечь
        /// </summary>
        public int CountContiguous
        {
            get
            {
                var count = 0;
                for (var next = targetIndexForFirstPacket;
                    count < storage.Count && storage[count].Index == next;
                    count++, next++)
                    ;
                return count;
            }
        }

        /// <summary>
        /// До последнего размещения объекта в очереди взять объект из очереди было нельзя, а теперь можно
        /// </summary>
        public bool EnqueueAddedFirstAvailableItem { get => enqueueAddedFirstAvailableItem; }
    }
}
