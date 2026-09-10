using System.Security.Cryptography;
using System.Text;

namespace Bari.Core.Generic
{
    /// <summary>
    /// Simple static utility class to compute MD5 checksum of strings
    /// </summary>
    public static class MD5
    {
        /// <summary>
        /// Encode a string by returning its MD5 checksum in string format
        /// </summary>
        /// <param name="input">The string to encode</param>
        /// <returns>Checksum, every byte is represented in hexadecimal</returns>
        public static string Encode(string input)
        {
            byte[] raw = Encoding.UTF8.GetBytes(input);
            byte[] hash = System.Security.Cryptography.MD5.HashData(raw);

            var sb = new StringBuilder();
            foreach (var b in hash)
                sb.Append(b.ToString("x2").ToLowerInvariant());
            return sb.ToString();
        }
    }
}
