using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Bari.Core.Generic;
using Bari.Core.Test.Helper;
using Castle.Core.Resource;
using FluentAssertions;
using NUnit.Framework;

namespace Bari.Core.Test.Generic
{
    [TestFixture]
    public class LocalFileSystemDirectoryTest
    {       
        [Test]
        public void EmptyDirectoryLooksEmpty()
        {
            using (var tmp = new TempDirectory())
            {
                var dir = new LocalFileSystemDirectory(tmp);

                dir.ChildDirectories.Should().BeEmpty();
                dir.Files.Should().BeEmpty();
            }
        }

        [Test]
        public void FilesAreEnumerated()
        {
            using (var tmp = new TempDirectory())
            {
                using (File.Create(Path.Combine(tmp, "test1.txt"))) {}                
                using (File.Create(Path.Combine(tmp, "test2.bin"))) {}

                var dir = new LocalFileSystemDirectory(tmp);
                dir.ChildDirectories.Should().BeEmpty();
                dir.Files.Should().HaveCount(2);
                dir.Files.Should().Contain("test1.txt");
                dir.Files.Should().Contain("test2.bin");
            }
        }

        [Test]
        public void FilesAddedLaterAreEnumerated()
        {
            using (var tmp = new TempDirectory())
            {
                using (File.Create(Path.Combine(tmp, "test1.txt"))) {}
                var dir = new LocalFileSystemDirectory(tmp);
                using (File.Create(Path.Combine(tmp, "test2.bin"))) {}

                dir.ChildDirectories.Should().BeEmpty();
                dir.Files.Should().HaveCount(2);
                dir.Files.Should().Contain("test1.txt");
                dir.Files.Should().Contain("test2.bin");
            }
        }

        [Test]
        public void DirectoriesAreEnumerated()
        {
            using (var tmp = new TempDirectory())
            {
                Directory.CreateDirectory(Path.Combine(tmp, "dir1"));
                Directory.CreateDirectory(Path.Combine(tmp, "dir2"));

                var dir = new LocalFileSystemDirectory(tmp);
                dir.ChildDirectories.Should().HaveCount(2);
                dir.ChildDirectories.Should().Contain("dir1");
                dir.ChildDirectories.Should().Contain("dir2");
                dir.Files.Should().BeEmpty();
            }
        }

        [Test]
        public void DirectoriesAddedLaterAreEnumerated()
        {
            using (var tmp = new TempDirectory())
            {
                Directory.CreateDirectory(Path.Combine(tmp, "dir1"));
                var dir = new LocalFileSystemDirectory(tmp);
                Directory.CreateDirectory(Path.Combine(tmp, "dir2"));
                
                dir.ChildDirectories.Should().HaveCount(2);
                dir.ChildDirectories.Should().Contain("dir1");
                dir.ChildDirectories.Should().Contain("dir2");
                dir.Files.Should().BeEmpty();
            }
        }

        [Test]
        public void GetChildDirectoryWorks()
        {
            using (var tmp = new TempDirectory())
            {
                Directory.CreateDirectory(Path.Combine(tmp, "dir1"));
                var dir = new LocalFileSystemDirectory(tmp);
                var subdir = dir.GetChildDirectory("dir1");

                subdir.Should().NotBeNull();                
            }
        }

        [Test]
        public void GetChildDirectoryForNonExistingDirectoryReturnsNull()
        {
            using (var tmp = new TempDirectory())
            {
                var dir = new LocalFileSystemDirectory(tmp);
                var subdir = dir.GetChildDirectory("dir1");

                subdir.Should().BeNull();
            }
        }

        [Test]
        public void GetRelativePathWorks()
        {
            using (var tmp = new TempDirectory())
            {
                Directory.CreateDirectory(Path.Combine(tmp, "dir1"));                
                Directory.CreateDirectory(Path.Combine(tmp, "dir1", "dir2"));

                var dir = new LocalFileSystemDirectory(tmp);
                var dir1 = dir.GetChildDirectory("dir1");
                var dir2 = dir1.GetChildDirectory("dir2");

                dir1.Should().NotBeNull();
                dir2.Should().NotBeNull();

                dir.GetRelativePath(dir1).Should().Be("dir1");
                dir.GetRelativePath(dir2).Should().Be(Path.Combine("dir1", "dir2"));
            }
        }

        [Test]
        public void CreateDirectoryWorks()
        {
            using (var tmp = new TempDirectory())
            {
                var dir = new LocalFileSystemDirectory(tmp);
                dir.CreateDirectory("testdir");

                dir.ChildDirectories.Should().Contain("testdir");
                Directory.Exists(Path.Combine(tmp, "testdir")).Should().BeTrue();
            }
        }

