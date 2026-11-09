// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTokenScope.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Tokens;

/// <summary>What an upload token gives access to.</summary>
public enum UploadTokenScope
{
  /// <summary>Every repository of the organization.</summary>
  Organization,

  /// <summary>One repository.</summary>
  Repository,
}
