// ------------------------------------------------------------------------------------------------
// <copyright file="CommentSection.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>A section of the pull request comment (<c>comment.layout</c>).</summary>
public enum CommentSection
{
  /// <summary>The full header (<c>header</c>).</summary>
  Header,

  /// <summary>The short header (<c>condensed_header</c>).</summary>
  CondensedHeader,

  /// <summary>The table of the project totals (<c>diff</c>).</summary>
  Diff,

  /// <summary>The table of the flags (<c>flags</c> or <c>flag</c>).</summary>
  Flags,

  /// <summary>The table of the impacted files (<c>files</c> or <c>tree</c>).</summary>
  Files,

  /// <summary>The footer (<c>footer</c>).</summary>
  Footer,
}
