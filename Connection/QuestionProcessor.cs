using System;
using System.Text;

namespace BurcatProtocol.Connection
{
    public enum ActionerKeySpecificity
    {
        RequiredMinimum,
        RequiredExactly,
        RequiredMaximum,
        OptionalMinimum,
        OptionalExactly,
        OptionalMaximum
    }

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

    public class QuestionArgumentKey
    {
        private string[] Parameters { get; }
        public int ValueCount { get; }
        public ActionerKeySpecificity Specificity { get; }

        public QuestionArgumentKey(string[] parameters, int valueCount, ActionerKeySpecificity specificity) { Parameters = parameters; ValueCount = valueCount; Specificity = specificity; }
        public QuestionArgumentKey(string[] parameters) : this(parameters, 0, ActionerKeySpecificity.OptionalExactly) { }

        public bool ContainsParameter(string argument) => Parameters.Contains(argument);
    }
}
