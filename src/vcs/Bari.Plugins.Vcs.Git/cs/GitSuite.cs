using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Bari.Core.Generic;

namespace Bari.Plugins.Vcs.Git
{
    public class GitSuite
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(GitSuite));
        private static readonly Regex describeOutput = new Regex(@"^(.+)-(\d+)-g[0-9a-f]+$");

        private readonly IFileSystemDirectory suiteRoot;
        private readonly IEnvironmentVariableContext environmentVariableContext;

        public GitSuite([SuiteRoot] IFileSystemDirectory suiteRoot, IEnvironmentVariableContext environmentVariableContext)
        {
            this.suiteRoot = suiteRoot;
            this.environmentVariableContext = environmentVariableContext;
        }

        public bool IsAvailable
        {
            get
            {
                var localRoot = suiteRoot as LocalFileSystemDirectory;
                if (localRoot != null)
                {
                    if (Directory.Exists(Path.Combine(localRoot.AbsolutePath, ".git")))
                    {
                        if (IsGitAvailable())
                        {
                            log.InfoFormat("Git support initialized");
                            return true;
                        }
                        else
                        {
                            log.WarnFormat("Suite seems to be in a git repository but git is not available");
                        }
                    }
                }

                return false;
            }
        }

        private static bool IsGitAvailable()
        {
            try
            {
                var output = RunGit("", "--version");
                return output != null && output.StartsWith("git version ");
            }
            catch (Win32Exception)
            {
                return false;
            }
        }

        private static string RunGit(string root, string arguments)
        {
            using (var process = Process.Start(
                new ProcessStartInfo("git", arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = root
                }))
            {
                if (process != null)
                {
                    process.WaitForExit();
                    return process.StandardOutput.ReadToEnd();
                }
                else
                {
                    return null;
                }
            }
        }

        public void AddEnvironmentVariables()
        {
            var localRoot = suiteRoot as LocalFileSystemDirectory;
            if (localRoot != null)
            {
                // --long also describes the tagged commit itself (as <tag>-0-g<hash>), and --tags accepts
                // lightweight tags too.
                var output = RunGit(localRoot.AbsolutePath, "describe --tags --long");
                log.Debug(output);
                if (output != null)
                {
                    var match = describeOutput.Match(output.Trim());
                    if (match.Success)
                    {
                        environmentVariableContext.Define("GIT_TAG", match.Groups[1].Value);
                        environmentVariableContext.Define("GIT_REVNO", match.Groups[2].Value);
                    }
                }
            }
        }
    }
}