// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeAvailability.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Forges;

/// <summary>Composes the availability contributors registered as keyed services, one per forge (ADR-014).</summary>
/// <param name="services">The services of the current scope, where each contributor is registered under its <see cref="Forge" />.</param>
/// <remarks>
///   The lists are computed at each reading, from contributors that read their settings through
///   <c>IOptionsMonitor&lt;T&gt;</c>: a change of setting applies without a restart. A new adapter adds its contributor
///   without touching this class.
/// </remarks>
internal sealed class ForgeAvailability(IServiceProvider services) : IForgeAvailability
{
  #region Methods

  // The forges whose contributor is registered and grants the capability, in the order of the enumeration.
  private IReadOnlyList<Forge> ListForges(Func<IForgeAvailabilityContributor, bool> grants) =>
  [
    .. Enum.GetValues<Forge>()
           .Where(forge => services.GetKeyedService<IForgeAvailabilityContributor>(forge) is { } contributor && grants(contributor)),
  ];

  #endregion

  #region Implementation of IForgeAvailability

  /// <inheritdoc />
  public IReadOnlyList<Forge> AdmissionForges => ListForges(static contributor => contributor.CanAdmit);

  /// <inheritdoc />
  public IReadOnlyList<Forge> SignInForges => ListForges(static contributor => contributor.CanSignIn);

  #endregion
}
