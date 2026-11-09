// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookOutcome.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>What became of a webhook delivery whose signature is valid.</summary>
public enum WebhookOutcome
{
  /// <summary>The delivery was stored and its job queued.</summary>
  Accepted,

  /// <summary>The delivery was already known: nothing changed.</summary>
  Duplicate,

  /// <summary>The delivery concerns no active organization and the instance has no use for it: it was not stored.</summary>
  Dropped,
}
