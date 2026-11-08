// ------------------------------------------------------------------------------------------------
// <copyright file="CommonTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Xml.Linq;

using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Resources;

public sealed class CommonTests
{
  #region Methods

  [Fact]
  public void Resources_ShouldTranslateEveryKeyInFrench()
  {
    var english = Entries("Resources/Common.resx");
    var french = Entries("Resources/Common.fr.resx");

    english.Keys.ShouldNotBeEmpty();
    french.Keys.Order(StringComparer.Ordinal).ShouldBe(english.Keys.Order(StringComparer.Ordinal));
    english.Concat(french).Where(e => string.IsNullOrWhiteSpace(e.Value)).ShouldBeEmpty();
  }

  private static Dictionary<string, string> Entries(string relativePath) =>
    XDocument.Parse(WebRootFiles.Read(relativePath))
             .Root!
             .Elements("data")
             .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);

  #endregion
}
