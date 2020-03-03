using System;
using System.IO;

namespace GZipTest
{
    abstract class ActionWithSourceAndDestination : Action
    {
        protected enum FileType
        {
            File,
            Directory,
            NotExists
        }

        protected string source;
        protected string destination;
        protected FileType sourceType;

        protected FileType GetFileType(string file)
        {
            try
            {
                if (File.Exists(file))
                    return FileType.File;
                else if (Directory.Exists(file))
                    return FileType.Directory;
                else
                    return FileType.NotExists;
            }
            catch
            {
                return FileType.NotExists;
            }
        }

        protected abstract void ExecuteConcrete();

        public ActionWithSourceAndDestination(string src, string dest)
        {
            source = src;
            destination = dest;
        }

        public override void Execute()
        {
            sourceType = GetFileType(source);
            if (sourceType == FileType.NotExists)
            {
                Console.WriteLine($"File not exists: {source}");
                return;
            }
            if (source.Equals(destination, StringComparison.CurrentCultureIgnoreCase) && destination.EndsWith(".gz", StringComparison.CurrentCultureIgnoreCase))
            {
                Console.WriteLine("Names of source and result files should differ.");
                return;
            }
            ExecuteConcrete();
        }
    };
}
