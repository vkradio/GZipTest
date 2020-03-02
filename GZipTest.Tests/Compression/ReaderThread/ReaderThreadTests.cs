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
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
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
            Assert.AreEqual(1, packets.Count, $"После чтения пустого файла ожидается 1 пакет, а получено: {packets.Count}.");
            Assert.IsNotNull(packets[0].Buffer, "После чтения пустого файла единственный буфер равен null");
            Assert.AreEqual(c_packetSize, packets[0].Buffer.Length, $"После чтения пустого файла размер единственного буфера должен быть равен {c_packetSize} байт, вместо этого равен: {packets[0].Buffer.Length} байт");
            Assert.AreEqual(0, packets[0].FilledLength, $"После чтения пустого файла заполненная длина единственного буфера должна быть равна 0, вместо этого равна: {packets[0].FilledLength}");
            Assert.AreEqual(0, packets[0].Index, $"После чтения пустого файла единственный пакет должен иметь индекс, равный 0, вместо этого индекс равен: {packets[0].Index}");
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
            Assert.AreEqual(1, packets.Count, $"После чтения файла ожидается 1 пакет, а получено: {packets.Count}.");
            Assert.IsNotNull(packets[0].Buffer, "После чтения файла единственный буфер равен null");
            Assert.AreEqual(c_packetSize, packets[0].Buffer.Length, $"После чтения файла размер единственного буфера должен быть равен {c_packetSize} байт, вместо этого равен: {packets[0].Buffer.Length} байт");
            Assert.AreEqual(578, packets[0].FilledLength, $"После чтения файла размером 578 байт заполненная длина единственного буфера должна быть равна 578, вместо этого равна: {packets[0].FilledLength}");
            Assert.AreEqual(0, packets[0].Index, $"После чтения файла единственный пакет должен иметь индекс, равный 0, вместо этого индекс равен: {packets[0].Index}");
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
            Assert.AreEqual(4, packets.Count, $"После чтения файла ожидается 4 пакета, а получено: {packets.Count}.");
            for (var i = 0; i < 4; i++)
            {
                Assert.IsNotNull(packets[i].Buffer, $"После чтения файла буфер {i} равен null");
                Assert.AreEqual(c_packetSize, packets[i].Buffer.Length, $"После чтения файла размер буфера {i} должен быть равен {c_packetSize} байт, вместо этого равен: {packets[i].Buffer.Length} байт");
                if (i != 3)
                    Assert.AreEqual(c_packetSize, packets[i].FilledLength, $"После чтения файла размером 13224960 байт заполненная длина буфера {i} должна быть равна {c_packetSize}, вместо этого равна: {packets[i].FilledLength}");
                Assert.AreEqual(i, packets[i].Index, $"После чтения файла пакет {i} должен иметь индекс {i}, вместо этого индекс равен: {packets[i].Index}");
            }
            Assert.AreEqual(13224960 - c_packetSize * 3, packets[3].FilledLength, $"После чтения файла размером 13224960 байт заполненная длина буфера 3 должна быть равна 642048, вместо этого равна: {packets[3].FilledLength}");
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
            mock.Verify(r => r.ReadCompleted(), Times.Once(), "ReadCompleted должен быть вызван 1 раз.");
        }
    }
}
