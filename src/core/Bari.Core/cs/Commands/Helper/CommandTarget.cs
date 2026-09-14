using System.Collections.Generic;
using Bari.Core.Model;
using System;
using System.Linq;
using Bari.Core.Exceptions;

namespace Bari.Core.Commands.Helper
{
    public abstract class CommandTarget
    {
        public abstract IEnumerable<Project> Projects { get; }
        public abstract IEnumerable<TestProject> TestProjects { get; }
    }

    public static class CommandTargetProjectExtensions
    {
        /// <summary>
        /// Expands a command target with every project reachable through build-time suite references.
        /// Keeping this complete closure in the top-level solution preserves transitive project references
        /// for module and project builds in the same way as product builds.
        /// </summary>
        public static IEnumerable<Project> WithBuildDependencies(this IEnumerable<Project> projects)
        {
            var result = new List<Project>();
            var visited = new HashSet<Project>();
            var pending = new Queue<Project>(projects);

            while (pending.Count > 0)
            {
                var project = pending.Dequeue();
                if (!visited.Add(project))
                    continue;

                result.Add(project);

                foreach (var reference in project.References.Where(r => r.Type == ReferenceType.Build))
                {
                    var referencedProject = ResolveSuiteProject(project, reference);
                    if (referencedProject != null && !visited.Contains(referencedProject))
                        pending.Enqueue(referencedProject);
                }
            }

            return result;
        }

        private static Project ResolveSuiteProject(Project project, Reference reference)
        {
            var suite = project.Module.Suite;

            switch (reference.Uri.Scheme)
            {
                case "module":
                    return ResolveProject(project.Module, reference.Uri.Host);

                case "suite":
                    var moduleName = reference.Uri.Host;
                    if (!suite.HasModule(moduleName))
                        throw new InvalidReferenceException(String.Format("Suite has no module called {0}", moduleName));

                    return ResolveProject(suite.GetModule(moduleName), reference.Uri.AbsolutePath.TrimStart('/'));

                default:
                    return null;
            }
        }

        private static Project ResolveProject(Module module, string projectName)
        {
            var project = module.GetProjectOrTestProject(projectName);
            if (project == null)
                throw new InvalidReferenceException(String.Format("Module {0} has no project called {1}", module.Name, projectName));

            return project;
        }
    }
}
