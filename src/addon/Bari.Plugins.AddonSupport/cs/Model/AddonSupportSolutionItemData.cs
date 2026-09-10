using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Bari.Core.Commands.Helper;
using Bari.Core.Model;

namespace Bari.Plugins.AddonSupport.Model
{
    public class AddonSupportSolutionItemData
    {
        private readonly ICommandTargetParser targetParser;
        private readonly IHasBuildTarget buildTargetSource;
        private readonly Goal activeGoal;
        private string targetName;

        public AddonSupportSolutionItemData(ICommandTargetParser targetParser, IHasBuildTarget buildTargetSource, Goal activeGoal)
        {
            this.targetParser = targetParser;
            this.buildTargetSource = buildTargetSource;
            this.activeGoal = activeGoal;
        }

        public string BariPath
        {
            get
            {
                var assemblyPath = Assembly.GetEntryAssembly().Location;
                var appHost = Path.ChangeExtension(assemblyPath, OperatingSystem.IsWindows() ? ".exe" : null);
                return File.Exists(appHost) ? appHost : assemblyPath;
            }
        }

        public string Goal
        {
            get
            {
                return activeGoal.Name;
            }
        }

        public string Target
        {
            get
            {
                return targetName ?? (targetName = FindOutCurrentTarget());
            }
        }

        public string StartupPath
        {
            get
            {
                return FindOutResultExecutablePath(Target);
            }
        }


        private string FindOutResultExecutablePath(string targetStr)
        {
            IEnumerable<Project> exeProjects = new List<Project>();

            var target = targetParser.ParseTarget(targetStr);
            var productTarget = target as ProductTarget;
            if (productTarget != null)
            {
                if (productTarget.Product.HasParameters("startup"))
                {
                    var startupParams = productTarget.Product.GetParameters<StartupModuleParameters>("startup");
                    exeProjects = startupParams.Projects;
                }
            }

            if (!exeProjects.Any())
            {
                return GetName(target.Projects.FirstOrDefault(
                        prj => prj.Type == ProjectType.Executable || prj.Type == ProjectType.WindowsExecutable));
            }

            return exeProjects.Select(GetName).Aggregate(new StringBuilder(), (p, n) =>
            {
                if (p.Length > 0)
                    p.Append(",");
                p.Append(n);
                return p;
            }).ToString();
        }

        private string GetName(Project project)
        {
            if (project == null)
                return String.Empty;

            return Path.Combine(project.RelativeTargetPath, project.Name + ".exe");
        }

        private string FindOutCurrentTarget()
        {
            if (buildTargetSource != null)
                return buildTargetSource.BuildTarget;
            else
                return String.Empty;
        }
    }
}
