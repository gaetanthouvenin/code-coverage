// ------------------------------------------------------------------------------------------------
// <copyright file="ResourceEntryFormatTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;

using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Resources;

// The resource files merge with merge=union, which is only safe when an entry is a single line:
// entries written on several lines share their closing line, and a merge interleaves them.
public sealed class ResourceEntryFormatTests
{
  #region Fields

  private static readonly Regex OneLineEntry =
    new(@"^\s*<data name=""[^""]+"" xml:space=""preserve""><value>[^<]*</value></data>\s*$", RegexOptions.Compiled);

  #endregion

  #region Methods

  [Theory]
  [InlineData("Resources/Common.resx")]
  [InlineData("Resources/Common.fr.resx")]
  [InlineData("Resources/UiStrings.resx")]
  [InlineData("Resources/UiStrings.fr.resx")]
  public void Entries_InResourceFile_ShouldEachFitOnOneLine(string relativePath)
  {
    var resx = WebRootFiles.Read(relativePath);

    resx.ShouldContain("<data ");
    InvalidEntryLines(resx).ShouldBeEmpty();
  }

  [Theory]
  [InlineData("  <data name=\"A\" xml:space=\"preserve\">\n    <value>a</value>\n  </data>")]
  [InlineData("  <data name=\"A\" xml:space=\"preserve\"><value>first\nsecond</value></data>")]
  [InlineData("  <data name=\"A\" xml:space=\"preserve\"><value>a</value><value>b</value></data>")]
  public void InvalidEntryLines_WithAnEntryNotOnOneLine_ShouldReportIt(string entry)
  {
    InvalidEntryLines($"<root>\n{entry}\n</root>").ShouldHaveSingleItem();
  }

  [Fact]
  public void InvalidEntryLines_WithOneLineEntries_ShouldReportNothing()
  {
    const string Resx = "<root>\r\n  <data name=\"A\" xml:space=\"preserve\"><value>a &amp; b</value></data>\r\n</root>\r\n";

    InvalidEntryLines(Resx).ShouldBeEmpty();
  }

  private static List<string> InvalidEntryLines(string resx) =>
    resx.Split('\n')
        .Where(line => line.Contains("<data ", StringComparison.Ordinal) && !OneLineEntry.IsMatch(line))
        .ToList();

  #endregion
}
