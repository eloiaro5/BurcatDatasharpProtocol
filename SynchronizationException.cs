using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace BurcatProtocol
{
    internal class SynchronizationException : Exception
    {
        public SynchronizationException() { }
        public SynchronizationException(string? message) : base(message) { }
        public SynchronizationException(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
