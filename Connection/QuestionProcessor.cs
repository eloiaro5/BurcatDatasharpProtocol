using System;
using System.Text;

namespace BurcatProtocol.Connection
{
    /// <summary>
    /// Describes how many values a command argument accepts and whether it is required.
    /// </summary>
    public enum ActionerKeySpecificity
    {
        /// <summary>
        /// The argument is required and accepts at least the configured number of values.
        /// </summary>
        RequiredMinimum,

        /// <summary>
        /// The argument is required and accepts exactly the configured number of values.
        /// </summary>
        RequiredExactly,

        /// <summary>
        /// The argument is required and accepts at most the configured number of values.
        /// </summary>
        RequiredMaximum,

        /// <summary>
        /// The argument is optional and accepts at least the configured number of values.
        /// </summary>
        OptionalMinimum,

        /// <summary>
        /// The argument is optional and accepts exactly the configured number of values.
        /// </summary>
        OptionalExactly,

        /// <summary>
        /// The argument is optional and accepts at most the configured number of values.
        /// </summary>
        OptionalMaximum
    }

    /// <summary>
    /// Parses command strings and dispatches argument handlers.
    /// </summary>
    public static class QuestionProcessor
    {
        private static AsyncLocal<Dictionary<Guid, string>> Parameters { get; } = new() { Value = [] };
        private static void SetParameter(Guid identifier, string value) => Parameters.Value!.Add(identifier, value);
        private static string GetParameter(Guid identifier)
        {
            string value = Parameters.Value![identifier];
            Parameters.Value!.Remove(identifier);
            return value;
        }

        /// <summary>
        /// Splits a command string into command enumerators while preserving quoted text.
        /// </summary>
        /// <param name="command">The command text to parse.</param>
        /// <returns>The parsed command enumerators.</returns>
        public static IEnumerable<QuestionEnumerator> ParseCommand(string command)
        {
            StringBuilder commandSB = new(), valueSB = new();
            Guid valueID = Guid.Empty;
            bool special = false, inText = false;
            foreach (char character in command)
                switch (character)
                {
                    case '\\':
                        if (special && inText) valueSB.Append(character);
                        else if (special) commandSB.Append(character);

                        special = !special;
                        break;
                    case '|':
                        if (inText) valueSB.Append(character);
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(commandSB.ToString())) yield return new(commandSB.ToString());
                            commandSB.Clear();
                        }
                        break;
                    case '"':
                        if (special)
                        {
                            if (inText) valueSB.Append(character);
                            else commandSB.Append(character);

                            special = false;
                        }
                        else if (inText)
                        {
                            SetParameter(valueID, valueSB.ToString());
                            commandSB.Append($"{{{valueID.ToString()}}}");
                            valueSB.Clear();
                            inText = false;
                        }
                        else
                        {
                            valueID = Guid.NewGuid();
                            inText = true;
                        }
                        break;
                    default:
                        if (inText) valueSB.Append(character);
                        else commandSB.Append(character);
                        break;
                }

