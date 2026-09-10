using NUnit.Framework;

namespace SmokeTest
{
    public class MessageTest
    {
        [Test]
        public void LoadsTheBuiltProjectReference()
        {
            Assert.That(Message.Text, Is.EqualTo("Bari on .NET 10"));
        }
    }
}
