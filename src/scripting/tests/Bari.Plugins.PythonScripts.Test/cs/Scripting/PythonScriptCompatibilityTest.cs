using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Bari.Plugins.PythonScripts.Scripting;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using NUnit.Framework;

namespace Bari.Plugins.PythonScripts.Test.Scripting
{
    [TestFixture]
    public class PythonScriptCompatibilityTest
    {
        private ScriptEngine engine;
        private ScriptScope scope;
        private MemoryStream output;
        private readonly List<string> temporaryFiles = new List<string>();

        [SetUp]
        public void SetUp()
        {
            engine = PythonScriptCompatibility.CreateEngine();
            scope = engine.CreateScope();
            output = new MemoryStream();
            engine.Runtime.IO.SetOutput(output, Encoding.UTF8);
        }

        [TearDown]
        public void TearDown()
        {
            engine.Runtime.Shutdown();
            output.Dispose();
            foreach (var path in temporaryFiles)
                File.Delete(path);
            temporaryFiles.Clear();
        }

        private void Run(string source)
        {
            PythonScriptCompatibility.Execute(engine, scope, source, "compatibility-test.py");
        }

        private string TempFile()
        {
            var path = Path.GetTempFileName();
            temporaryFiles.Add(path);
            scope.SetVariable("path", path);
            return path;
        }

        [Test]
        public void LegacyPrintAndPython3PrintFunctionsBothWork()
        {
            Run("print 'GPU forcing added'\n");
            Run("print('one', 'two')\n");
            Assert.That(Encoding.UTF8.GetString(output.ToArray()).Replace("\r\n", "\n"),
                Is.EqualTo("GPU forcing added\none two\n"));
        }

        [Test]
        public void RedirectedPrintAndOldStringIOModuleWork()
        {
            Run("import StringIO\nsink = StringIO.StringIO()\nprint >>sink, 'message'\nresult = sink.getvalue()\n");
            Assert.That(scope.GetVariable<string>("result").Trim(), Is.EqualTo("message"));
        }

        [Test]
        public void LegacyDictionaryIteratorsWorkWithoutChangingModernViews()
        {
            Run("d = {'a': 1}\nresult = next(d.iteritems())\nview = d.keys()\nd['b'] = 2\ncount = len(view)\n");
            Assert.That(scope.GetVariable<int>("count"), Is.EqualTo(2));
            Assert.That(engine.Execute<bool>("result == ('a', 1)", scope), Is.True);
        }

        [Test]
        public void WindowsPathsPreserveTruncatedHexEscapesAndRealEscapes()
        {
            Run("path_part = '\\x' + '64\\ActivatorHidden'\nraw = r'\\x'\nescaped = '\\\\x'\nvalid = '\\x41\\u00e9'\n");
            Assert.That(scope.GetVariable<string>("path_part"), Is.EqualTo(@"\x64\ActivatorHidden"));
            Assert.That(scope.GetVariable<string>("raw"), Is.EqualTo(@"\x"));
            Assert.That(scope.GetVariable<string>("escaped"), Is.EqualTo(@"\x"));
            Assert.That(scope.GetVariable<string>("valid"), Is.EqualTo("Aé"));
        }

        [Test]
        public void CodeLookingTextInStringsAndCommentsIsUnchanged()
        {
            Run("# print >> stream\ntext = 'print value; data.iteritems()'\nprint('one', 'two')\n");
            Assert.That(scope.GetVariable<string>("text"), Is.EqualTo("print value; data.iteritems()"));
            Assert.That(Encoding.UTF8.GetString(output.ToArray()).Trim(), Is.EqualTo("one two"));
        }

        [Test]
        public void XrangeWorksWithoutChangingPython3RangeObjects()
        {
            Run("total = sum(xrange(4))\nmodern = range(4)\n");
            Assert.That(scope.GetVariable<int>("total"), Is.EqualTo(6));
            Assert.That(engine.Execute<string>("type(modern).__name__", scope), Is.EqualTo("range"));
        }

        [Test]
        public void ElementTreeCanWriteUtf8BytesToLegacyTextFile()
        {
            var path = TempFile();
            Run("import xml.etree.ElementTree as ET\nroot = ET.Element('settings', {'value': 'árvíz'})\nwith open(path, 'w') as f:\n    ET.ElementTree(root).write(f, encoding='utf-8', xml_declaration=True)\nresult = ET.parse(path).getroot().get('value')\n");
            Assert.That(scope.GetVariable<string>("result"), Is.EqualTo("árvíz"));
            Assert.That(File.ReadAllText(path), Does.Contain("encoding='utf-8'"));
        }

        [Test]
        public void SettingsEditsRoundTripNonUtf8Bytes()
        {
            var path = TempFile();
            File.WriteAllBytes(path, new byte[] { 111, 108, 100, 32, 233 });
            Run("with open(path, 'r') as f:\n    text = f.read()\nwith open(path, 'w') as f:\n    f.write(text.replace('old', 'new'))\n");
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(new byte[] { 110, 101, 119, 32, 233 }));
        }

        [Test]
        public void BinaryFilesAndExplicitEncodingsKeepPython3Behavior()
        {
            var path = TempFile();
            File.WriteAllBytes(path, new byte[] { 233 });
            Run("with open(path, 'rb') as f:\n    raw = f.read()\nfailed = False\ntry:\n    with open(path, encoding='utf-8') as f:\n        f.read()\nexcept UnicodeDecodeError:\n    failed = True\n");
            Assert.That(engine.Execute<bool>("raw == b'\\xe9'", scope), Is.True);
            Assert.That(scope.GetVariable<bool>("failed"), Is.True);
        }

        [Test]
        public void RuntimeFailureDoesNotExecuteTheScriptAgain()
        {
            var calls = 0;
            scope.SetVariable("record", (Action)(() => calls++));
            Assert.Throws<InvalidOperationException>(() => Run("from System import InvalidOperationException\nrecord()\nraise InvalidOperationException('expected')\n"));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void SyntaxErrorsRemainErrorsAndKeepTheirLineNumbers()
        {
            var error = Assert.Throws<SyntaxErrorException>(() => Run("# header\nif :\n    pass\n"));
            Assert.That(error.Line, Is.EqualTo(2));
        }
    }
}
