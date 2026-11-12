// ------------------------------------------------------------------------------------------------
// <copyright file="AuditCategory.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Audit;

/// <summary>The families of the actions of the audit log of the instance, for its filter.</summary>
public enum AuditCategory
{
  /// <summary>A secret replaced or cleared.</summary>
  Secrets,

  /// <summary>Another change of the settings of the instance, the setup included.</summary>
  Settings,

  /// <summary>An organization admitted or suspended.</summary>
  Organizations,

  /// <summary>An API token created or revoked.</summary>
  Tokens,

  /// <summary>A job retried.</summary>
  Retries,
}
