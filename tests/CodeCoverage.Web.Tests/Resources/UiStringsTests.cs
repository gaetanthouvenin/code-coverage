// ------------------------------------------------------------------------------------------------
// <copyright file="UiStringsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Extensions;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Tests.Resources;

public sealed class UiStringsTests
{
  #region Methods

  [Theory]
  [InlineData("en-US", "Loading…")]
  [InlineData("fr-FR", "Chargement…")]
  [InlineData("it-IT", "Caricamento…")]
  [InlineData("es-ES", "Cargando…")]
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

  #endregion
}
