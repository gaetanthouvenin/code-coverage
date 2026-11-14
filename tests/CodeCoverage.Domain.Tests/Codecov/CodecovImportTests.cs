// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovImportTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Codecov;

namespace CodeCoverage.Domain.Tests.Codecov;

public sealed class CodecovImportTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 14, 8, 0, 0, TimeSpan.FromHours(1));

  #endregion

  #region Methods

  [Fact]
  public void Start_ShouldBeginAtTheFirstPageInUtc()
  {
    var import = CodecovImport.Start(5, "protected", Now);

    import.RepositoryId.ShouldBe(5);
    import.ProtectedToken.ShouldBe("protected");
    import.CreatedAt.ShouldBe(Now);
    import.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
    import.NextPage.ShouldBe(1);
    import.ImportedCommits.ShouldBe(0);
  }

  [Fact]
  public void RecordPage_ShouldAdvanceAndCount()
  {
    var import = CodecovImport.Start(5, "protected", Now);

    import.RecordPage(10);
    import.RecordPage(0);

    import.NextPage.ShouldBe(3);
    import.ImportedCommits.ShouldBe(10);
    Should.Throw<ArgumentOutOfRangeException>(() => import.RecordPage(-1));
  }

  [Fact]
  public void IsExpired_AfterSevenDays_ShouldBeTrue()
  {
    var import = CodecovImport.Start(5, "protected", Now);

    import.IsExpired(Now + CodecovImport.CredentialLifetime).ShouldBeFalse();
    import.IsExpired(Now + CodecovImport.CredentialLifetime + TimeSpan.FromSeconds(1)).ShouldBeTrue();
    CodecovImport.CredentialLifetime.ShouldBe(TimeSpan.FromDays(7));
  }

  [Fact]
  public void Start_InvalidArguments_ShouldThrow()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => CodecovImport.Start(0, "protected", Now));
    Should.Throw<ArgumentException>(() => CodecovImport.Start(5, " ", Now));
    Should.Throw<ArgumentNullException>(() => CodecovImport.Start(5, null!, Now));
  }

  #endregion
}
