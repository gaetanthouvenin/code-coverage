// ------------------------------------------------------------------------------------------------
// <copyright file="TemporaryGitRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader.Git;

namespace CodeCoverage.Uploader.Tests.Git;

// A git repository in a temporary directory, for the behaviors that only a real git can show.
public sealed class TemporaryGitRepository : IDisposable
{
  #region Fields

  private static readonly string[] Identity =
    ["-c", "user.name=test", "-c", "user.email=test@example.test", "-c", "commit.gpgsign=false"];

  #endregion

  #region Ctors

  public TemporaryGitRepository()
  {
    Directory = Path.Combine(Path.GetTempPath(), $"cc-git-{Guid.NewGuid():N}");
    System.IO.Directory.CreateDirectory(Directory);
    Run("init", "-b", "main");
  }

  private TemporaryGitRepository(string directory)
  {
    Directory = directory;
  }

  #endregion

  #region Properties

  public string Directory { get; }

  #endregion

  #region Methods

  public string Commit(string fileName)
  {
    File.WriteAllText(Path.Combine(Directory, fileName), fileName);
    Run("add", "--", fileName);
    Run([.. Identity, "commit", "-m", fileName]);

    return Git(["rev-parse", "HEAD"]).ShouldNotBeNull();
  }

  public void Run(params string[] arguments) => Git(arguments).ShouldNotBeNull($"git {string.Join(' ', arguments)}");

  public void MergeNoFastForward(string branch) => Run([.. Identity, "merge", "--no-ff", "--no-edit", branch]);

  public string? Git(string[] arguments) => GitRunner.Run(arguments, Directory);

  public string? GitRaw(string[] arguments) => GitRunner.RunRaw(arguments, Directory);

  // git honours --depth on a clone only over a transport, which a plain path is not.
  public TemporaryGitRepository CloneWithDepthOne()
  {
    var directory = Path.Combine(Path.GetTempPath(), $"cc-git-{Guid.NewGuid():N}");

    GitRunner.Run(["clone", "--depth", "1", new Uri(Directory).AbsoluteUri, directory], null).ShouldNotBeNull();

    return new TemporaryGitRepository(directory);
  }

  public void Dispose()
  {
    try
    {
      foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "*", SearchOption.AllDirectories))
      {
        File.SetAttributes(file, FileAttributes.Normal);
      }

      System.IO.Directory.Delete(Directory, recursive: true);
    }
    catch (IOException)
    {
      // The system cleans its temporary directory; a leftover must not fail a test.
    }
    catch (UnauthorizedAccessException)
    {
      // Same as above.
    }
  }

  #endregion
}
