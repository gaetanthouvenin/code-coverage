// ------------------------------------------------------------------------------------------------
// <copyright file="DocumentationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;

using CodeCoverage.Architecture.Tests.Support;

namespace CodeCoverage.Architecture.Tests;

// The documents of the repository against what is published (M4.02k).
public sealed partial class DocumentationTests
{
  #region Fields

  private static readonly string[] Skipped = ["bin", "obj", "TestResults", "node_modules", ".git", ".claude"];

  // The plan names the defect it fixes and the title of M5.01: it describes, it does not instruct.
  private static readonly string[] Allowed = ["docs/PLAN.md"];

  #endregion

  #region Methods

  // The major tag v1 of the action does not exist yet: the owner sets it at the first stable release. Until then, a
  // workflow copied from a document must pin a release, @vX.Y.Z. Drop this check once the tag v1 is published.
  [Fact]
  public void Documents_ShouldNotCiteTheMajorTagOfTheAction()
  {
    var offenders = Directory.EnumerateFiles(SourceFiles.Root, "*.md", SearchOption.AllDirectories)
                             .Select(SourceFiles.Relative)
                             .Where(static f => !f.Split('/').Any(s => Skipped.Contains(s, StringComparer.OrdinalIgnoreCase)))
                             .Where(static f => !Allowed.Contains(f, StringComparer.Ordinal))
                             .Where(static f => MajorTag().IsMatch(SourceFiles.Read(Path.Combine(SourceFiles.Root, f))))
                             .Order(StringComparer.Ordinal)
                             .ToList();

    offenders.ShouldBeEmpty("These documents cite action@v1, a tag that does not exist: cite @vX.Y.Z.");
  }

  [GeneratedRegex(@"action@v1(?![\w.\-])")]
  private static partial Regex MajorTag();

  #endregion
}
