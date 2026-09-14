using Bari.Core.Model;
using Bari.Core.Model.Parameters;

namespace Bari.Plugins.VsCore.Model
{
    public class MSBuildParameters: IProjectParameters
    {
        public MSBuildVersion Version { get; set; }

        public bool Restore { get; set; }

        /// <summary>
        /// If enabled, files below subdirectories of a module's output directory are also
        /// exposed as build results and copied into product outputs. Disabled by default
        /// for compatibility with pre-.NET 10 Bari versions.
        /// </summary>
        public bool IncludeOutputSubdirectories { get; set; }

        public MSBuildParameters()
        {
            Version = MSBuildVersion.Net40x86;
        }
    }
}
