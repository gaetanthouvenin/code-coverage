// ------------------------------------------------------------------------------------------------
// <copyright file="LatestRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Messaging;

/// <summary>
///   Keeps only the answer of the latest load of a component: a load started before another one may answer after it,
///   and its answer must then be dropped rather than replace the newer one.
/// </summary>
/// <remarks>
///   <para>
///     A component holds one instance, takes a ticket with <see cref="Begin" /> before sending its request, and assigns
///     the answer only when <see cref="IsLatest" /> still holds for that ticket. The circuit runs the component on one
///     dispatcher, so the counter needs no lock.
///   </para>
/// </remarks>
public sealed class LatestRequest
{
  #region Fields

  private long _version;

  #endregion

  #region Methods

  /// <summary>Starts a load: every ticket taken before this one stops being the latest.</summary>
  /// <returns>The ticket of the load.</returns>
  public long Begin() => ++_version;

  /// <summary>Tells whether a load is still the latest one started.</summary>
  /// <param name="ticket">The ticket <see cref="Begin" /> returned for the load.</param>
  /// <returns><see langword="true" /> when no load started since; otherwise, <see langword="false" />.</returns>
  public bool IsLatest(long ticket) => ticket == _version;

  #endregion
}
