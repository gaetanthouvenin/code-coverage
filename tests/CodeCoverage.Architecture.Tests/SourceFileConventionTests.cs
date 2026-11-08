// ------------------------------------------------------------------------------------------------
// <copyright file="SourceFileConventionTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using CodeCoverage.Architecture.Tests.Support;

namespace CodeCoverage.Architecture.Tests;

// The text of every source file of src/ and tests/: the copyright header (CONVENTIONS §3.1),
// the "Ctors" region (§3.3) and one type per file, never nested (§3.5).
public sealed partial class SourceFileConventionTests
{
  #region Fields

  private const string Dashes = "------------------------------------------------------------------------------------------------";

  private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

  #endregion

  #region Properties

  public static TheoryData<string, int> WrongHeaders
  {
    get
    {
      var header = ExpectedHeader("src/Sample/Sample.cs");

      string With(int index, string line) =>
        string.Join('\n', header.Select((l, i) => i == index ? line : l)) + "\nnamespace Sample;\n";

      return new TheoryData<string, int>
      {
        { "namespace Sample;\n", 1 },
        { "// --------\n", 1 },
        { With(1, "// <copyright file=\"Other.cs\" company=\"Someone Else\">"), 2 },
        { With(2, "//   Copyright (c) Someone Else. All rights reserved."), 3 },
        { With(5, "namespace Sample;"), 6 },
      };
    }
  }

  #endregion

  #region Methods

  [Fact]
  public void SourceFiles_ShouldStartWithCopyrightHeader()
  {
    var files = SourceFiles.All;

    files.ShouldNotBeEmpty();
    files.Select(f => HeaderViolation(SourceFiles.Relative(f), SourceFiles.Read(f)))
         .OfType<string>()
         .ShouldBeEmpty("every .cs and .razor file starts with the copyright header, then a blank line (CONVENTIONS §3.1)");
  }

  [Fact]
  public void SourceFiles_ShouldNeverUseConstructorsRegion()
  {
    SourceFiles.All
               .Where(f => UsesConstructorsRegion(f, SourceFiles.Read(f)))
               .Select(SourceFiles.Relative)
               .ShouldBeEmpty("the region of the constructors is named Ctors, never Constructors (CONVENTIONS §3.3)");
  }

  [Fact]
  public void SourceFiles_ShouldDeclareOneTopLevelType()
  {
    SourceFiles.All
               .Where(f => f.EndsWith(".cs", StringComparison.Ordinal))
               .SelectMany(f => TypeLayoutViolations(SourceFiles.Relative(f), SourceFiles.Read(f)))
               .ShouldBeEmpty("a file declares at most one type, and no nested type (CONVENTIONS §3.5)");
  }

  [Theory]
  [InlineData("src/Sample/Sample.cs")]
  [InlineData("src/Sample/Components/Sample.razor")]
  public void HeaderViolation_WithExpectedHeader_ShouldReturnNull(string path)
  {
    var text = string.Join('\n', ExpectedHeader(path)) + "\nnamespace Sample;\n";

    HeaderViolation(path, text).ShouldBeNull();
  }

  [Theory]
  [MemberData(nameof(WrongHeaders))]
  public void HeaderViolation_WithWrongHeader_ShouldNameTheFirstWrongLine(string text, int line)
  {
    var violation = HeaderViolation("src/Sample/Sample.cs", text);

    violation.ShouldNotBeNull();
    violation.ShouldStartWith($"src/Sample/Sample.cs:{line}: ");
  }

  [Theory]
  [InlineData("Sample.cs", "class A\n{\n  #region Constructors\n\n  public A() { }\n\n  #endregion\n}\n", true)]
  [InlineData("Sample.cs", "class A\n{\n  #region Ctors\n\n  public A() { }\n\n  #endregion\n}\n", false)]
  [InlineData("Sample.cs", "class A\n{\n  private const string Text = \"#region Constructors\";\n}\n", false)]
  [InlineData("Sample.razor", "@code {\n  #region Constructors\n  #endregion\n}\n", true)]
  public void UsesConstructorsRegion_WithRegion_ShouldDetectConstructors(string path, string text, bool expected)
  {
    UsesConstructorsRegion(path, text).ShouldBe(expected);
  }

  [Theory]
  [InlineData("namespace S;\n\npublic sealed class A;\n")]
  [InlineData("return 0;\n")]
  [InlineData("var app = 1;\n\npublic partial class Program;\n")]
  [InlineData("namespace S;\n\npublic static class E\n{\n  extension(string s)\n  {\n    public int Twice => s.Length * 2;\n  }\n}\n")]
  public void TypeLayoutViolations_WithOneType_ShouldBeEmpty(string text)
  {
    TypeLayoutViolations("Sample.cs", text).ShouldBeEmpty();
  }

