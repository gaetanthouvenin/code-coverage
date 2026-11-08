// ------------------------------------------------------------------------------------------------
// <copyright file="UniqueIndexViolationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using Npgsql;

using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Tests.Persistence;

// The engine error is recognized whether a save wraps it (SaveChanges) or a raw statement reports it
// directly (FromSql, ExecuteUpdate). The SQL Server errors are covered against a server.
public sealed class UniqueIndexViolationTests
{
  #region Fields

  private const string IndexName = "UX_Jobs_LockKey_Running";

  #endregion

  #region Methods

  [Fact]
  public void Is_WithEngineErrorReportedDirectly_ShouldRecognizeTheIndex()
  {
    var violation = Violation(IndexName);

    UniqueIndexViolation.Is(violation, IndexName).ShouldBeTrue();
    UniqueIndexViolation.Is(violation, "UX_Jobs_DedupKey_Queued").ShouldBeFalse();
  }

  [Fact]
  public void Is_WithEngineErrorWrappedBySave_ShouldRecognizeTheIndex()
  {
    var exception = new DbUpdateException("save failed", Violation(IndexName));

    UniqueIndexViolation.Is(exception, IndexName).ShouldBeTrue();
  }

  [Fact]
  public void Is_WithAnotherError_ShouldReturnFalse()
  {
    var deadlock = new PostgresException("deadlock", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected);

    UniqueIndexViolation.Is(deadlock, IndexName).ShouldBeFalse();
    UniqueIndexViolation.Is(new InvalidOperationException("other"), IndexName).ShouldBeFalse();
    UniqueIndexViolation.Is(new DbUpdateException("save failed"), IndexName).ShouldBeFalse();
  }

  [Fact]
  public void Is_WithInvalidArguments_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => UniqueIndexViolation.Is(null!, IndexName));
    Should.Throw<ArgumentException>(() => UniqueIndexViolation.Is(Violation(IndexName), " "));
  }

  private static PostgresException Violation(string constraintName) =>
    new("duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation, constraintName: constraintName);

  #endregion
}
