// ------------------------------------------------------------------------------------------------
// <copyright file="ICurrentOrganization.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Ports;

/// <summary>The organization the current DI scope has entered, if any.</summary>
/// <remarks>
///   This port only reads. A scope enters an organization once, through <see cref="IOrganizationScope" />
///   or from the row of a job: no caller can choose an organization by its identifier here.
/// </remarks>
public interface ICurrentOrganization
{
  #region Properties

  /// <summary>Gets the identifier of the organization the scope has entered.</summary>
  /// <exception cref="InvalidOperationException">The scope has not entered an organization.</exception>
  long Id { get; }

  /// <summary>Gets a value indicating whether the scope has entered an organization.</summary>
  bool IsKnown { get; }

  #endregion
}
