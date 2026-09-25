using System;
using Bari.Core;
using Bari.Core.Generic;
using Bari.Core.Model;
using Bari.Core.Test.Helper;
using Bari.Plugins.VsCore.VisualStudio;
using FluentAssertions;
using NUnit.Framework;
using Ninject;

namespace Bari.Plugins.Csharp.Test.VisualStudio
{
    [TestFixture]
    public class DefaultProjectGuidManagementTest
    {
        private IKernel kernel;
        private Suite suite;

        [SetUp]
        public void SetUp()
        {
            kernel = new StandardKernel();
            Kernel.RegisterCoreBindings(kernel);
            kernel.Bind<IFileSystemDirectory>().ToConstant(new TestFileSystemDirectory("root")).WhenTargetHas
                <SuiteRootAttribute>();
            kernel.Bind<IFileSystemDirectory>().ToConstant(new TestFileSystemDirectory("target")).WhenTargetHas
                <TargetRootAttribute>();

            suite = kernel.Get<Suite>();
            suite.Name = "test suite";
        }

        [TearDown]
        public void TearDown()
        {
            kernel.Dispose();
        }

        [Test]
        public void NameBasedGuidFollowsRfc4122()
        {
            var dns = new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

            DefaultProjectGuidManagement.NameBasedGuid(dns, "www.example.com")
                .Should().Be(new Guid("2ed6657d-e927-568b-95e1-2665a8aea6a2"));
        }

        [Test]
        public void GuidsDoNotChangeWhenTheCacheIsCleaned()
        {
            var module = suite.GetModule("mod");
            var project = module.GetProject("proj");
            var other = module.GetProject("other");

            using (var cache = new TempDirectory())
            using (var cleanedCache = new TempDirectory())
            {
                var before = new DefaultProjectGuidManagement(
                    new Lazy<IFileSystemDirectory>(() => new LocalFileSystemDirectory(cache)), suite);
                var after = new DefaultProjectGuidManagement(
                    new Lazy<IFileSystemDirectory>(() => new LocalFileSystemDirectory(cleanedCache)), suite);

                after.GetGuid(project).Should().Be(before.GetGuid(project));
                after.GetGuid(module).Should().Be(before.GetGuid(module));
                before.GetGuid(other).Should().NotBe(before.GetGuid(project));
                before.GetGuid(module).Should().NotBe(before.GetGuid(project));
            }
        }

        [Test]
        public void CachedGuidsAreKept()
        {
            var module = suite.GetModule("mod");
            var project = module.GetProject("proj");
            var cached = Guid.NewGuid();

            using (var cache = new TempDirectory())
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(cache, "guids"),
                    "mod.proj=" + cached.ToString("B") + Environment.NewLine);
                var guids = new DefaultProjectGuidManagement(
                    new Lazy<IFileSystemDirectory>(() => new LocalFileSystemDirectory(cache)), suite);

                guids.GetGuid(project).Should().Be(cached);
            }
        }
    }
}
