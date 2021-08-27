using System;
using System.Collections.Generic;

namespace Discord.ApplicationCommands.Builders
{
    internal class SlashParameterBuilder
    {
        private List<ParameterChoice> _choices;
        private List<Attribute> _attributes;

        public SlashCommandBuilder Command { get; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Type ParameterType { get; private set; }
        public bool IsRequired { get; set; }
        public object DefaultValue { get; set; }
        public IReadOnlyList<ParameterChoice> Choices => _choices;
        public IReadOnlyList<Attribute> Attributes => _attributes;

        public TypeReader TypeReader { get; private set; }

        internal SlashParameterBuilder (SlashCommandBuilder command)
        {
            Command = command;
            _choices = new List<ParameterChoice>();
            _attributes = new List<Attribute>();
        }

        internal SlashParameterBuilder (SlashCommandBuilder command, string name, Type type) : this(command)
        {
            Name = name;
            ParameterType = type;
        }

        public SlashParameterBuilder WithName (string name)
        {
            Name = name;
            return this;
        }

        public SlashParameterBuilder WithDescription (string description)
        {
            Description = description;
            return this;
        }

        internal SlashParameterBuilder WithType (Type type)
        {
            ParameterType = type;
            TypeReader = GetTypeReader(type);
            return this;
        }

        public SlashParameterBuilder AsOptional (bool state = true)
        {
            IsRequired = !state;
            return this;
        }

        public SlashParameterBuilder WithDefaultValue (object value)
        {
            DefaultValue = value;
            return this;
        }

        public SlashParameterBuilder AddOptions (params ParameterChoice[] options)
        {
            _choices.AddRange(options);
            return this;
        }

        public SlashParameterBuilder AddAttributes (params Attribute[] attributes)
        {
            _attributes.AddRange(attributes);
            return this;
        }

        internal SlashParameterInfo Build (SlashCommandInfo command)
        {
            return new SlashParameterInfo(this, command);
        }

        private TypeReader GetTypeReader (Type type)
        {
            var commandService = Command.Module.CommandService;

            return commandService.GetTypeReader(type);
        }
    }
}
