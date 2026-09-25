using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Bari.Core.Generic
{
    /// <summary>
    /// Some entries of a directory could not be deleted; everything else was.
    /// </summary>
    public class PartialDeleteException : IOException
    {
        private const int ListedFailures = 20;

        /// <summary>
        /// Creates the exception
        /// </summary>
        /// <param name="root">Absolute path of the directory being deleted</param>
        /// <param name="failures">One message per entry that could not be deleted, with the processes holding it when known</param>
        public PartialDeleteException(string root, IList<string> failures)
            : base(Describe(root, failures))
        {
            Failures = failures;
        }

        /// <summary>
        /// Gets one message per entry that could not be deleted
        /// </summary>
        public IList<string> Failures { get; }

        private static string Describe(string root, IList<string> failures)
        {
            var message = new StringBuilder();
            message.AppendFormat("Could not delete {0} {1} under {2}:", failures.Count, failures.Count == 1 ? "entry" : "entries", root);
            foreach (var failure in failures.Take(ListedFailures))
                message.AppendLine().Append("    ").Append(failure);
            if (failures.Count > ListedFailures)
                message.AppendLine().AppendFormat("    ... and {0} more", failures.Count - ListedFailures);
            return message.ToString();
        }
    }
}
