using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Transactions
{
    public class CommitException(string message) : BurcatException(message) { }
}
