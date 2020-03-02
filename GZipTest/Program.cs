using GZipTest.Compression;
using GZipTest.Decompression;
using System;

namespace GZipTest
{
    class Program
    {
        static int Main(string[] args)
        {
            Action action;

            if (args == null || args.Length == 0 || args.Length != 3 || args[0] == "-?" || args[0] == "/?" || args[0] == "--help")
                action = new HelpAction();
            else if (args[0].Equals("compress", StringComparison.CurrentCultureIgnoreCase))
                action = new CompressAction(args[1], args[2]);
            else if (args[0].Equals("decompress", StringComparison.CurrentCultureIgnoreCase))
                action = new DecompressAction(args[1], args[2]);
            else
                action = new HelpAction();

            action.Execute();

            return 0; // -1 если ошибка.
        }
    }
}
