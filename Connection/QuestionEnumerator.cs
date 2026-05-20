using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Connection
{
    public class QuestionEnumerator : IEnumerator<string>
    {
        public string Current => CurrentCommand!.Current;

        private LinkedList<IEnumerator<string>> Commands { get; } = new();
        IEnumerator<IEnumerator<string>>? Enumerator { get; set; } = null;
        IEnumerator<string>? CurrentCommand { get; set; } = null;
        bool EnumeratorS = false;
        bool EnumeratorF = false;

        object System.Collections.IEnumerator.Current => Current;

        public QuestionEnumerator(string command) { Commands.AddLast(((IEnumerable<string>)command.Split(' ', StringSplitOptions.RemoveEmptyEntries)).GetEnumerator()); }
        public QuestionEnumerator(string command, params IEnumerator<string>[] innerCommands) : this(command)
        {
            foreach (var innerCommand in innerCommands)
                Commands.AddLast(innerCommand);
        }

        public void Dispose() { throw new InvalidOperationException(); }

        public bool MoveNext()
        {
            if (!EnumeratorS) { Reset(); EnumeratorS = true; }

            if (EnumeratorF) throw new InvalidOperationException();
            else if (!CurrentCommand!.MoveNext())
                if (Enumerator!.MoveNext()) { CurrentCommand = Enumerator.Current; MoveNext(); }
                else EnumeratorF = true;

            return !EnumeratorF;
        }

        public void Reset()
        {
            Enumerator = Commands.GetEnumerator();

            if (Enumerator.MoveNext()) { CurrentCommand = Enumerator.Current; EnumeratorF = false; }
            else EnumeratorF = true;
        }
    }
}
