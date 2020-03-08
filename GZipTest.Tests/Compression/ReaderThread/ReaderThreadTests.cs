using System;
using System.Collections.Generic;
using System.IO;
using GZipTest.Compression;
using GZipTest.Compression.SharedState;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace GZipTest.Tests.Compression.ReaderThread
{
    [TestClass]
    public class ReaderThreadTests
    {
        const int c_packetSize = 4194304;

        TestContext testContextInstance;
        /// <summary>
        /// Gets or sets the test context which provides information about and functionality for the current test run
        /// </summary>
        public TestContext TestContext { get => testContextInstance; set => testContextInstance = value; }

        delegate void ProduceCallback(FilePacketArray filePacket, out bool cancel);

        string GetAssetPath(string file) => Path.Combine(Path.GetDirectoryName(TestContext.TestDir), "..\\", "GZipTest.Tests", "TestAssets", file);

        [TestMethod]
        public void Read_Empty_File()
        {
            // arrange
            var packets = new List<FilePacketArray>();
            var mock = new Mock<IForReaderThread>();
            mock.Setup(m => m.Produce(It.IsAny<FilePacketArray>(), out It.Ref<bool>.IsAny))
                .Callback(new ProduceCallback((FilePacketArray p, out bool cancel) =>
                {
                    packets.Add(new FilePacketArray(p));
                    cancel = false;
                }));

            // act
            var file = GetAssetPath("EmptyFile.txt");
            var reader = new GZipTest.Compression.Threads.ReaderThread(file, c_packetSize, mock.Object);
            reader.Join(100);

            // assert
            Assert.AreEqual(1, packets.Count, $"After reading empty file we expect 1 packet, but received: {packets.Count}.");
            Assert.IsNotNull(packets[0].Buffer, "After reading empty file the single buffer is null");
            Assert.AreEqual(c_packetSize, packets[0].Buffer.Length, $"After reading empty file the single buffer's size should be equal to {c_packetSize} bytes, but instead is equal to: {packets[0].Buffer.Length} bytes");
            Assert.AreEqual(0, packets[0].FilledLength, $"After reading empty file the filled lenght of the single buffer should be equal to 0, but instead it equal to: {packets[0].FilledLength}");
            Assert.AreEqual(0, packets[0].Index, $"After reading empty file the single packet shoud have an index of 0, but instead it has a value: {packets[0].Index}");
        }

        [TestMethod]
        public void Read_File_Less_Than_Single_Packet()
        {
            // arrange
            var packets = new List<FilePacketArray>();
            var mock = new Mock<IForReaderThread>();
            mock.Setup(m => m.Produce(It.IsAny<FilePacketArray>(), out It.Ref<bool>.IsAny))
                .Callback(new ProduceCallback((FilePacketArray p, out bool cancel) =>
                {
                    packets.Add(new FilePacketArray(p));
                    cancel = false;
                }));

            // act
            var file = GetAssetPath("FileSize578bytes.txt");
            var reader = new GZipTest.Compression.Threads.ReaderThread(file, c_packetSize, mock.Object);
            reader.Join(100);

            // assert
            Assert.AreEqual(1, packets.Count, $"After reading a file we expect 1 packet, but received: {packets.Count}.");
            Assert.IsNotNull(packets[0].Buffer, "After reading a file the single buffer is null");
            Assert.AreEqual(c_packetSize, packets[0].Buffer.Length, $"After reading a file the size of single buffer should be equal to {c_packetSize} bytes, but instead in equal to: {packets[0].Buffer.Length} bytes");
            Assert.AreEqual(578, packets[0].FilledLength, $"After reading a file having 578 bytes of size, the filled lenght of single buffer should be 578, but instead it has a value: {packets[0].FilledLength}");
            Assert.AreEqual(0, packets[0].Index, $"After reading a file the single packet should have an index of 0, but instead it has a value: {packets[0].Index}");
        }

        [TestMethod]
        public void Read_File_4_Packets()
        {
            // arrange
            var packets = new List<FilePacketArray>();
            var mock = new Mock<IForReaderThread>();
            mock.Setup(m => m.Produce(It.IsAny<FilePacketArray>(), out It.Ref<bool>.IsAny))
                .Callback(new ProduceCallback((FilePacketArray p, out bool cancel) =>
                {
                    packets.Add(new FilePacketArray(p));
                    cancel = false;
                }));

            // act
            var file = GetAssetPath("FileSize13224960bytes.txt");
            var reader = new GZipTest.Compression.Threads.ReaderThread(file, c_packetSize, mock.Object);
            reader.Join(100);

            // assert
            Assert.AreEqual(4, packets.Count, $"After reading a file expected 4 packets, but received: {packets.Count}.");
            for (var i = 0; i < 4; i++)
            {
                Assert.IsNotNull(packets[i].Buffer, $"After reading a file the buffer {i} is null");
                Assert.AreEqual(c_packetSize, packets[i].Buffer.Length, $"After reading a file the size of buffer {i} should be equal {c_packetSize} bytes, but instead it equal to: {packets[i].Buffer.Length} bytes");
                if (i != 3)
                    Assert.AreEqual(c_packetSize, packets[i].FilledLength, $"After reading a file having 13224960 bytes of size, the filled length of buffer {i} should be equal to {c_packetSize}, but instead it equals: {packets[i].FilledLength}");
                Assert.AreEqual(i, packets[i].Index, $"After reading a file the packet {i} should has an index {i}, but instead index equals to: {packets[i].Index}");
            }
            Assert.AreEqual(13224960 - c_packetSize * 3, packets[3].FilledLength, $"After reading a file having a size of 13224960 bytes, the filled size of buffer 3 should be equal to 642048, but instead it equals to: {packets[3].FilledLength}");
        }

        [TestMethod]
        public void Method_ReadCompleted()
        {
            // arrange
            Mock<IForReaderThread> mock = new Mock<IForReaderThread>();
            mock.Setup(m => m.ReadCompleted());

            // act
            var file = GetAssetPath("EmptyFile.txt");
            var reader = new GZipTest.Compression.Threads.ReaderThread(file, c_packetSize, mock.Object);
            reader.Join(100);

            // assert
            mock.Verify(r => r.ReadCompleted(), Times.Once(), "ReadCompleted should be called 1 time.");
        }
    }
}
