// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubEventErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Features.GitHubEvents;

/// <summary>The error codes of the webhooks of GitHub.</summary>
public static class GitHubEventErrors
{
  #region Fields

  /// <summary>The signature of the delivery is missing or matches no secret.</summary>
  public const string InvalidSignature = "GitHubEvent.InvalidSignature";

  /// <summary>The body of the delivery cannot be read.</summary>
  public const string InvalidPayload = "GitHubEvent.InvalidPayload";

  /// <summary>The identifier or the event of the delivery is missing or too long.</summary>
  public const string InvalidDelivery = "GitHubEvent.InvalidDelivery";

  #endregion
}
