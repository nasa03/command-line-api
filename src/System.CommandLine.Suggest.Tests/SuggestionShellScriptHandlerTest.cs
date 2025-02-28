﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace System.CommandLine.Suggest.Tests
{
    public class SuggestionShellScriptHandlerTest
    {
        private readonly CommandLineConfiguration _configuration;
        private readonly TestConsole _console;

        public SuggestionShellScriptHandlerTest()
        {
            _configuration = new SuggestionDispatcher(new TestSuggestionRegistration()).Configuration;
            _console = new TestConsole();
        }

        [Fact]
        public async Task When_shell_type_is_not_supported_it_throws()
        {
            await _configuration.InvokeAsync(
                "script 123",
                _console);

            _console.Error
                    .ToString()
                    .Should()
                    .Contain("Shell '123' is not supported.");
        }

        [Fact]
        public async Task It_should_print_bash_shell_script()
        {
            await _configuration.InvokeAsync(
                "script bash",
                _console);

            _console.Out.ToString().Should().Contain("_dotnet_bash_complete()");
        }

        [Fact]
        public async Task It_should_print_powershell_shell_script()
        {
            await _configuration.InvokeAsync(
                "script powershell",
                _console);

            _console.Out.ToString().Should().Contain("Register-ArgumentCompleter");
        }

        [Fact]
        public async Task It_should_print_zsh_shell_script()
        {
            await _configuration.InvokeAsync(
                "script zsh",
                _console);
            
            _console.Out.ToString().Should().Contain("_dotnet_zsh_complete()");
        }
    }
}
