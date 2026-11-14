// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeCommentDeletion.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>What became of the deletion of a comment on its forge.</summary>
public enum ForgeCommentDeletion
{
  /// <summary>The comment was deleted.</summary>
  Deleted,

  /// <summary>The forge no longer had the comment: it was deleted already.</summary>
  Absent,

  /// <summary>The forge refused the deletion: the comment stays.</summary>
  Refused,
}
