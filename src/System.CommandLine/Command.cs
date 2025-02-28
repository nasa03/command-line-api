﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.CommandLine.Completions;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Linq;

namespace System.CommandLine
{
    /// <summary>
    /// Represents a specific action that the application performs.
    /// </summary>
    /// <remarks>
    /// Use the Command object for actions that correspond to a specific string (the command name). See
    /// <see cref="RootCommand"/> for simple applications that only have one action. For example, <c>dotnet run</c>
    /// uses <c>run</c> as the command.
    /// </remarks>
    public class Command : IdentifierSymbol, IEnumerable<Symbol>
    {
        private ChildList<Argument>? _arguments;
        private ChildList<Option>? _options;
        private ChildList<Command>? _subcommands;
        private List<Action<CommandResult>>? _validators;

        /// <summary>
        /// Initializes a new instance of the Command class.
        /// </summary>
        /// <param name="name">The name of the command.</param>
        /// <param name="description">The description of the command, shown in help.</param>
        public Command(string name, string? description = null) : base(name, description)
        {
        }

        /// <summary>
        /// Gets the child symbols.
        /// </summary>
        public IEnumerable<Symbol> Children
        {
            get
            {
                foreach (var command in Subcommands)
                    yield return command;

                foreach (var option in Options)
                    yield return option;

                foreach (var argument in Arguments)
                    yield return argument;
            }
        }

        /// <summary>
        /// Represents all of the arguments for the command.
        /// </summary>
        public IList<Argument> Arguments => _arguments ??= new(this);

        internal bool HasArguments => _arguments is not null && _arguments.Count > 0 ;

        /// <summary>
        /// Represents all of the options for the command, including global options that have been applied to any of the command's ancestors.
        /// </summary>
        public IList<Option> Options => _options ??= new (this);

        internal bool HasOptions => _options is not null && _options.Count > 0;

        /// <summary>
        /// Represents all of the subcommands for the command.
        /// </summary>
        public IList<Command> Subcommands => _subcommands ??= new(this);

        internal bool HasSubcommands => _subcommands is not null && _subcommands.Count > 0;

        /// <summary>
        /// Validators to the command. Validators can be used
        /// to create custom validation logic.
        /// </summary>
        public List<Action<CommandResult>> Validators => _validators ??= new ();

        internal bool HasValidators => _validators is not null && _validators.Count > 0;

        /// <summary>
        /// Adds a <see cref="Symbol"/> to the command.
        /// </summary>
        /// <param name="symbol">The symbol to add to the command.</param>
        [EditorBrowsable(EditorBrowsableState.Never)] // hide from intellisense, it's public for C# duck typing
        public void Add(Symbol symbol)
        {
            // this method exists so users can use C# duck typing for adding symbols to the Command:
            // new Command { option };
            switch (symbol)
            {
                case Option option:
                    Options.Add(option);
                    break;
                case Argument argument:
                    Arguments.Add(argument);
                    break;
                case Command command:
                    Subcommands.Add(command);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        private protected override string DefaultName => throw new NotImplementedException();

        /// <summary>
        /// Gets or sets a value that indicates whether unmatched tokens should be treated as errors. For example,
        /// if set to <see langword="true"/> and an extra command or argument is provided, validation will fail.
        /// </summary>
        public bool TreatUnmatchedTokensAsErrors { get; set; } = true;

        /// <summary>
        /// Gets or sets the <see cref="ICommandHandler"/> for the command. The handler represents the action
        /// that will be performed when the command is invoked.
        /// </summary>
        /// <remarks>
        /// <para>Use one of the <see cref="Handler.SetHandler(Command, Action)" /> overloads to construct a handler.</para>
        /// <para>If the handler is not specified, parser errors will be generated for command line input that
        /// invokes this command.</para></remarks>
        public ICommandHandler? Handler { get; set; }

        /// <summary>
        /// Represents all of the symbols for the command.
        /// </summary>
        public IEnumerator<Symbol> GetEnumerator() => Children.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Parses an array strings using the command.
        /// </summary>
        /// <param name="args">The string arguments to parse.</param>
        /// <param name="configuration">The configuration on which the parser's grammar and behaviors are based.</param>
        /// <returns>A parse result describing the outcome of the parse operation.</returns>
        public ParseResult Parse(IReadOnlyList<string> args, CommandLineConfiguration? configuration = null)
            => Parser.Parse(this, args, configuration);

        /// <summary>
        /// Parses a command line string value using the command.
        /// </summary>
        /// <remarks>The command line string input will be split into tokens as if it had been passed on the command line.</remarks>
        /// <param name="commandLine">A command line string to parse, which can include spaces and quotes equivalent to what can be entered into a terminal.</param>
        /// <param name="configuration">The configuration on which the parser's grammar and behaviors are based.</param>
        /// <returns>A parse result describing the outcome of the parse operation.</returns>
        public ParseResult Parse(string commandLine, CommandLineConfiguration? configuration = null)
            => Parser.Parse(this, commandLine, configuration);

        /// <inheritdoc />
        public override IEnumerable<CompletionItem> GetCompletions(CompletionContext context)
        {
            var completions = new List<CompletionItem>();

            if (context.WordToComplete is { } textToMatch)
            {
                if (HasSubcommands)
                {
                    var commands = Subcommands;
                    for (int i = 0; i < commands.Count; i++)
                    {
                        AddCompletionsFor(commands[i]);
                    }
                }

                if (HasOptions)
                {
                    var options = Options;
                    for (int i = 0; i < options.Count; i++)
                    {
                        AddCompletionsFor(options[i]);
                    }
                }

                if (HasArguments)
                {
                    var arguments = Arguments;
                    for (int i = 0; i < arguments.Count; i++)
                    {
                        var argument = arguments[i];
                        foreach (var completion in argument.GetCompletions(context))
                        {
                            if (completion.Label.ContainsCaseInsensitive(textToMatch))
                            {
                                completions.Add(completion);
                            }
                        }
                    }
                }

                ParentNode? parent = FirstParent;
                while (parent is not null)
                {
                    Command parentCommand = (Command)parent.Symbol;

                    if (context.IsEmpty || context.ParseResult.FindResultFor(parentCommand) is not null)
                    {
                        if (parentCommand.HasOptions)
                        {
                            for (var i = 0; i < parentCommand.Options.Count; i++)
                            {
                                var option = parentCommand.Options[i];

                                if (option.AppliesToSelfAndChildren)
                                {
                                    AddCompletionsFor(option);
                                }
                            }
                        }
                        parent = parent.Symbol.FirstParent;
                    }
                    else
                    {
                        parent = parent.Next;
                    }
                }
            }

            return completions
                   .OrderBy(item => item.SortText.IndexOfCaseInsensitive(context.WordToComplete))
                   .ThenBy(symbol => symbol.Label, StringComparer.OrdinalIgnoreCase);

            void AddCompletionsFor(IdentifierSymbol identifier)
            {
                if (!identifier.IsHidden)
                {
                    foreach (var alias in identifier.Aliases)
                    {
                        if (alias is { } &&
                            alias.ContainsCaseInsensitive(textToMatch))
                        {
                            completions.Add(new CompletionItem(alias, CompletionItemKind.Keyword, detail: identifier.Description));
                        }
                    }
                }
            }
        }
    }
}
