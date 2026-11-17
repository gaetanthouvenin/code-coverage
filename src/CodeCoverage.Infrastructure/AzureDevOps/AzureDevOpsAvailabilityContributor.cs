// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsAvailabilityContributor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Infrastructure.Forges;

namespace CodeCoverage.Infrastructure.AzureDevOps;

/// <summary>Tells whether Azure DevOps can admit organizations, from the settings of the Microsoft Entra ID application.</summary>
/// <param name="options">The <c>AzureDevOps</c> settings group, read at each call.</param>
/// <remarks>
///   Registered as the <see cref="IForgeAvailabilityContributor" /> keyed by
///   <see cref="Domain.Organizations.Forge.AzureDevOps" />: <c>AzureDevOps:ClientId</c> and a client secret or a
///   certificate rule the admission. The sign-in with Entra ID is not built yet, so it is never offered.
/// </remarks>
internal sealed class AzureDevOpsAvailabilityContributor(IOptionsMonitor<AzureDevOpsOptions> options) : IForgeAvailabilityContributor
{
  #region Implementation of IForgeAvailabilityContributor

  /// <inheritdoc />
  public bool CanAdmit => options.CurrentValue.HasCredential();

  /// <inheritdoc />
  public bool CanSignIn => false;

  #endregion
}
