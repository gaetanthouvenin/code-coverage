// ------------------------------------------------------------------------------------------------
// <copyright file="UploadPartKind.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Uploads;

/// <summary>What a part of an upload holds.</summary>
public enum UploadPartKind
{
  /// <summary>A coverage report.</summary>
  Report,

  /// <summary>The list of the files of the repository, one path per line.</summary>
  Network,
}
