// ------------------------------------------------------------------------------------------------
// <copyright file="IHasDomainEvents.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain;

/// <summary>
///   An entity that records what happened to it; the events are published once the changes are
///   saved, then cleared.
/// </summary>
public interface IHasDomainEvents
{
  #region Properties

  /// <summary>Gets the events recorded since the last save.</summary>
  IReadOnlyList<IDomainEvent> DomainEvents { get; }

  #endregion

  #region Methods

  /// <summary>Clears the recorded events, once published.</summary>
  void ClearDomainEvents();

  #endregion
}
