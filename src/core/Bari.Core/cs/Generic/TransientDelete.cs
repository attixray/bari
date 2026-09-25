using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Bari.Core.Generic
{
    /// <summary>
    /// Deletes files and directories, retrying while another process briefly holds them.
    ///
    /// <para>Tools that watch the suite (IDEs, language servers, virus scanners, MSBuild nodes) open
    /// generated project files and build outputs for a few milliseconds. A delete in that window fails
    /// with a sharing violation although a moment later it would succeed. If the file is still held
    /// after the retries, the error names the processes holding it.</para>
    /// </summary>
    public static class TransientDelete
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(TransientDelete));

        private const int ErrorAccessDenied = 5;
        private const int ErrorSharingViolation = 32;
        private const int ErrorLockViolation = 33;
        private const int ErrorDirNotEmpty = 145;

        private static readonly int[] retryDelays = { 50, 100, 200, 400, 800, 1600 };

        /// <summary>
        /// Deletes a file, retrying sharing violations
        /// </summary>
        /// <param name="path">Absolute path of the file</param>
        public static void DeleteFile(string path)
        {
            Run(() => File.Delete(path), path, isFile: true);
        }

        /// <summary>
        /// Deletes a directory recursively, retrying sharing violations and files appearing in it meanwhile
        /// </summary>
        /// <param name="path">Absolute path of the directory</param>
        public static void DeleteDirectory(string path)
        {
            Run(() => Directory.Delete(path, recursive: true), path, isFile: false);
        }

        private static void Run(Action delete, string path, bool isFile)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    delete();
                    return;
                }
                catch (Exception ex) when (IsTransient(ex) && attempt < retryDelays.Length)
                {
                    log.DebugFormat("Retrying delete of {0} after {1}: {2}", path, retryDelays[attempt], ex.Message);
                    Thread.Sleep(retryDelays[attempt]);
                }
                catch (IOException ex) when (isFile && IsSharingViolation(ex))
                {
                    var holders = DescribeHolders(path);
                    if (holders == null)
                        throw;
                    throw new IOException(String.Format("{0} Held by: {1}.", ex.Message, holders), ex);
                }
            }
        }

        private static bool IsTransient(Exception ex)
        {
            if (ex is UnauthorizedAccessException)
                return true; // also reported for files that are pending deletion

            var code = ex.HResult & 0xFFFF;
            return ex is IOException &&
                   (code == ErrorSharingViolation || code == ErrorLockViolation ||
                    code == ErrorDirNotEmpty || code == ErrorAccessDenied);
        }

        private static bool IsSharingViolation(IOException ex)
        {
            var code = ex.HResult & 0xFFFF;
            return code == ErrorSharingViolation || code == ErrorLockViolation;
        }

        /// <summary>
        /// Asks the Windows Restart Manager which processes have the given file open
        /// </summary>
        /// <param name="path">Absolute path of the file</param>
        /// <returns>Returns the holders as "name (pid)", comma separated, or <c>null</c> if they cannot be determined.</returns>
        public static string DescribeHolders(string path)
        {
            if (!OperatingSystem.IsWindows())
                return null;

            try
            {
                uint session;
                if (RmStartSession(out session, 0, new StringBuilder(SessionKeyLength + 1)) != 0)
                    return null;

                try
                {
                    if (RmRegisterResources(session, 1, new[] { path }, 0, null, 0, null) != 0)
                        return null;

                    uint needed;
                    uint count = 0;
                    uint reasons;
                    var result = RmGetList(session, out needed, ref count, null, out reasons);
                    if (result != ErrorMoreData || needed == 0)
                        return null;

                    var infos = new RmProcessInfo[needed];
                    count = needed;
                    if (RmGetList(session, out needed, ref count, infos, out reasons) != 0)
                        return null;

                    var names = new List<string>();
                    foreach (var info in infos.Take((int)count))
                        names.Add(String.Format("{0} ({1})", ProcessName(info), info.Process.ProcessId));
                    return names.Count > 0 ? String.Join(", ", names) : null;
                }
                finally
                {
                    RmEndSession(session);
                }
            }
            catch (Exception ex)
            {
                log.DebugFormat("Could not determine the holders of {0}: {1}", path, ex.Message);
                return null;
            }
        }

        private static string ProcessName(RmProcessInfo info)
        {
            try
            {
                using (var process = Process.GetProcessById(info.Process.ProcessId))
                    return process.ProcessName;
            }
            catch (ArgumentException)
            {
                return info.AppName;
            }
            catch (InvalidOperationException)
            {
                return info.AppName;
            }
        }

        private const int ErrorMoreData = 234;
        private const int SessionKeyLength = 32;

        [StructLayout(LayoutKind.Sequential)]
        private struct RmUniqueProcess
        {
            public int ProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RmProcessInfo
        {
            public RmUniqueProcess Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string AppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string ServiceShortName;
            public int ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)] public bool Restartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint sessionHandle, int sessionFlags, StringBuilder sessionKey);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint sessionHandle);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(uint sessionHandle, uint fileCount, string[] fileNames,
            uint applicationCount, RmUniqueProcess[] applications, uint serviceCount, string[] serviceNames);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(uint sessionHandle, out uint processInfoNeeded, ref uint processInfoCount,
            [In, Out] RmProcessInfo[] affectedApps, out uint rebootReasons);
    }
}
