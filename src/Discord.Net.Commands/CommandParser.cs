using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Discord.Commands
{
    internal static class CommandParser
    {
        // Taken from CommandInfo for my own nefarious misdeeds
        private static readonly MethodInfo _convertParamsMethod = typeof(CommandInfo).GetTypeInfo().GetDeclaredMethod(nameof(ConvertParamsList));
        private static readonly ConcurrentDictionary<Type, Func<IEnumerable<object>, object>> _arrayConverters = new ConcurrentDictionary<Type, Func<IEnumerable<object>, object>>();

        private static T[] ConvertParamsList<T>(IEnumerable<object> paramsList)
            => paramsList.Cast<T>().ToArray();

        private enum ParserPart
        {
            None,
            Parameter,
            QuotedParameter
        }
        public static async Task<ParseResult> ParseArgsAsync(CommandInfo command, ICommandContext context, bool ignoreExtraArgs, IServiceProvider services, string input, int startPos, IReadOnlyDictionary<char, char> aliasMap)
        {
            ParameterInfo curParam = null;
            StringBuilder argBuilder = new StringBuilder(input.Length);
            int endPos = input.Length;
            var curPart = ParserPart.None;
            int lastArgEndPos = int.MinValue;
            int lastArgStartPos = 0;
            var argList = ImmutableArray.CreateBuilder<TypeReaderResult>();
            var paramList = ImmutableArray.CreateBuilder<TypeReaderResult>();
            (ParameterInfo, List<TypeReaderResult>) currentGreedyArgs = (null, null);
            List<ParameterInfo> currentGreedyParam = null;
            bool isEscaping = false;
            char c, matchQuote = '\0';
            var failedParses = new List<ParseResult>();

            // local helper functions
            bool IsOpenQuote(IReadOnlyDictionary<char, char> dict, char ch)
            {
                // return if the key is contained in the dictionary if it is populated
                if (dict.Count != 0)
                    return dict.ContainsKey(ch);
                // or otherwise if it is the default double quote
                return c == '\"';
            }

            char GetMatch(IReadOnlyDictionary<char, char> dict, char ch)
            {
                // get the corresponding value for the key, if it exists
                // and if the dictionary is populated
                if (dict.Count != 0 && dict.TryGetValue(c, out var value))
                    return value;
                // or get the default pair of the default double quote
                return '\"';
            }

            // Also taken from CommandInfo, convert collection to array
            object GetTypeArray(Type type, IEnumerable<object> collection)
            {
                var func = _arrayConverters.GetOrAdd(type, t =>
                {
                    var method = _convertParamsMethod.MakeGenericMethod(t);
                    return (Func<IEnumerable<object>, object>)method.CreateDelegate(typeof(Func<IEnumerable<object>, object>));
                });
                return func(collection);
            }

            for (int curPos = startPos; curPos <= endPos; curPos++)
            {
                if (curPos < endPos)
                    c = input[curPos];
                else
                    c = '\0';

                //If we're processing an remainder parameter, ignore all other logic
                if (curParam != null && curParam.IsRemainder && curPos != endPos)
                {
                    argBuilder.Append(c);
                    continue;
                }

                //If this character is escaped, skip it
                if (isEscaping)
                {
                    if (curPos != endPos)
                    {
                        // if this character matches the quotation mark of the end of the string
                        // means that it should be escaped
                        // but if is not, then there is no reason to escape it then
                        if (c != matchQuote)
                        {
                            // if no reason to escape the next character, then re-add \ to the arg
                            argBuilder.Append('\\');
                        }

                        argBuilder.Append(c);
                        isEscaping = false;
                        continue;
                    }
                }
                //Are we escaping the next character?
                if (c == '\\' && (curParam == null || !curParam.IsRemainder))
                {
                    isEscaping = true;
                    continue;
                }

                //If we're not currently processing one, are we starting the next argument yet?
                if (curPart == ParserPart.None)
                {
                    if (char.IsWhiteSpace(c) || curPos == endPos)
                        continue; //Skip whitespace between arguments
                    else if (curPos == lastArgEndPos)
                        return ParseResult.FromError(CommandError.ParseFailed, "There must be at least one character of whitespace between arguments.");
                    else
                    {
                        if (curParam == null)
                            curParam = command.Parameters.Count > argList.Count ? command.Parameters[argList.Count] : null;

                        if (!curParam.IsGreedy)
                            currentGreedyArgs = (null, null);
                        else if (currentGreedyArgs.Item1 == null)
                            currentGreedyArgs = (curParam, new List<TypeReaderResult>());

                        if (curParam != null && curParam.IsRemainder)
                        {
                            argBuilder.Append(c);
                            continue;
                        }

                        if (IsOpenQuote(aliasMap, c))
                        {
                            curPart = ParserPart.QuotedParameter;
                            matchQuote = GetMatch(aliasMap, c);
                            continue;
                        }
                        curPart = ParserPart.Parameter;
                    }
                }

                //Has this parameter ended yet?
                string argString = null;
                if (curPart == ParserPart.Parameter)
                {
                    if (curPos == endPos || char.IsWhiteSpace(c))
                    {
                        argString = argBuilder.ToString();
                        lastArgEndPos = curPos;
                    }
                    else
                        argBuilder.Append(c);
                }
                else if (curPart == ParserPart.QuotedParameter)
                {
                    if (c == matchQuote)
                    {
                        argString = argBuilder.ToString(); //Remove quotes
                        lastArgEndPos = curPos + 1;
                    }
                    else
                        argBuilder.Append(c);
                }

                if (argString != null)
                {
                    if (curParam == null)
                    {
                        if (command.IgnoreExtraArgs)
                            break;
                        else
                            return ParseResult.FromError(CommandError.BadArgCount, "The input text has too many parameters.");
                    }

                    var typeReaderResult = await curParam.ParseAsync(context, argString, services).ConfigureAwait(false);
                    if (!typeReaderResult.IsSuccess && typeReaderResult.Error != CommandError.MultipleMatches)
                    {
                        if (curParam.IsOptional
                            || curParam.IsGreedy) // Handle incorrect values that may be skippable or the end of a greedy chain
                        {
                            if (!curParam.IsGreedy)    // Skip skippable argument by putting in default value
                                argList.Add(TypeReaderResult.FromSuccess(curParam.DefaultValue));
                            else
                            {
                                argList.Add(TypeReaderResult.FromSuccess(GetTypeArray(currentGreedyArgs.Item1.Type,
                                    currentGreedyArgs.Item2.Select(a => a.BestMatch))));
                                currentGreedyArgs = (null, null);
                            }
                            // Add to our list of failed optional parameters, so the first one is returned if the command fails
                            failedParses.Add(ParseResult.FromError(typeReaderResult, curParam));
                            // Reset to rereading the previous argument
                            curParam = null;
                            curPart = ParserPart.None;
                            argBuilder.Clear();
                            curPos = lastArgStartPos;
                            continue;
                        }
                        else
                        {
                            failedParses.Add(ParseResult.FromError(typeReaderResult, curParam));
                            return failedParses.First();
                        }
                    }
                    else if (typeReaderResult.IsSuccess && curParam.IsGreedy) // Add to greedy arg list
                    {
                        currentGreedyArgs.Item2.Add(typeReaderResult);
                        curParam = null;
                        curPart = ParserPart.None;
                        argBuilder.Clear();
                        lastArgStartPos = curPos;
                        continue;
                    }

                    if (curParam.IsMultiple)
                    {
                        paramList.Add(typeReaderResult);

                        curPart = ParserPart.None;
                    }
                    else
                    {
                        argList.Add(typeReaderResult);

                        curParam = null;
                        curPart = ParserPart.None;
                    }
                    argBuilder.Clear();
                    lastArgStartPos = curPos;
                }
            }

            if (curParam != null && curParam.IsRemainder)
            {
                var typeReaderResult = await curParam.ParseAsync(context, argBuilder.ToString(), services).ConfigureAwait(false);
                if (!typeReaderResult.IsSuccess)
                {
                    failedParses.Add(ParseResult.FromError(typeReaderResult, curParam));
                    return failedParses.First();
                }
                argList.Add(typeReaderResult);
            }

            if (isEscaping)
                return ParseResult.FromError(CommandError.ParseFailed, "Input text may not end on an incomplete escape.");
            if (curPart == ParserPart.QuotedParameter)
                return ParseResult.FromError(CommandError.ParseFailed, "A quoted parameter is incomplete.");

            // Wrap up greedy collection if we're doing one
            if (currentGreedyArgs.Item2?.Any() ?? false)
            {
                argList.Add(TypeReaderResult.FromSuccess(GetTypeArray(currentGreedyArgs.Item1.Type,
                    currentGreedyArgs.Item2.Select(a => a.BestMatch))));
            }

            //Add missing optionals
            for (int i = argList.Count; i < command.Parameters.Count; i++)
            {
                var param = command.Parameters[i];
                if (param.IsMultiple && !param.IsGreedy)
                    continue;
                if (!param.IsOptional && !param.IsGreedy)
                    return ParseResult.FromError(CommandError.BadArgCount, "The input text has too few parameters.");
                argList.Add(TypeReaderResult.FromSuccess(param.DefaultValue));
            }

            return ParseResult.FromSuccess(argList.ToImmutable(), paramList.ToImmutable());
        }
    }
}
