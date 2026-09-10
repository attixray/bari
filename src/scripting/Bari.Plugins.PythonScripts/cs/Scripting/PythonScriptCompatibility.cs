using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using IronPython.Compiler;
using IronPython.Hosting;
using IronPython.Runtime;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;

namespace Bari.Plugins.PythonScripts.Scripting
{
    /// <summary>Runs existing suite scripts on IronPython 3 without changing their files.</summary>
    public static class PythonScriptCompatibility
    {
        private static readonly object translatorLock = new object();
        private static readonly Lazy<ScriptScope> translator = new Lazy<ScriptScope>(CreateTranslator);
        private static readonly ConcurrentDictionary<string, string> translatedSources = new ConcurrentDictionary<string, string>();
        private static readonly ConditionalWeakTable<ScriptEngine, ScriptScope> fileHelpers = new ConditionalWeakTable<ScriptEngine, ScriptScope>();

        public static ScriptEngine CreateEngine()
        {
            var engine = Python.CreateEngine();
            var root = Path.GetDirectoryName(typeof(PythonScriptCompatibility).Assembly.Location);
            engine.SetSearchPaths(new[] { Path.Combine(root, "lib") });
            return engine;
        }

        public static string PrepareSource(string source)
        {
            return translatedSources.GetOrAdd(source, text =>
            {
                lock (translatorLock)
                {
                    var scope = translator.Value;
                    return (string)scope.Engine.Operations.Invoke(scope.GetVariable("prepare"), text);
                }
            });
        }

        public static void Execute(ScriptEngine engine, ScriptScope scope, string source, string name)
        {
            var helper = fileHelpers.GetValue(engine, CreateFileHelper);
            scope.SetVariable("open", helper.GetVariable("compat_open"));
            var options = (PythonCompilerOptions)engine.GetCompilerOptions();
            options.Module |= ModuleOptions.Optimized;
            var script = engine.CreateScriptSourceFromString(PrepareSource(source), name, SourceCodeKind.File);
            // Execute the compiled result exactly once, also when execution fails.
            script.Compile(options).Execute(scope);
        }

        private static ScriptScope CreateTranslator()
        {
            var engine = CreateEngine();
            var scope = engine.CreateScope();
            engine.Execute(ReadResource("PythonCompatibility.py"), scope);
            return scope;
        }

        private static ScriptScope CreateFileHelper(ScriptEngine engine)
        {
            var scope = engine.CreateScope();
            engine.Execute(ReadResource("PythonFileCompatibility.py"), scope);
            return scope;
        }

        private static string ReadResource(string name)
        {
            using (var stream = typeof(PythonScriptCompatibility).Assembly.GetManifestResourceStream("Bari.Plugins.PythonScripts." + name))
            {
                if (stream == null)
                    throw new InvalidOperationException("Missing Python compatibility resource: " + name);
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
        }
    }
}
