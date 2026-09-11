using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Transactions
{
    public class BurcatCommitException : BurcatException
    {
        public BurcatCommitException(string message, BurcatCommitException? additionalException = null) : base(message, innerException: additionalException) { }
    }
}
