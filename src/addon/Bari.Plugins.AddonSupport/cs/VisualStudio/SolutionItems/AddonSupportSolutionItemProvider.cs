using System;
using System.Collections.Generic;
using Bari.Core.Commands;
using Bari.Core.Commands.Helper;
using Bari.Core.Generic;
using Bari.Core.Model;
using Bari.Plugins.AddonSupport.Model;
using Bari.Plugins.VsCore.VisualStudio.SolutionItems;

namespace Bari.Plugins.AddonSupport.VisualStudio.SolutionItems
{
    public class AddonSupportSolutionItemProvider : ISolutionItemProvider
    {
        private readonly IFileSystemDirectory targetRoot;
        private readonly Suite suite;
        private readonly ICommand currentCommand;
        private readonly ICommandTargetParser targetParser;

        public AddonSupportSolutionItemProvider([TargetRoot] IFileSystemDirectory targetRoot, Suite suite, [Current] ICommand currentCommand, ICommandTargetParser targetParser)
        {
            this.targetRoot = targetRoot;
            this.suite = suite;
            this.currentCommand = currentCommand;
            this.targetParser = targetParser;
        }

        public IEnumerable<TargetRelativePath> GetItems(string solutionName)
        {
            var path = GenerateAddonSupportFile(solutionName);

            return new[] { path };
        }

        private TargetRelativePath GenerateAddonSupportFile(string solutionName)
        {
            var path = new TargetRelativePath("", solutionName + ".yaml");
            // Include the line break the file has always ended with, or the comparison never
            // matches and the file is rewritten on every run.
            targetRoot.UpdateTextFile(path.RelativePath, AddonSupportData() + Environment.NewLine);
            return path;
        }

        private string AddonSupportData()
        {
            var data = new AddonSupportSolutionItemData(targetParser, currentCommand as IHasBuildTarget, suite.ActiveGoal);

            return String.Format(@"---
bari-path: {0}
goal: {1}
target: {2}
startup-path: {3}
", data.BariPath, data.Goal, data.Target, data.StartupPath);
        }

    }
}
