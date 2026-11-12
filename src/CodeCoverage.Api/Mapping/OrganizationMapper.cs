// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Api.Contracts.Organizations;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Features.Organizations.Commands.ResetOrganizationSetting;
using CodeCoverage.Application.Features.Organizations.Commands.UpdateOrganizationSetting;
using CodeCoverage.Application.Features.Organizations.Queries.GetOrganizationSettings;
using CodeCoverage.Application.Features.Organizations.Queries.ListMyOrganizations;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Api.Mapping;

/// <summary>Converts the organizations and their settings between the Domain and the API.</summary>
internal static class OrganizationMapper
{
  #region Methods

  /// <summary>Lists the organizations of the user and converts them for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The organizations, by login.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<OrganizationResponse>> ListMyOrganizationsResponseAsync(this ISender sender, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var organizations = await sender.Send(new ListMyOrganizationsQuery(), cancellationToken).ConfigureAwait(false);

    return [.. organizations.Select(static m => new OrganizationResponse(ForgeSegments.ToSegment(m.Organization.Forge), m.Organization.Login, m.Role.ToString()))];
  }

  /// <summary>Reads the settings of an organization and converts them for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="organization">The login of the organization.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The settings, by key.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<OrganizationSettingResponse>> GetOrganizationSettingsResponseAsync(
    this ISender sender,
    string forge,
    string organization,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var settings = await sender.Send(new GetOrganizationSettingsQuery(ToForge(forge), organization), cancellationToken).ConfigureAwait(false);

    return
    [
      .. settings.Select(static s => new OrganizationSettingResponse(
                           s.Key,
                           s.Definition.Type.ToString(),
                           s.Value ?? s.Definition.Default,
                           s.Definition.Default,
                           s.Source.ToString(),
                           s.UpdatedAt,
                           s.UpdatedBy)),
    ];
  }

  /// <summary>Changes a setting of an organization.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="organization">The login of the organization.</param>
  /// <param name="key">The key of the setting.</param>
  /// <param name="request">The new value.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the setting is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> or <paramref name="request" /> is <see langword="null" />.</exception>
  public static async Task UpdateOrganizationSettingAsync(
    this ISender sender,
    string forge,
    string organization,
    string key,
    UpdateOrganizationSettingRequest request,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(request);

    await sender.Send(new UpdateOrganizationSettingCommand(ToForge(forge), organization, key, request.Value), cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Puts a setting of an organization back to its default.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="organization">The login of the organization.</param>
  /// <param name="key">The key of the setting.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the setting is reset.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task ResetOrganizationSettingAsync(this ISender sender, string forge, string organization, string key, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new ResetOrganizationSettingCommand(ToForge(forge), organization, key), cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Reads the forge segment of the address of an organization: an unknown forge is an unknown organization.</summary>
  /// <param name="forge">The segment.</param>
  /// <returns>The forge.</returns>
  /// <exception cref="NotFoundException">The segment names no forge.</exception>
  private static Forge ToForge(string forge) =>
    ForgeSegments.TryParse(forge, out var parsed)
      ? parsed
      : throw new NotFoundException(OrganizationErrors.NotFound, "The organization does not exist or cannot be read.");

  #endregion
}
