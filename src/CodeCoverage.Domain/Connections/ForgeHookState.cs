// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeHookState.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Connections;

/// <summary>The state of a hook created by a forge connection, as last checked, stored as text.</summary>
public enum ForgeHookState
{
  /// <summary>The hook exists at the forge and delivers its events.</summary>
  Active,

  /// <summary>The hook exists at the forge, but the forge or an administrator turned it off.</summary>
  Disabled,

  /// <summary>The hook does not exist at the forge, or not yet.</summary>
  Missing,

  /// <summary>The credential of the connection has no right to read or create the hook.</summary>
  Forbidden,
}
