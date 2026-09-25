using Bari.Core.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using Bari.Core.Test.Helper;
using System.IO;

namespace Bari.Core.Test.Generic
{
    [TestFixture]
    public class FileSystemDirectoryExtensionsTest
    {
        [Test]
        public void GetRelativePathFromTest()
        {
            // r 
            // -> a
            //   -> c.txt
            // -> a.test
            //   -> e.txt
            // -> b
            //   -> d.txt

            var r = new Mock<IFileSystemDirectory>();
            var a = new Mock<IFileSystemDirectory>();
            var atest = new Mock<IFileSystemDirectory>();
            var b = new Mock<IFileSystemDirectory>();

            r.Setup(dir => dir.GetRelativePath(a.Object)).Returns(@"a");
            r.Setup(dir => dir.GetRelativePath(atest.Object)).Returns(@"a.test");
            r.Setup(dir => dir.GetRelativePath(b.Object)).Returns(@"b");

            r.Object.GetRelativePathFrom(a.Object, Path.Combine("a", "c.txt")).Should().Be(@"c.txt");
            r.Object.GetRelativePathFrom(a.Object, Path.Combine("a.test", "e.txt")).Should().Be(Path.Combine("..", "a.test", "e.txt"));
            r.Object.GetRelativePathFrom(a.Object, Path.Combine("b", "d.txt")).Should().Be(Path.Combine("..", "b", "d.txt"));
        }

        [Test]
        public void UpdateTextFileLeavesUnchangedFilesAlone()
        {
            using (var tmp = new TempDirectory())
            {
                var dir = new LocalFileSystemDirectory(tmp);
                var file = Path.Combine(tmp, "App.csproj");
                var written = new System.DateTime(2020, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);

                dir.UpdateTextFile("App.csproj", "<Project />").Should().BeTrue();
                File.SetLastWriteTimeUtc(file, written);

                dir.UpdateTextFile("App.csproj", "<Project />").Should().BeFalse();
                File.GetLastWriteTimeUtc(file).Should().Be(written);

                dir.UpdateTextFile("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />").Should().BeTrue();
                File.ReadAllText(file).Should().Be("<Project Sdk=\"Microsoft.NET.Sdk\" />");
            }
        }

        [Test]
        public void Utf8StringWriterDeclaresUtf8()
        {
            using (var output = new Utf8StringWriter())
            {
                using (var writer = System.Xml.XmlWriter.Create(output))
                    writer.WriteElementString("Project", "");

                output.ToString().Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            }
        }
    }
}