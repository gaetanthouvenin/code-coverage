// ------------------------------------------------------------------------------------------------
// <copyright file="ConnectionSecretKind.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Connections;

/// <summary>The secret of a forge connection an administrator replaces.</summary>
public enum ConnectionSecretKind
{
  /// <summary>The credential the instance uses to reach the forge, verified by the forge before it is kept.</summary>
  Credential,

  /// <summary>The secret that signs the webhooks; the current one stays accepted until the next rotation.</summary>
  WebhookSecret,
}
