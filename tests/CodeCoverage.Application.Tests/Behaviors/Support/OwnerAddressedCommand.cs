// ------------------------------------------------------------------------------------------------
// <copyright file="OwnerAddressedCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Tests.Behaviors.Support;

// A command anyone may send to an owner named in its address.
internal sealed record OwnerAddressedCommand(Forge Forge, string Owner) : ICommand<string>, IPublicOwnerAddressed;
