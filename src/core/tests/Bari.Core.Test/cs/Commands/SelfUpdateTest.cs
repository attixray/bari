using System;
using Bari.Core.Commands;
using FluentAssertions;
using NUnit.Framework;

namespace Bari.Core.Test.Commands
{
    [TestFixture]
    public class SelfUpdateTest
    {
        [TestCase("1.1.1", "1.1.0.7", true)]
        [TestCase("v1.2.0", "1.1.9.0", true)]
        [TestCase("1.1.0", "1.1.0.0", false)]
        [TestCase("1.1.0", "1.1.0.12", false)]
        [TestCase("1.0.3", "1.1.0.0", false)]
        [TestCase("1.1.0", "0.0.0.0", true)]
        [TestCase("not-a-version", "0.0.0.0", false)]
        [TestCase(null, "0.0.0.0", false)]
        public void ComparesTheReleaseTagWithTheBuild(string tag, string build, bool newer)
        {
            SelfUpdateCommand.IsNewer(tag, Version.Parse(build)).Should().Be(newer);
        }
    }
}
