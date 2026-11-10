// ------------------------------------------------------------------------------------------------
// <copyright file="NotificationErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Features.Notifications;

/// <summary>The error codes of the notifications.</summary>
public static class NotificationErrors
{
  #region Fields

  /// <summary>The commit to notify, or its repository, no longer exists.</summary>
  public const string CommitNotFound = "Notification.CommitNotFound";

  #endregion
}