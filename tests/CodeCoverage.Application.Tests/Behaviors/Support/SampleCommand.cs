// ------------------------------------------------------------------------------------------------
// <copyright file="SampleCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

namespace CodeCoverage.Application.Tests.Behaviors.Support;

// A command that carries a secret, which no behavior may ever write to the logs.
internal sealed record SampleCommand(string Name, string Token) : ICommand<string>;
