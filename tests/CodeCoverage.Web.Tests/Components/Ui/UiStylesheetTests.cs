// ------------------------------------------------------------------------------------------------
// <copyright file="UiStylesheetTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;

using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

// The stylesheets of the UI components: BEM classes prefixed with cc-, colours from the tokens,
// both themes, a visible focus.
public sealed partial class UiStylesheetTests
{
  #region Fields

  private const string Tokens = "wwwroot/css/tokens.css";

  private const string Components = "wwwroot/css/components.css";

  private const string App = "wwwroot/css/app.css";

  #endregion

  #region Methods

  [Theory]
  [InlineData(Components)]
  [InlineData(App)]
  public void Stylesheet_ShouldOnlyDeclareClassesPrefixedWithCc(string path)
  {
    var css = WithoutComments(WebRootFiles.Read(path));

    ClassSelector().Matches(css)
                   .Select(m => m.Groups["name"].Value)
                   .Distinct()
                   .Where(name => !name.StartsWith("cc-", StringComparison.Ordinal))
                   .ShouldBeEmpty("every class of the stylesheets is prefixed with cc- (PLAN M0.12a, M0.12)");
  }

  [Theory]
  [InlineData(Components)]
  [InlineData(App)]
  public void Stylesheet_ShouldTakeEveryColourFromTheTokens(string path)
  {
    var css = WithoutComments(WebRootFiles.Read(path));

    HexColour().Matches(css)
               .Select(m => m.Value)
               .ShouldBeEmpty("the stylesheets read their colours from tokens.css, so that both themes apply");
  }

  [Theory]
  [InlineData(Components)]
  [InlineData(App)]
  public void Stylesheet_ShouldOnlyReadDefinedTokens(string path)
  {
    var tokens = WebRootFiles.Read(Tokens);
    var defined = TokenDefinition().Matches(tokens).Select(m => m.Groups["name"].Value).ToHashSet();

    TokenUse().Matches(WebRootFiles.Read(path))
              .Select(m => m.Groups["name"].Value)
              .Distinct()
              .Where(name => !defined.Contains(name))
              .ShouldBeEmpty("every var() of the stylesheet is defined in tokens.css");
  }

  [Fact]
  public void Tokens_ShouldDefineTheLightAndTheDarkThemes()
  {
    var css = WebRootFiles.Read(Tokens);

    css.ShouldContain("[data-theme=\"light\"] {");
    css.ShouldContain("@media (prefers-color-scheme: dark)");
    css.ShouldContain(":root:not([data-theme=\"light\"])");
    css.ShouldContain("[data-theme=\"dark\"] {");
    css.ShouldContain("--cc-hit-edge:");
    css.ShouldContain("--cc-part-edge:");
    css.ShouldContain("--cc-miss-edge:");
    css.ShouldContain("font-family: \"Plus Jakarta Sans\"");
    css.ShouldContain("font-family: \"JetBrains Mono\"");
  }

  [Fact]
  public void Tokens_ShouldGiveTheDarkThemesTheSameColourTokens()
  {
    var css = WebRootFiles.Read(Tokens);
    var dark = css[css.IndexOf("[data-theme=\"dark\"] {", StringComparison.Ordinal)..];
    var media = css[css.IndexOf("@media (prefers-color-scheme: dark)", StringComparison.Ordinal)..css.IndexOf("[data-theme=\"dark\"] {", StringComparison.Ordinal)];

    var darkNames = TokenDefinition().Matches(dark).Select(m => m.Groups["name"].Value).ToList();
    var mediaNames = TokenDefinition().Matches(media).Select(m => m.Groups["name"].Value).ToList();

    darkNames.ShouldNotBeEmpty();
    mediaNames.ShouldBe(darkNames, "the forced dark theme and the system dark theme define the same tokens");
  }

  [Fact]
  public void Components_ShouldKeepAVisibleFocus()
  {
    var css = WithoutComments(WebRootFiles.Read(Components));

    FocusVisibleOutline().IsMatch(css).ShouldBeTrue("a focus-visible element gets a 2 px accent outline");
    FocusVisibleRemoval().IsMatch(css).ShouldBeFalse("no rule removes the outline of a focus-visible element");
  }

  private static string WithoutComments(string css) => Comment().Replace(css, string.Empty);

  [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
  private static partial Regex Comment();

  // A class in a selector: a dot followed by a name, outside declarations ({...}).
  [GeneratedRegex(@"(?<![\w-])\.(?<name>-?[a-zA-Z_][\w-]*)(?=[^{}]*\{)")]
  private static partial Regex ClassSelector();

  [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b")]
  private static partial Regex HexColour();

  [GeneratedRegex(@"(?<name>--cc-[\w-]+)\s*:")]
  private static partial Regex TokenDefinition();

  [GeneratedRegex(@"var\((?<name>--[\w-]+)")]
  private static partial Regex TokenUse();

  [GeneratedRegex(@"(^|\s):focus-visible\s*\{\s*outline:\s*2px solid var\(--cc-accent\)", RegexOptions.Multiline)]
  private static partial Regex FocusVisibleOutline();

  [GeneratedRegex(@":focus-visible\s*\{[^}]*outline:\s*(none|0)")]
  private static partial Regex FocusVisibleRemoval();

  #endregion
}
