// ------------------------------------------------------------------------------------------------
// <copyright file="AdminOnlyCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Tests.Behaviors.Support;

// A command reserved to the administrators of the instance.
internal sealed record AdminOnlyCommand : ICommand<string>, IAdminOnly;
