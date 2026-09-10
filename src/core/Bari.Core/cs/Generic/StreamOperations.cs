using System;
using System.IO;

namespace Bari.Core.Generic
{
    public static class StreamOperations
    {
        /// <summary>
        /// Copies a stream to another one
        /// </summary>
        /// <param name="source">Source stream</param>
        /// <param name="target">Target stream</param>
        public static void Copy(Stream source, Stream target)
        {   
            source.CopyTo(target);
        }
    }
}
