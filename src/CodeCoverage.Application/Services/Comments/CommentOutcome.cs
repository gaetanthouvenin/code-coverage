// ------------------------------------------------------------------------------------------------
// <copyright file="CommentOutcome.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Services.Comments;

/// <summary>What became of the comment of a pull request when its head was notified.</summary>
internal enum CommentOutcome
{
  /// <summary>No comment was written: a condition of the publication is not met.</summary>
  Skipped,

  /// <summary>The comment on the forge already holds this body.</summary>
  Unchanged,

  /// <summary>A new comment was written.</summary>
  Created,

  /// <summary>The comment was updated in place.</summary>
  Updated,

  /// <summary>The comment was deleted on the forge and is not written again.</summary>
  Gone,
}
