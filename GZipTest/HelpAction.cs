using System;

namespace GZipTest
{
    class HelpAction : Action
    {
        public override void Execute() =>
            Console.WriteLine("usage: GZipTest.exe compress/decompress [source file name] [result file name]");
    }
}
