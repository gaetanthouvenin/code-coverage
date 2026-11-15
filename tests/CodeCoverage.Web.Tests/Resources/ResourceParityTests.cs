// ------------------------------------------------------------------------------------------------
// <copyright file="ResourceParityTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;
using System.Xml.Linq;

using CodeCoverage.Web.Localization;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Resources;

// Every language of the console has each resource file, with exactly the keys and the format parameters of the
// English file (M4.02s): a key added in English and forgotten in a language fails here.
public sealed partial class ResourceParityTests
{
  #region Properties

  public static TheoryData<string, string> Translations
  {
    get
    {
      var data = new TheoryData<string, string>();

      foreach (var resource in new[] { "Common", "UiStrings" })
      {
        foreach (var language in ConsoleLanguages.Codes.Where(static c => c != ConsoleLanguages.DefaultCode))
        {
          data.Add(resource, language);
        }
      }

      return data;
    }
  }

  #endregion

  #region Methods

  [Theory]
  [MemberData(nameof(Translations))]
  public void Translation_ShouldHaveExactlyTheKeysOfTheEnglishFile(string resource, string language)
  {
    var english = Entries($"Resources/{resource}.resx");
    var translation = Entries($"Resources/{resource}.{language}.resx");

    english.Keys.ShouldNotBeEmpty();
    english.Keys.Except(translation.Keys).ShouldBeEmpty($"keys missing from {resource}.{language}.resx");
    translation.Keys.Except(english.Keys).ShouldBeEmpty($"keys of {resource}.{language}.resx unknown in English");
    translation.Where(static e => string.IsNullOrWhiteSpace(e.Value)).Select(static e => e.Key).ShouldBeEmpty("empty values");
  }

  [Theory]
  [MemberData(nameof(Translations))]
  public void Translation_ShouldKeepTheFormatParametersOfEachKey(string resource, string language)
  {
    var english = Entries($"Resources/{resource}.resx");
    var translation = Entries($"Resources/{resource}.{language}.resx");

    english.Where(e => translation.TryGetValue(e.Key, out var value) && !Parameters(value).SetEquals(Parameters(e.Value)))
           .Select(static e => e.Key)
           .ShouldBeEmpty($"{resource}.{language}.resx changes the {{0}} parameters of these keys");
  }

  [Fact]
  public void ResourceFiles_ShouldOnlyExistForTheLanguagesOfTheConsole()
  {
    var folder = Path.Combine(WebRootFiles.ProjectDirectory, "Resources");
    var cultures = Directory.EnumerateFiles(folder, "*.resx")
                            .Select(static f => Path.GetFileNameWithoutExtension(f).Split('.'))
                            .Where(static parts => parts.Length > 1)
                            .Select(static parts => parts[^1])
                            .Distinct(StringComparer.Ordinal)
                            .Order(StringComparer.Ordinal)
                            .ToList();

    cultures.ShouldBe(ConsoleLanguages.Codes.Where(static c => c != ConsoleLanguages.DefaultCode).Order(StringComparer.Ordinal));
  }

  private static Dictionary<string, string> Entries(string relativePath) =>
    XDocument.Parse(WebRootFiles.Read(relativePath))
             .Root!
             .Elements("data")
             .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);

  private static HashSet<string> Parameters(string value) =>
    [.. Parameter().Matches(value).Select(static m => m.Value)];

  [GeneratedRegex(@"\{\d+(?:[,:][^}]*)?\}")]
  private static partial Regex Parameter();

  #endregion
}
