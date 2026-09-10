using Bari.Core.Generic;
using Bari.Core.Tools;
using Bari.Core.UI;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bari.Plugins.dotnet.Tools
{
    public class Dotnet : ExternalTool, Idotnet
    {
        private readonly IFileSystemDirectory targetDir;

        public Dotnet([TargetRoot] IFileSystemDirectory targetDir, IParameters parameters) : base("dotnet", parameters)
        {
            this.targetDir = targetDir;
        }

        protected override string ToolPath
        {
            get { return "dotnet"; }
        }

        protected override bool IsDotNETProcess
        {
            get { return false; }
        }

        public bool RunTests(IEnumerable<TargetRelativePath> testAssemblies)
        {
            var assemblies = testAssemblies.ToArray();
            if (assemblies.Length == 0)
                return true;
            var localTarget = targetDir as LocalFileSystemDirectory;
            if (localTarget == null)
                throw new NotSupportedException("Only local file system is supported for dotnet tests!");

            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = localTarget.AbsolutePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("vstest");
            foreach (var assembly in assemblies)
                startInfo.ArgumentList.Add(assembly);
            startInfo.ArgumentList.Add("/Logger:trx;LogFileName=bari-tests.trx");
            startInfo.ArgumentList.Add("/ResultsDirectory:" + System.IO.Path.Combine(localTarget.AbsolutePath, "test-results"));
            using (var process = Process.Start(startInfo))
            {
                process.OutputDataReceived += (sender, e) => { if (e.Data != null) System.Console.WriteLine(e.Data); };
                process.ErrorDataReceived += (sender, e) => { if (e.Data != null) System.Console.Error.WriteLine(e.Data); };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }

        protected override void EnsureToolAvailable()
        {

        }
    }
}
