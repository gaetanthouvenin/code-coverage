// ------------------------------------------------------------------------------------------------
// <copyright file="IfNotFoundBehavior.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>The outcome of a status when a report it needs is missing (<c>if_not_found</c>).</summary>
public enum IfNotFoundBehavior
{
  /// <summary>The status passes.</summary>
  Success,

  /// <summary>The status fails.</summary>
  Failure,

  /// <summary>The status ends in error.</summary>
  Error,

  /// <summary>The status is ignored.</summary>
  Ignore,
}
