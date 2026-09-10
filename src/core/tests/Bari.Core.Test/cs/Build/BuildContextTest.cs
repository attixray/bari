using System;
using Bari.Core.Build;
using Bari.Core.Build.Cache;
using Bari.Core.Build.Statistics;
using Bari.Core.Exceptions;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace Bari.Core.Test.Build
{
    [TestFixture]
    public class BuildContextTest
    {
        [Test]
        public void DependencyCycleFailsTheBuild()
        {
            var first = new Mock<IBuilder>();
            var second = new Mock<IBuilder>();

            first.SetupGet(builder => builder.Prerequisites).Returns(new[] { second.Object });
            second.SetupGet(builder => builder.Prerequisites).Returns(new[] { first.Object });

            var context = new BuildContext(
                Mock.Of<ICachedBuilderFactory>(),
                Mock.Of<IMonitoredBuilderFactory>(),
                () => Mock.Of<IBuilderStatistics>());

            context.AddBuilder(first.Object);

            Action run = () => context.Run(first.Object);

            run.Should().Throw<BuildGraphCycleException>()
                .WithMessage("Build graph contains a dependency cycle.");
            first.Verify(builder => builder.Run(It.IsAny<IBuildContext>()), Times.Never);
            second.Verify(builder => builder.Run(It.IsAny<IBuildContext>()), Times.Never);
        }
    }
}