            if (!string.IsNullOrWhiteSpace(commandSB.ToString())) yield return new(commandSB.ToString());
        }

        /// <summary>
        /// Parses command arguments and invokes matching argument actions.
        /// </summary>
        /// <param name="command">The command argument enumerator.</param>
        /// <param name="actions">The argument handlers keyed by accepted argument names.</param>
        public static void ParseArguments(IEnumerator<string> command, Dictionary<QuestionArgumentKey, Action<IEnumerable<string>>> actions)
        {
            bool parameters = command.MoveNext();

            if (parameters)
            {
                LinkedList<QuestionArgumentKey> used = new();
                LinkedList<string> parameterValues = new();
                string parameter = command.Current;

                if (parameter.StartsWith('-'))
                {
                    bool next;

                    do
                    {
                        next = command.MoveNext();

                        if (next && command.Current.StartsWith('{') && command.Current.EndsWith('}')) parameterValues.AddLast(GetParameter(new(command.Current[1..^1])));
                        else if (!next || command.Current.StartsWith('-'))
                        {
                            bool found = false;
                            var actionE = actions.GetEnumerator();

                            while (!found && actionE.MoveNext())
                            {
                                QuestionArgumentKey key = actionE.Current.Key;

                                if (key.ContainsParameter(parameter))
                                {
                                    if (used.Contains(key)) throw new InvalidOperationException($"The argument '{parameter}' was alredy used");
                                    else
                                        if ((key.Specificity == ActionerKeySpecificity.RequiredMinimum || key.Specificity == ActionerKeySpecificity.OptionalMinimum) && parameterValues.Count < key.ValueCount) throw new InvalidOperationException($"The argument '{parameterValues.Last!.Value}' needs, at least, '{key.ValueCount}' parameter/s");
                                        else if ((key.Specificity == ActionerKeySpecificity.RequiredExactly || key.Specificity == ActionerKeySpecificity.OptionalExactly) && parameterValues.Count != key.ValueCount) throw new InvalidOperationException($"The argument '{parameterValues.Last!.Value}' needs, exactly, '{key.ValueCount}' parameter/s");
                                        else if ((key.Specificity == ActionerKeySpecificity.RequiredMaximum || key.Specificity == ActionerKeySpecificity.OptionalMaximum) && parameterValues.Count > key.ValueCount) throw new InvalidOperationException($"The argument '{parameterValues.Last!.Value}' can handle, at maximum, '{key.ValueCount}' parameter/s");
                                        else if (used.Contains(actionE.Current.Key)) throw new InvalidOperationException($"The argument '{command.Current}' has alredy been used");
                                        else { actionE.Current.Value.Invoke(parameterValues); used.AddLast(actionE.Current.Key); found = true; }
                                }
                            }

                            if (found && next) { parameterValues.Clear(); parameter = command.Current; }
                            else if (!found) throw new InvalidOperationException($"The argument '{parameter}' was not recognized");
                        }
                        else if (next) parameterValues.AddLast(command.Current);
                        else throw new InvalidOperationException("Expected additional arguments, which must start with '-'");
                    }
                    while (next);
                }
                else throw new InvalidOperationException("Expected arguments, which must start with '-'");

                foreach (QuestionArgumentKey key in actions.Keys)
                    if ((key.Specificity == ActionerKeySpecificity.RequiredMinimum || key.Specificity == ActionerKeySpecificity.RequiredExactly || key.Specificity == ActionerKeySpecificity.RequiredMaximum) && !used.Contains(key))
                        throw new InvalidOperationException("There are required arguments that are not specified");
            }
        }
    }

    /// <summary>
    /// Describes a recognized command argument and its value-count requirements.
    /// </summary>
    public class QuestionArgumentKey
    {
        private string[] Parameters { get; }

        /// <summary>
        /// Gets the configured argument value count.
        /// </summary>
        public int ValueCount { get; }

        /// <summary>
        /// Gets the argument value-count specificity.
        /// </summary>
        public ActionerKeySpecificity Specificity { get; }

        /// <summary>
        /// Initializes an argument key.
        /// </summary>
        /// <param name="parameters">The accepted argument names.</param>
        /// <param name="valueCount">The configured value count.</param>
        /// <param name="specificity">The value-count specificity.</param>
        public QuestionArgumentKey(string[] parameters, int valueCount, ActionerKeySpecificity specificity) { Parameters = parameters; ValueCount = valueCount; Specificity = specificity; }

        /// <summary>
        /// Initializes an optional argument key with no values.
        /// </summary>
        /// <param name="parameters">The accepted argument names.</param>
        public QuestionArgumentKey(string[] parameters) : this(parameters, 0, ActionerKeySpecificity.OptionalExactly) { }

        /// <summary>
        /// Determines whether an argument name matches this key.
        /// </summary>
        /// <param name="argument">The argument name.</param>
        /// <returns><see langword="true"/> when the argument is accepted; otherwise, <see langword="false"/>.</returns>
        public bool ContainsParameter(string argument) => Parameters.Contains(argument);
    }
}
