using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GZipTest.Decompression
{
    class DecompressAction : ActionWithSourceAndDestination
    {
        public DecompressAction(string src, string dest) : base(src, dest) { }

        protected override void ExecuteConcrete()
        {
            throw new NotImplementedException();
        }

        public override void Execute()
        {
            throw new NotImplementedException();
        }
    }
}
