// ------------------------------------------------------------------------------------------------
// <copyright file="NotificationGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;

namespace CodeCoverage.Application.Features.Notifications;

/// <summary>
///   The refusals of the notifications, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the
///   message of each.
/// </summary>
internal static class NotificationGuards
{
  #region Methods

  extension(NotFoundException)
  {
    /// <summary>Throws when the commit to notify, its repository or its organization no longer exists.</summary>
    /// <typeparam name="T">The type of what was looked for.</typeparam>
    /// <param name="target">What was looked for.</param>
    /// <exception cref="NotFoundException"><paramref name="target" /> is <see langword="null" /> (<see cref="NotificationErrors.CommitNotFound" />).</exception>
    public static void ThrowIfNotifiedTargetGone<T>([NotNull] T? target)
      where T : class
    {
      if (target is null)
      {
        throw new NotFoundException(NotificationErrors.CommitNotFound, "The commit to notify no longer exists.");
      }
    }
  }

  #endregion
}