        [Test]
        public void CreateDirectoryForExistingDirectoryIsNotAnError()
        {
            using (var tmp = new TempDirectory())
            {
                Directory.CreateDirectory(Path.Combine(tmp, "testdir"));

                var dir = new LocalFileSystemDirectory(tmp);
                dir.ChildDirectories.Should().Contain("testdir");

                dir.CreateDirectory("testdir");

                dir.ChildDirectories.Should().Contain("testdir");
                Directory.Exists(Path.Combine(tmp, "testdir")).Should().BeTrue();
            }
        }

        [Test]
        public void CreateTextFileWorks()
        {
            using (var tmp = new TempDirectory())
            {
                var dir = new LocalFileSystemDirectory(tmp);
                using (var writer = dir.CreateTextFile("test.txt"))
                    writer.WriteLine("Hello world");

                dir.Files.Should().Contain("test.txt");
                File.Exists(Path.Combine(tmp, "test.txt")).Should().BeTrue();
            }        
        }

        [Test]
        public void GetLastModifiedDateReturnsLastModifiedDateInUTC()
        {
            using (var tmp = new TempDirectory())
            {
                var dir = new LocalFileSystemDirectory(tmp);
                using (var writer = dir.CreateTextFile("test.txt"))
                    writer.WriteLine("Hello world");

                var lastWriteTime = File.GetLastWriteTimeUtc(Path.Combine(tmp, "test.txt"));
                var lastModifiedDate = dir.GetLastModifiedDate("test.txt");

                lastModifiedDate.Should().Be(lastWriteTime);
            }        
        }

        [Test]
        public void PartialDelete()
        {
            using (var tmp = new TempDirectory())
            {
                var dir = new LocalFileSystemDirectory(tmp);
                
                Directory.CreateDirectory(Path.Combine(tmp, "dir1"));
                Directory.CreateDirectory(Path.Combine(tmp, "dir1", "dir2"));
                using (var f = File.CreateText(Path.Combine(tmp, "dir1", "file.delete")))
                    f.WriteLine("test");
                using (var f = File.CreateText(Path.Combine(tmp, "dir1", "file.keep")))
                    f.WriteLine("test");
                using (var f = File.CreateText(Path.Combine(tmp, "dir1", "dir2", "file.delete")))
                    f.WriteLine("test");

                var paths = new HashSet<string>();

                dir.Delete(p =>
                {
                    paths.Add(p);
                    return false;
                });

                paths.Should().HaveCount(3);
                paths.Should().Contain(Path.Combine("dir1", "file.delete"));
                paths.Should().Contain(Path.Combine("dir1", "file.keep"));
                paths.Should().Contain(Path.Combine("dir1", "dir2", "file.delete"));

                Directory.Exists(tmp).Should().BeTrue();
                Directory.Exists(Path.Combine(tmp, "dir1")).Should().BeTrue();
                Directory.Exists(Path.Combine(tmp, "dir1", "dir2")).Should().BeTrue();
                File.Exists(Path.Combine(tmp, "dir1", "file.delete")).Should().BeTrue();
                File.Exists(Path.Combine(tmp, "dir1", "file.keep")).Should().BeTrue();
                File.Exists(Path.Combine(tmp, "dir1", "dir2", "file.delete")).Should().BeTrue();

                paths.Clear();
                dir.Delete(p =>
                {
                    paths.Add(p);
                    return !p.EndsWith(".keep", System.StringComparison.InvariantCulture);
                });

                paths.Should().HaveCount(4);
                paths.Should().Contain(Path.Combine("dir1", "file.delete"));
                paths.Should().Contain(Path.Combine("dir1", "file.keep"));
                paths.Should().Contain(Path.Combine("dir1", "dir2"));
                paths.Should().Contain(Path.Combine("dir1", "dir2", "file.delete"));

                Directory.Exists(tmp).Should().BeTrue();
                Directory.Exists(Path.Combine(tmp, "dir1")).Should().BeTrue();
                Directory.Exists(Path.Combine(tmp, "dir1", "dir2")).Should().BeFalse();
                File.Exists(Path.Combine(tmp, "dir1", "file.delete")).Should().BeFalse();
                File.Exists(Path.Combine(tmp, "dir1", "file.keep")).Should().BeTrue();
                File.Exists(Path.Combine(tmp, "dir1", "dir2", "file.delete")).Should().BeFalse();

                paths.Clear();
                dir.Delete(p =>
                {
                    paths.Add(p);
                    return true;
                });

                paths.Should().HaveCount(3);
                paths.Should().Contain(Path.Combine("dir1", "file.keep"));
                paths.Should().Contain("dir1");
                paths.Should().Contain("");

                Directory.Exists(tmp).Should().BeFalse();
            }
        }

