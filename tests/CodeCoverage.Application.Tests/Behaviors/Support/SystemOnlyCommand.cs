// ------------------------------------------------------------------------------------------------
// <copyright file="SystemOnlyCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Tests.Behaviors.Support;

// A command reserved to the system principal (jobs, webhook processing).
internal sealed record SystemOnlyCommand : ICommand<string>, ISystemOnly;
