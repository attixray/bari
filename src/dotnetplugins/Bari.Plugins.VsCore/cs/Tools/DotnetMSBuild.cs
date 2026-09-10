using System;
using System.Diagnostics;
using System.IO;
using Bari.Core.Generic;
using Bari.Core.UI;
using Bari.Plugins.VsCore.Exceptions;

namespace Bari.Plugins.VsCore.Tools
{
    /// <summary>Builds SDK projects using the installed .NET SDK.</summary>
    public sealed class DotnetMSBuild : IMSBuild
    {
        private readonly IParameters parameters;

        public DotnetMSBuild(IParameters parameters)
        {
            this.parameters = parameters;
        }

        public void Run(IFileSystemDirectory root, string relativePath, bool restore)
        {
            var localRoot = root as LocalFileSystemDirectory;
            if (localRoot == null)
                throw new NotSupportedException("Only local file system is supported for MSBuild!");

            // Separate invocations let the build re-evaluate the imports written by restore.
            if (restore)
                RunTarget(localRoot.AbsolutePath, relativePath, "Restore");
            RunTarget(localRoot.AbsolutePath, relativePath, "Build");
        }

        private void RunTarget(string root, string relativePath, string target)
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "msbuild", Path.GetFullPath(Path.Combine(root, relativePath)),
                "-nologo", "-m", "-nr:false", "-t:" + target,
                "-verbosity:" + (parameters.VerboseOutput ? "normal" : "minimal") })
                startInfo.ArgumentList.Add(argument);

            using (var process = Process.Start(startInfo))
            {
                process.OutputDataReceived += (sender, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
                process.ErrorDataReceived += (sender, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new MSBuildFailedException();
            }
        }
    }
}
