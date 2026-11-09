// ------------------------------------------------------------------------------------------------
// <copyright file="AdmitOrganizationCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Organizations.Commands.AdmitOrganization;

/// <summary>Admits a forge account, creating its organization when the instance does not know it yet.</summary>
/// <param name="Forge">The forge of the account.</param>
/// <param name="Login">The login of the account.</param>
/// <param name="ExternalAccountId">The identifier the forge gives the account.</param>
public sealed record AdmitOrganizationCommand(Forge Forge, string Login, string ExternalAccountId) : ICommand<Organization>, IAdminOnly;
