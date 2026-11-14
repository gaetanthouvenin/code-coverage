// ------------------------------------------------------------------------------------------------
// <copyright file="CommentBehavior.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>How the pull request comment is published (<c>comment.behavior</c>).</summary>
public enum CommentBehavior
{
  /// <summary>One comment, updated in place and created again if it was deleted.</summary>
  Default,

  /// <summary>One comment, updated in place and never created again.</summary>
  Once,

  /// <summary>Each notification deletes the comments of the application and writes a new one, at the bottom of the pull request.</summary>
  New,

  /// <summary>Each notification writes a new comment and leaves the earlier ones.</summary>
  Spammy,
}
