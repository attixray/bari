using System;
using System.IO;
using System.Text;
using Bari.Console.UI;
using FluentAssertions;
using NUnit.Framework;

namespace Bari.Console.Test
{
    [TestFixture]
    public class ConsoleUserInterfaceTest
    {
        private class ClosedPipeWriter : TextWriter
        {
            public int Writes { get; private set; }

            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }

            public override void Write(char value)
            {
                Writes++;
                throw new IOException("No process is on the other end of the pipe.");
            }
        }

        private TextWriter originalOut;

        [SetUp]
        public void SetUp()
        {
            originalOut = System.Console.Out;
        }

        [TearDown]
        public void TearDown()
        {
            System.Console.SetOut(originalOut);
        }

        [Test]
        public void ClosedOutputDoesNotFailBari()
        {
            var closed = new ClosedPipeWriter();
            System.Console.SetOut(closed);
            var ui = new ConsoleUserInterface(new ConsoleParameters(new string[0]));

            Action write = () =>
            {
                ui.Error("MSBUILD : error MSB4166: Child node \"2\" exited prematurely.");
                ui.Warning("warning", new[] { "hint" });
                ui.Message("*message*");
                ui.Describe("target", "description");
            };

            write.Should().NotThrow();
            closed.Writes.Should().Be(1, "output stops after the first failed write");
        }
    }
}
