// ------------------------------------------------------------------------------------------------
// <copyright file="WebRootFiles.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Tests.Support;

// Reads the source files of the Web project (wwwroot, resources) from the repository.
public static class WebRootFiles
{
  #region Properties

  public static string ProjectDirectory { get; } = FindProjectDirectory();

  #endregion

  #region Methods

  public static string Read(string relativePath) =>
    File.ReadAllText(Path.Combine(ProjectDirectory, relativePath));

  private static string FindProjectDirectory()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "CodeCoverage.slnx")))
      {
        return Path.Combine(directory.FullName, "src", "CodeCoverage.Web");
      }
    }

    throw new InvalidOperationException("The repository root (CodeCoverage.slnx) was not found.");
  }

  #endregion
}
