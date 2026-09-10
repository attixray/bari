using System;

namespace Bari.Core.Exceptions
{
    /// <summary>
    /// Thrown when the build graph contains a dependency cycle and cannot be executed.
    /// </summary>
    public class BuildGraphCycleException : Exception
    {
        public BuildGraphCycleException()
            : base("Build graph contains a dependency cycle.")
        {
        }
    }
}
