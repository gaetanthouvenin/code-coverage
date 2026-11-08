// ------------------------------------------------------------------------------------------------
// <copyright file="SourceFiles.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Xml.Linq;

namespace CodeCoverage.Architecture.Tests.Support;

// The source tree of the repository: src/**/*.cs|razor, tests/**/*.cs and the project files.
internal static class SourceFiles
{
  #region Fields

  private const string SolutionFile = "CodeCoverage.slnx";

  private static readonly string[] Skipped = ["bin", "obj", "TestResults", "node_modules"];

  private static readonly Lazy<string> LazyRoot = new(FindRoot);

  #endregion

  #region Properties

  public static string Root => LazyRoot.Value;

  // Every hand-written source file, as an absolute path, in a stable order.
  public static IReadOnlyList<string> All =>
    [.. Enumerate("src", "*.cs"), .. Enumerate("src", "*.razor"), .. Enumerate("tests", "*.cs")];

  #endregion

  #region Methods

  public static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

  // The text of a source file, with LF line endings (File.ReadAllText strips the UTF-8 BOM).
  public static string Read(string path) =>
    File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);

  // The projects a project of {folder}/ names in its .csproj (file names, without extension).
  public static IReadOnlyList<string> ProjectReferences(string project, string folder = "src") =>
    Includes(folder, project, "ProjectReference").Select(Path.GetFileNameWithoutExtension).ToList()!;

  // The packages a project of {folder}/ names in its .csproj (Directory.Build.props excluded).
  public static IReadOnlyList<string> PackageReferences(string project, string folder = "src") =>
    Includes(folder, project, "PackageReference");

  private static List<string> Includes(string folder, string project, string item)
  {
    var file = Path.Combine(Root, folder, project, project + ".csproj");

    return XDocument.Load(file)
                    .Descendants(item)
                    .Select(e => (string?)e.Attribute("Include"))
                    .OfType<string>()
                    .Select(i => i.Replace('\\', '/'))
                    .Order(StringComparer.Ordinal)
                    .ToList();
  }

  private static IEnumerable<string> Enumerate(string folder, string pattern) =>
    Directory.EnumerateFiles(Path.Combine(Root, folder), pattern, SearchOption.AllDirectories)
             .Where(f => !Relative(f).Split('/').Any(s => Skipped.Contains(s, StringComparer.OrdinalIgnoreCase)))
             .Order(StringComparer.Ordinal);

  private static string FindRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFile)))
    {
      directory = directory.Parent;
    }

    return directory?.FullName
           ?? throw new InvalidOperationException($"The repository root ({SolutionFile}) was not found.");
  }

  #endregion
}
