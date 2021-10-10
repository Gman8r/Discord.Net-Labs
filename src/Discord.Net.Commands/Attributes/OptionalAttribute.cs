using System;

namespace Discord.Commands
{
    /// <summary>
    ///     Marks the input as skippable by the parser, similar to an optional parameter without actually defining it as such in the function
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public class OptionalAttribute : Attribute
    {
    }
}
