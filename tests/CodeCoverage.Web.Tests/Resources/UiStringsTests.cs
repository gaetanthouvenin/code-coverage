// ------------------------------------------------------------------------------------------------
// <copyright file="UiStringsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Xml.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Extensions;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Resources;

public sealed class UiStringsTests
{
  #region Methods

  [Fact]
  public void Resources_ShouldTranslateEveryKeyInFrench()
  {
    var english = Entries("Resources/UiStrings.resx");
    var french = Entries("Resources/UiStrings.fr.resx");

    english.Keys.ShouldNotBeEmpty();
    french.Keys.Order(StringComparer.Ordinal).ShouldBe(english.Keys.Order(StringComparer.Ordinal));
    english.Concat(french).Where(e => string.IsNullOrWhiteSpace(e.Value)).ShouldBeEmpty();
  }

  [Theory]
  [InlineData("en-US", "Loading…")]
  [InlineData("fr-FR", "Chargement…")]
  public void Localizer_WithCulture_ShouldReadTheMatchingResource(string culture, string expected)
  {
    using var provider = new ServiceCollection().AddLogging().AddCoverageUi().BuildServiceProvider();
    var localizer = provider.GetRequiredService<IStringLocalizer<UiStrings>>();
    var previous = CultureInfo.CurrentUICulture;

    try
    {
      CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

      var text = localizer["Loading"];

      text.ResourceNotFound.ShouldBeFalse();
      text.Value.ShouldBe(expected);
    }
    finally
    {
      CultureInfo.CurrentUICulture = previous;
    }
  }

  [Fact]
  public void AddCoverageUi_WithNullServices_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => UiServiceCollectionExtensions.AddCoverageUi(null!));
  }

  private static Dictionary<string, string> Entries(string relativePath) =>
    XDocument.Parse(WebRootFiles.Read(relativePath))
             .Root!
             .Elements("data")
             .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);

  #endregion
}
