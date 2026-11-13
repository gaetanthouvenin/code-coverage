// ------------------------------------------------------------------------------------------------
// <copyright file="ProcessingLogViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>The processing log of an upload, as stored: never more than its limits.</summary>
/// <param name="Messages">The messages kept, in the order they were reported.</param>
/// <param name="MoreMessages">The number of messages reported but not kept.</param>
/// <param name="Paths">The unresolved or ambiguous paths kept, the first ones reported.</param>
/// <param name="MorePaths">The number of paths reported but not kept.</param>
public sealed record ProcessingLogViewModel(IReadOnlyList<LogMessageViewModel> Messages, int MoreMessages, IReadOnlyList<string> Paths, int MorePaths)
{
  #region Properties

  /// <summary>Gets the empty log, the one of a carried session.</summary>
  public static ProcessingLogViewModel Empty { get; } = new([], 0, [], 0);

  #endregion
}
