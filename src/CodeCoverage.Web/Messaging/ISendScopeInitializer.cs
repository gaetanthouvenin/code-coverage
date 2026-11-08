// ------------------------------------------------------------------------------------------------
// <copyright file="ISendScopeInitializer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Messaging;

/// <summary>
///   Prepares the dependency-injection scope that <see cref="ScopedSender" /> opens for each
///   request, before the request is sent in it.
/// </summary>
/// <remarks>
///   The host implements it under <c>Configuration</c>, the only place of the Web allowed to touch
///   the Infrastructure: it copies the user of the circuit into the principal of the new scope.
/// </remarks>
public interface ISendScopeInitializer
{
  #region Methods

  /// <summary>Prepares a scope just opened for one request.</summary>
  /// <param name="scopeServices">The services of the new scope.</param>
  /// <exception cref="ArgumentNullException"><paramref name="scopeServices" /> is <see langword="null" />.</exception>
  void Initialize(IServiceProvider scopeServices);

  #endregion
}
