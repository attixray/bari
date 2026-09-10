using Bari.Core.Model;
using Bari.Core.UI;

namespace Bari.Core.Commands
{
    public class SelfUpdateCommand : ICommand
    {
        private readonly IUserOutput output;

        public SelfUpdateCommand(IUserOutput output)
        {
            this.output = output;
        }

        public string Name { get { return "selfupdate"; } }
        public string Description { get { return "shows how to update this Bari build"; } }
        public string Help
        {
            get
            {
                return @"=Self update command=

This .NET 10 build must be updated by rebuilding the source with bootstrap.ps1
or installing a matching .NET 10 distribution. Legacy NuGet releases cannot
update this runtime and its plugins.
";
            }
        }
        public bool NeedsExplicitTargetGoal { get { return false; } }

        public bool Run(Suite suite, string[] parameters)
        {
            output.Warning("Automatic self-update is unavailable for this .NET 10 build. Rebuild with bootstrap.ps1 or install a matching .NET 10 distribution.");
            return false;
        }
    }
}
