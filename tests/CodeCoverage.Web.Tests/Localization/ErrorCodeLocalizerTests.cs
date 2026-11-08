// ------------------------------------------------------------------------------------------------
// <copyright file="ErrorCodeLocalizerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Reflection;
using System.Xml.Linq;

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Application;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Security;
using CodeCoverage.Web.Configuration;
using CodeCoverage.Web.Localization;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Localization;

public sealed class ErrorCodeLocalizerTests : IDisposable
{
  #region Fields

  private readonly ServiceProvider _services;

  private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

  #endregion

  #region Ctors

  public ErrorCodeLocalizerTests()
  {
    _services = new ServiceCollection().AddLogging().AddUiMessaging().BuildServiceProvider();
  }

  #endregion

  #region Properties

  private ErrorCodeLocalizer Localizer => _services.GetRequiredService<ErrorCodeLocalizer>();

  #endregion

  #region Methods

  [Theory]
  [InlineData("en", "You do not have the permission to do this.")]
  [InlineData("fr", "Vous n’avez pas la permission de faire cela.")]
  public void Localize_WithKnownCode_ShouldTranslateItInTheCultureOfTheCircuit(string culture, string expected)
  {
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

    Localizer.Localize(SecurityErrors.Forbidden).ShouldBe(expected);
  }

  [Theory]
  [InlineData("Sample.NoTranslation")]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData(null)]
  public void Localize_WithUnknownOrEmptyCode_ShouldFallBackToUnexpected(string? errorCode)
  {
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

    Localizer.Localize(errorCode).ShouldBe(Localizer.Localize(GeneralErrors.Unexpected));
    Localizer.Localize(errorCode).ShouldStartWith("An unexpected error occurred.");
  }

  [Fact]
  public void Resources_ShouldTranslateEveryErrorCodeOfTheApplication()
  {
    // PLAN §1.3: every Feature.Reason code of a {Feature}Errors class is translated in Common.resx.
    var codes = typeof(DependencyInjection).Assembly
                                           .GetTypes()
                                           .Where(t => t is { IsAbstract: true, IsSealed: true }
                                                       && t.Name.EndsWith("Errors", StringComparison.Ordinal))
                                           .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
                                           .Where(f => f is { IsLiteral: true } && f.FieldType == typeof(string))
                                           .Select(f => (string)f.GetRawConstantValue()!)
                                           .ToList();

    codes.ShouldContain(GeneralErrors.Unexpected);
    codes.ShouldContain(SecurityErrors.Forbidden);

    foreach (var file in new[] { "Resources/Common.resx", "Resources/Common.fr.resx" })
    {
      var keys = XDocument.Parse(WebRootFiles.Read(file))
                          .Root!
                          .Elements("data")
                          .Select(d => (string)d.Attribute("name")!)
                          .ToHashSet(StringComparer.Ordinal);

      codes.Where(c => !keys.Contains(c)).ShouldBeEmpty($"every error code is translated in {file}");
    }
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose()
  {
    CultureInfo.CurrentUICulture = _uiCulture;
    _services.Dispose();
  }

  #endregion
}
