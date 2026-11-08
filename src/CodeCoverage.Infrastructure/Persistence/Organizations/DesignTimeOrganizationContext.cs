// ------------------------------------------------------------------------------------------------
// <copyright file="DesignTimeOrganizationContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Persistence.Organizations;

/// <summary>The organization of the contexts built for <c>dotnet ef</c>: there is none.</summary>
/// <remarks>
///   The tooling builds the model and applies migrations, and neither reads the filter. A query on an
///   organization table made through such a context fails instead of reading the rows of every organization.
/// </remarks>
internal sealed class DesignTimeOrganizationContext : ICurrentOrganization
{
  #region Implementation of ICurrentOrganization

  /// <inheritdoc />
  public long Id => throw new InvalidOperationException("A design-time context has no organization.");

  /// <inheritdoc />
  public bool IsKnown => false;

  #endregion
}
