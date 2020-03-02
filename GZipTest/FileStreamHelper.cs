using System.IO;

namespace GZipTest
{
    static class FileStreamHelper
    {
        public static void CopyTo(this Stream input, Stream output)
        {
            var buffer = new byte[16 * 1024];
            int bytesRead;

            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                output.Write(buffer, 0, bytesRead);
        }
    }
}
