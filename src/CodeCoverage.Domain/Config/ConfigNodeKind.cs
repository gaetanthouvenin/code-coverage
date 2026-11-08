// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigNodeKind.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>What a <see cref="ConfigNode" /> holds.</summary>
public enum ConfigNodeKind
{
  /// <summary>An explicit empty value.</summary>
  Null,

  /// <summary>A true or false value.</summary>
  Boolean,

  /// <summary>A number.</summary>
  Number,

  /// <summary>A text.</summary>
  Text,

  /// <summary>Named values, in the order of the file.</summary>
  Map,

  /// <summary>A sequence of values.</summary>
  List,
}
