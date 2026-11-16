// ------------------------------------------------------------------------------------------------
// <copyright file="GetForgeAvailabilityQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Organizations.Queries.GetForgeAvailability;

/// <summary>Handler for the <see cref="GetForgeAvailabilityQuery" /> query.</summary>
/// <param name="availability">The availability of the forges, computed from the settings at each reading.</param>
internal sealed class GetForgeAvailabilityQueryHandler(IForgeAvailability availability)
  : IQueryHandler<GetForgeAvailabilityQuery, ForgeAvailabilitySnapshot>
{
  #region Implementation of IQueryHandler<GetForgeAvailabilityQuery,ForgeAvailabilitySnapshot>

  /// <inheritdoc />
  public ValueTask<ForgeAvailabilitySnapshot> Handle(GetForgeAvailabilityQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    return ValueTask.FromResult(new ForgeAvailabilitySnapshot(availability.AdmissionForges, availability.SignInForges));
  }

  #endregion
}
