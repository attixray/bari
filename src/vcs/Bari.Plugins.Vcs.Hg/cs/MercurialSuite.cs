using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Bari.Core.Generic;

namespace Bari.Plugins.Vcs.Hg
{
    public class MercurialSuite
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(MercurialSuite));
        private readonly IFileSystemDirectory suiteRoot;
        private readonly IEnvironmentVariableContext environmentVariableContext;

        public MercurialSuite([SuiteRoot] IFileSystemDirectory suiteRoot, IEnvironmentVariableContext environmentVariableContext)
        {
            this.suiteRoot = suiteRoot;
            this.environmentVariableContext = environmentVariableContext;
        }

        public bool IsAvailable
        {
            get
            {
                var localRoot = suiteRoot as LocalFileSystemDirectory;
                if (localRoot == null || !Directory.Exists(Path.Combine(localRoot.AbsolutePath, ".hg")))
                    return false;

                try
                {
                    RunHg(localRoot.AbsolutePath, "--version");
                    log.Info("Mercurial support initialized");
                    return true;
                }
                catch (Win32Exception ex)
                {
                    log.WarnFormat("Could not start Mercurial: {0}", ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    log.WarnFormat("Could not initialize Mercurial: {0}", ex.Message);
                }
                return false;
            }
        }

        public void AddEnvironmentVariables()
        {
            var localRoot = suiteRoot as LocalFileSystemDirectory;
            if (localRoot == null)
                return;

            var revision = RunHg(localRoot.AbsolutePath, "log", "-r", ".", "--template", "{rev}").Trim();
            // The null revision is -1 for an empty repository.
            int.Parse(revision, CultureInfo.InvariantCulture);
            environmentVariableContext.Define("HG_REVNO", revision);
        }

        private static string RunHg(string root, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo("hg")
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            // Machine-readable output without local aliases, defaults or translated messages.
            startInfo.Environment["HGPLAIN"] = "1";
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using (var process = Process.Start(startInfo))
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Mercurial failed: " + error.GetAwaiter().GetResult());
                return output.GetAwaiter().GetResult();
            }
        }
    }
}
