using System;
using Newtonsoft.Json;

namespace SmokeTest
{
    public static class Program
    {
        public static int Main()
        {
            Console.WriteLine(JsonConvert.SerializeObject(new { Message = Message.Text, Runtime = Environment.Version.Major }));
            return Environment.Version.Major == 10 ? 0 : 1;
        }
    }
}
