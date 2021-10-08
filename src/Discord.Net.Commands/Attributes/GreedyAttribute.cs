using System;

namespace Discord.Commands
{
    /// <summary>
    ///     Marks the input as greedy for the parser, parsing the input by parsing parameters from the input an adding them as elements until one isn't parseable as the item type
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public class GreedyAttribute : Attribute
    {
    }
}
