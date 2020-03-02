using System;

namespace GZipTest
{
    class HelpAction : Action
    {
        public override void Execute() =>
            Console.WriteLine("usage: GZipTest.exe compress/decompress [имя исходного файла] [имя результирующего файла]");
    }
}
