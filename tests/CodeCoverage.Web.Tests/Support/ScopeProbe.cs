// ------------------------------------------------------------------------------------------------
// <copyright file="ScopeProbe.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Infrastructure.Identity;

namespace CodeCoverage.Web.Tests.Support;

// A scoped service that tells the DI scope a request ran in: its principal, and whether the scope
// has been disposed since.
public sealed class ScopeProbe(CurrentUserContext user) : IDisposable
{
  #region Properties

  public CurrentUserContext User { get; } = user;

  public bool IsDisposed { get; private set; }

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => IsDisposed = true;

  #endregion
}
