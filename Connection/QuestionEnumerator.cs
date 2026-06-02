using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Connection
{
    /// <summary>
    /// Enumerates a command and its nested command enumerators as a single argument stream.
    /// </summary>
    public class QuestionEnumerator : IEnumerator<string>
    {
        /// <inheritdoc/>
        public string Current => CurrentCommand!.Current;

        private LinkedList<IEnumerator<string>> Commands { get; } = new();
        IEnumerator<IEnumerator<string>>? Enumerator { get; set; } = null;
        IEnumerator<string>? CurrentCommand { get; set; } = null;
        bool EnumeratorS = false;
        bool EnumeratorF = false;

        /// <inheritdoc/>
        object System.Collections.IEnumerator.Current => Current;

        /// <summary>
        /// Initializes an enumerator for a command string.
        /// </summary>
        /// <param name="command">The command text.</param>
        public QuestionEnumerator(string command) { Commands.AddLast(((IEnumerable<string>)command.Split(' ', StringSplitOptions.RemoveEmptyEntries)).GetEnumerator()); }

        /// <summary>
        /// Initializes an enumerator for a command string and nested commands.
        /// </summary>
        /// <param name="command">The command text.</param>
        /// <param name="innerCommands">The nested command enumerators.</param>
        public QuestionEnumerator(string command, params IEnumerator<string>[] innerCommands) : this(command)
        {
            foreach (var innerCommand in innerCommands)
                Commands.AddLast(innerCommand);
        }

        /// <inheritdoc/>
        public void Dispose() { throw new InvalidOperationException(); }

        /// <inheritdoc/>
        public bool MoveNext()
        {
            if (!EnumeratorS) { Reset(); EnumeratorS = true; }

            if (EnumeratorF) throw new InvalidOperationException();
            else if (!CurrentCommand!.MoveNext())
                if (Enumerator!.MoveNext()) { CurrentCommand = Enumerator.Current; MoveNext(); }
                else EnumeratorF = true;

            return !EnumeratorF;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            Enumerator = Commands.GetEnumerator();

            if (Enumerator.MoveNext()) { CurrentCommand = Enumerator.Current; EnumeratorF = false; }
            else EnumeratorF = true;
        }
    }
}