  [Theory]
  [InlineData("namespace S;\n\npublic sealed class A;\n\npublic sealed class B;\n", "declares 2 top-level types: A, B")]
  [InlineData("namespace S;\n\npublic sealed class A;\n\npublic delegate void D();\n", "declares 2 top-level types: A, D")]
  [InlineData("namespace S;\n\npublic sealed class A\n{\n  private enum Kind { One }\n}\n", "nests Kind in A")]
  [InlineData("namespace S\n{\n  public record A;\n  public interface IB;\n}\n", "declares 2 top-level types: A, IB")]
  public void TypeLayoutViolations_WithSeveralOrNestedTypes_ShouldExplain(string text, string expected)
  {
    TypeLayoutViolations("Sample.cs", text).ShouldContain($"Sample.cs: {expected}");
  }

  private static string? HeaderViolation(string relativePath, string text)
  {
    var expected = ExpectedHeader(relativePath);
    var lines = text.Split('\n');

    for (var i = 0; i < expected.Count; i++)
    {
      var actual = i < lines.Length ? lines[i] : "(end of file)";

      if (!string.Equals(actual, expected[i], StringComparison.Ordinal))
      {
        return $"{relativePath}:{i + 1}: expected \"{expected[i]}\" but found \"{actual}\"";
      }
    }

    return null;
  }

  // The five lines of the header, then the blank line that follows it.
  private static IReadOnlyList<string> ExpectedHeader(string relativePath)
  {
    var fileName = Path.GetFileName(relativePath);

    return relativePath.EndsWith(".razor", StringComparison.Ordinal)
             ?
             [
               "@* " + Dashes[2..],
               $"   <copyright file=\"{fileName}\" company=\"Gaëtan Thouvenin\">",
               "     Copyright (c) Gaëtan Thouvenin. All rights reserved.",
               "   </copyright>",
               "   " + Dashes[2..] + " *@",
               string.Empty,
             ]
             :
             [
               "// " + Dashes,
               $"// <copyright file=\"{fileName}\" company=\"Gaëtan Thouvenin\">",
               "//   Copyright (c) Gaëtan Thouvenin. All rights reserved.",
               "// </copyright>",
               "// " + Dashes,
               string.Empty,
             ];
  }

  // Roslyn reads the directives of a .cs file, so a string that mentions a region does not count.
  private static bool UsesConstructorsRegion(string path, string text)
  {
    if (!path.EndsWith(".cs", StringComparison.Ordinal))
    {
      return ConstructorsRegion().IsMatch(text);
    }

    return CSharpSyntaxTree.ParseText(text, ParseOptions)
                           .GetRoot()
                           .DescendantTrivia(descendIntoTrivia: true)
                           .Where(t => t.IsKind(SyntaxKind.RegionDirectiveTrivia))
                           .Any(t => ConstructorsRegion().IsMatch(t.ToFullString()));
  }

  private static List<string> TypeLayoutViolations(string relativePath, string text)
  {
    var declarations = CSharpSyntaxTree.ParseText(text, ParseOptions)
                                       .GetRoot()
                                       .DescendantNodes()
                                       .Where(n => n is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax
                                                   && !n.IsKind(SyntaxKind.ExtensionBlockDeclaration))
                                       .ToList();
    var violations = new List<string>();
    var topLevel = declarations.Where(d => !d.Ancestors().Any(a => a is TypeDeclarationSyntax)).ToList();

    if (topLevel.Count > 1)
    {
      var names = string.Join(", ", topLevel.Select(NameOf));

      violations.Add($"{relativePath}: declares {topLevel.Count} top-level types: {names}");
    }

    foreach (var nested in declarations.Except(topLevel))
    {
      var outer = nested.Ancestors().First(a => a is TypeDeclarationSyntax and not ExtensionBlockDeclarationSyntax);

      violations.Add($"{relativePath}: nests {NameOf(nested)} in {NameOf(outer)}");
    }

    return violations;
  }

  private static string NameOf(SyntaxNode declaration) =>
    declaration switch
    {
      BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
      DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
      var _ => declaration.Kind().ToString(),
    };

  [GeneratedRegex(@"^\s*#region\s+Constructors\b", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
  private static partial Regex ConstructorsRegion();

  #endregion
}
