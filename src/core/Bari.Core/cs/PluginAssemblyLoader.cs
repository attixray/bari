using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Bari.Core
{
    /// <summary>Loads plugins into Bari's shared type context using their dependency manifests.</summary>
    public static class PluginAssemblyLoader
    {
        private static readonly Dictionary<string, AssemblyDependencyResolver> resolvers = new Dictionary<string, AssemblyDependencyResolver>();
        private static readonly object sync = new object();

        static PluginAssemblyLoader()
        {
            AssemblyLoadContext.Default.Resolving += ResolveAssembly;
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += (assembly, name) =>
            {
                foreach (var resolver in GetResolvers())
                {
                    var path = resolver.ResolveUnmanagedDllToPath(name);
                    if (path != null)
                        return System.Runtime.InteropServices.NativeLibrary.Load(path);
                }
                return IntPtr.Zero;
            };
        }

        public static Assembly Load(string path)
        {
            path = Path.GetFullPath(path);
            lock (sync)
            {
                if (!resolvers.ContainsKey(path))
                    resolvers.Add(path, new AssemblyDependencyResolver(path));
            }
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        }

        private static AssemblyDependencyResolver[] GetResolvers()
        {
            lock (sync)
                return resolvers.Values.ToArray();
        }

        private static Assembly ResolveAssembly(AssemblyLoadContext context, AssemblyName name)
        {
            foreach (var resolver in GetResolvers())
            {
                var path = resolver.ResolveAssemblyToPath(name);
                if (path != null)
                    return context.LoadFromAssemblyPath(path);
            }

            // Bari products merge their modules into one directory, including legacy
            // file references which have no dependency manifest of their own.
            var localPath = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
            return File.Exists(localPath) ? context.LoadFromAssemblyPath(localPath) : null;
        }
    }
}
