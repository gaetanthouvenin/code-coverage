// ------------------------------------------------------------------------------------------------
// <copyright file="SuspendOrganizationCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Organizations.Commands.SuspendOrganization;

/// <summary>Suspends an organization on the decision of an administrator of the instance.</summary>
/// <param name="Forge">The forge of the account.</param>
/// <param name="Login">The login of the account.</param>
public sealed record SuspendOrganizationCommand(Forge Forge, string Login) : ICommand<Unit>, IAdminOnly;
