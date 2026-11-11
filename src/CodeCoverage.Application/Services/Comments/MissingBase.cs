// ------------------------------------------------------------------------------------------------
// <copyright file="MissingBase.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Services.Comments;

/// <summary>What a cell of coverage writes for its change when the base does not hold what it describes.</summary>
/// <remarks>As <c>make_metrics</c> of Codecov, which receives <c>False</c> for a file and <c>None</c> for a flag.</remarks>
internal enum MissingBase
{
  /// <summary>No impact (<c>ø</c>): a file the base does not hold.</summary>
  NoImpact,

  /// <summary>Unknown (<c>?</c>): a flag the base does not hold.</summary>
  Unknown,
}
