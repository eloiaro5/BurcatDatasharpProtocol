using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Transactions
{
    public class BurcatCommitException(string message) : BurcatException(message) { }
}
