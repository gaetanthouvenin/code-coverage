// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationScope.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Identity;

/// <summary>Enters the active organization that a forge and a login designate.</summary>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="context">The organization of the scope.</param>
/// <param name="currentUser">The principal of the scope.</param>
/// <remarks>
///   A request authenticated by a personal token of the API enters the organization of its token only, and a token of
///   the instance enters none: any other entry is refused like an unknown organization, so that the answer reveals
///   nothing of it (M3.05).
/// </remarks>
internal sealed class OrganizationScope(IOrganizationDirectory directory, OrganizationContext context, ICurrentUser currentUser) : IOrganizationScope
{
  #region Implementation of IOrganizationScope

  /// <inheritdoc />
  public async Task<bool> TryEnterAsync(Forge forge, string? login, CancellationToken cancellationToken)
  {
    // The login comes straight from an address, before any validator: a bad one is a refusal, not a failure.
    if (login is null || !Organization.IsWellFormedLogin(login) || currentUser.IsInstanceToken)
    {
      return false;
    }

    var organization = await directory.FindByLoginAsync(forge, login, cancellationToken).ConfigureAwait(false);

    if (organization is not { State: OrganizationState.Active }
        || (currentUser.TokenOrganizationId is { } tokenOrganization && tokenOrganization != organization.Id))
    {
      return false;
    }

    context.Enter(organization.Id);

    return true;
  }

  #endregion
}
