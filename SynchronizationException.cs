using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace BurcatProtocol
{
    /// <summary>
    /// Represents an internal synchronization failure while processing protocol state.
    /// </summary>
    internal class SynchronizationException : Exception
    {
        /// <summary>
        /// Initializes a synchronization exception.
        /// </summary>
        public SynchronizationException() { }

        /// <summary>
        /// Initializes a synchronization exception with a message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public SynchronizationException(string? message) : base(message) { }

        /// <summary>
        /// Initializes a synchronization exception with a message and inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        public SynchronizationException(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
