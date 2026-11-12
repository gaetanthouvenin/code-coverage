// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationsMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.Organizations.Commands.AdmitOrganization;
using CodeCoverage.Application.Features.Organizations.Commands.SuspendOrganization;
using CodeCoverage.Application.Features.Organizations.Queries.ListOrganizations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Web.ViewModels.Organizations;

namespace CodeCoverage.Web.Mapping;

/// <summary>Converts the organizations of the instance between the Domain and the pages.</summary>
internal static class OrganizationsMapper
{
  #region Methods

  /// <summary>Lists the organizations of the instance for the page of its administrators.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The organizations, by login.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<OrganizationRowViewModel>> GetOrganizationRowsViewModelAsync(this ISender sender, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var organizations = await sender.Send(new ListOrganizationsQuery(), cancellationToken).ConfigureAwait(false);

    return [.. organizations.Select(ToViewModel)];
  }

  /// <summary>Admits an account: a pending one, or one admitted directly.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address of the account.</param>
  /// <param name="login">The login of the account.</param>
  /// <param name="externalAccountId">The identifier of the account on its forge.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once it is admitted.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> AdmitOrganizationAsync(
    this ISender sender,
    string forge,
    string login,
    string externalAccountId,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new AdmitOrganizationCommand(ForgeOf(forge), login, externalAccountId), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Suspends an organization.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of its address.</param>
  /// <param name="login">Its login.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once it is suspended.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> SuspendOrganizationAsync(this ISender sender, string forge, string login, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new SuspendOrganizationCommand(ForgeOf(forge), login), cancellationToken).ConfigureAwait(false);

    return true;
  }

  // The forges the page offers are the ones it listed: GitHub until M8 and M9.
  private static Forge ForgeOf(string forge) => ForgeSegments.TryParse(forge, out var parsed) ? parsed : Forge.GitHub;

  private static OrganizationRowViewModel ToViewModel(OrganizationOverview overview)
  {
    var organization = overview.Organization;

    return new OrganizationRowViewModel(
      ForgeSegments.ToSegment(organization.Forge),
      organization.Forge.ToString(),
      organization.Login,
      organization.ExternalAccountId,
      organization.AccountType is OrganizationAccountType.User,
      organization.State switch
      {
        OrganizationState.Active => OrganizationLifecycle.Active,
        OrganizationState.Suspended => OrganizationLifecycle.Suspended,
        var _ => OrganizationLifecycle.Pending,
      },
      organization.SuspensionReason?.ToString(),
      organization.AdmittedBy,
      overview.QueueDepth > 0 ? overview.QueueDepth : null
    );
  }

  #endregion
}
