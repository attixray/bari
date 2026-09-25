using System;
using System.IO;
using Bari.Core.Commands;
using Bari.Core.Commands.Helper;
using Bari.Core.Generic;
using Bari.Core.Model;
using Bari.Core.Test.Helper;
using Bari.Plugins.AddonSupport.VisualStudio.SolutionItems;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace Bari.Plugins.AddonSupport.Test.VisualStudio
{
    [TestFixture]
    public class AddonSupportSolutionItemProviderTest
    {
        [Test]
        public void UnchangedFileIsNotRewritten()
        {
            using (var tmp = new TempDirectory())
            {
                var suite = new Suite(new TestFileSystemDirectory("root"));
                suite.GetModule("TestModule").GetProject("TestExe").Type = ProjectType.Executable;
                var targetParser = new Mock<ICommandTargetParser>();
                targetParser.Setup(p => p.ParseTarget(It.IsAny<string>())).Returns(new FullSuiteTarget(suite));
                var provider = new AddonSupportSolutionItemProvider(
                    new LocalFileSystemDirectory(tmp), suite, new Mock<ICommand>().Object, targetParser.Object);
                var file = Path.Combine(tmp, "product.yaml");

                provider.GetItems("product");
                var contents = File.ReadAllText(file);
                contents.Should().Contain("goal: debug").And.EndWith(Environment.NewLine);

                var written = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                File.SetLastWriteTimeUtc(file, written);
                provider.GetItems("product");

                File.GetLastWriteTimeUtc(file).Should().Be(written);
                File.ReadAllText(file).Should().Be(contents);
            }
        }
    }
}
