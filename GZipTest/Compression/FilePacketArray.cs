using System.Diagnostics;

namespace GZipTest.Compression
{
    /// <summary>
    /// Packet containing part of the file (implementation using an array)
    /// </summary>
    public class FilePacketArray
    {
        public int Index;
        public readonly byte[] Buffer;
        public int FilledLength;

        public FilePacketArray(int packetIndex, int initSize)
        {
            Debug.Assert(initSize >= 0, $"{nameof(initSize)} must be positive number");

            Index = packetIndex;
            Buffer = new byte[initSize];
        }

        public FilePacketArray(FilePacketArray copyFrom)
        {
            Index = copyFrom.Index;
            Buffer = new byte[copyFrom.Buffer.Length];
            System.Buffer.BlockCopy(copyFrom.Buffer, 0, Buffer, 0, copyFrom.FilledLength);
            FilledLength = copyFrom.FilledLength;
        }

        public void CopyTo(FilePacketArray destination)
        {
            Debug.Assert(destination.Buffer != null, $"{nameof(destination)}.{nameof(destination.Buffer)} is null");
            Debug.Assert(destination.Buffer.Length >= FilledLength, $"{nameof(destination)}.{nameof(destination.Buffer)} could not be less than source ${nameof(FilledLength)}");

            destination.Index = Index;
            System.Buffer.BlockCopy(Buffer, 0, destination.Buffer, 0, FilledLength);
            destination.FilledLength = FilledLength;
        }

        public void IncIndex() => Index++;
    }
}
