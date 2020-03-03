using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace GZipTest.Compression
{
    /// <summary>
    /// Ordering queue
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
            if (!canEnqueue) // If limit is filled, we can add to queue, only if the new index limited by lower range edge
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
            else // If not, then add, and then resort the storage
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
        /// Overall element count
        /// </summary>
        public int Count { get => storage.Count; }

        /// <summary>
        /// Count of elements in queue, which we can pull in ordered manner
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
        /// Now we can get an object from the queue, but it was not possible before placing the last object
        /// </summary>
        public bool EnqueueAddedFirstAvailableItem { get => enqueueAddedFirstAvailableItem; }
    }
}