        [Test]
        public void DeleteFileWaitsForABriefLock()
        {
            using (var tmp = new TempDirectory())
            {
                var file = Path.Combine(tmp, "held.csproj");
                File.WriteAllText(file, "<Project />");
                var reader = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                var release = Task.Run(() =>
                {
                    Thread.Sleep(300);
                    reader.Dispose();
                });

                var dir = new LocalFileSystemDirectory(tmp);
                dir.DeleteFile("held.csproj");

                release.Wait();
                File.Exists(file).Should().BeFalse();
            }
        }

        [Test]
        public void DeleteWaitsForABriefLockInTheTree()
        {
            using (var tmp = new TempDirectory())
            {
                var root = Path.Combine(tmp, "target");
                Directory.CreateDirectory(Path.Combine(root, "module"));
                var file = Path.Combine(root, "module", "held.dll");
                File.WriteAllText(file, "x");
                var reader = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                var release = Task.Run(() =>
                {
                    Thread.Sleep(300);
                    reader.Dispose();
                });

                var dir = new LocalFileSystemDirectory(root);
                dir.Delete();

                release.Wait();
                Directory.Exists(root).Should().BeFalse();
            }
        }

        [Test]
        public void DeleteFileNamesTheProcessHoldingIt()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Ignore("The Restart Manager is Windows only");

            using (var tmp = new TempDirectory())
            {
                var file = Path.Combine(tmp, "held.csproj");
                File.WriteAllText(file, "<Project />");
                using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var dir = new LocalFileSystemDirectory(tmp);
                    Action delete = () => dir.DeleteFile("held.csproj");

                    delete.Should().Throw<IOException>()
                        .WithMessage("*Held by: *" + System.Diagnostics.Process.GetCurrentProcess().ProcessName + " (" + Environment.ProcessId + ")*");
                }
                File.Exists(file).Should().BeTrue();
            }
        }

        [Test]
        public void DeleteGoesOnPastAHeldFileAndNamesItsHolder()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Ignore("Only Windows keeps an open file from being deleted");

            using (var tmp = new TempDirectory())
            {
                var root = Path.Combine(tmp, "target");
                Directory.CreateDirectory(Path.Combine(root, "a"));
                Directory.CreateDirectory(Path.Combine(root, "b"));
                var held = Path.Combine(root, "a", "held.dll");
                File.WriteAllText(held, "x");
                File.WriteAllText(Path.Combine(root, "a", "other.dll"), "x");
                File.WriteAllText(Path.Combine(root, "b", "third.dll"), "x");

                using (new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var dir = new LocalFileSystemDirectory(root);
                    Action delete = () => dir.Delete();

                    var failure = delete.Should().Throw<PartialDeleteException>().Which;
                    failure.Failures.Should().ContainSingle()
                        .Which.Should().Contain("held.dll").And.Contain("Held by: " + System.Diagnostics.Process.GetCurrentProcess().ProcessName + " (" + Environment.ProcessId + ")");
                }

                File.Exists(held).Should().BeTrue();
                File.Exists(Path.Combine(root, "a", "other.dll")).Should().BeFalse();
                Directory.Exists(Path.Combine(root, "b")).Should().BeFalse();
            }
        }

        [Test]
        public void PartialDeleteGoesOnPastAHeldFile()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Ignore("Only Windows keeps an open file from being deleted");

            using (var tmp = new TempDirectory())
            {
                Directory.CreateDirectory(Path.Combine(tmp, ".vs"));
                Directory.CreateDirectory(Path.Combine(tmp, "a"));
                File.WriteAllText(Path.Combine(tmp, ".vs", "state"), "x");
                var held = Path.Combine(tmp, "a", "held.dll");
                File.WriteAllText(held, "x");
                File.WriteAllText(Path.Combine(tmp, "a", "other.dll"), "x");
                File.WriteAllText(Path.Combine(tmp, "b.txt"), "x");

                using (new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var dir = new LocalFileSystemDirectory(tmp);
                    Action delete = () => dir.Delete(p => !p.StartsWith(".vs"));

                    delete.Should().Throw<PartialDeleteException>()
                        .Which.Failures.Should().ContainSingle().Which.Should().Contain("held.dll");
                }

                File.Exists(held).Should().BeTrue();
                File.Exists(Path.Combine(tmp, "a", "other.dll")).Should().BeFalse();
                File.Exists(Path.Combine(tmp, "b.txt")).Should().BeFalse();
                File.Exists(Path.Combine(tmp, ".vs", "state")).Should().BeTrue();
            }
        }
    }
}