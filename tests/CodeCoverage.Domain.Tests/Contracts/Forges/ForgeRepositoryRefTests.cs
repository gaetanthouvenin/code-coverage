// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeRepositoryRefTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Contracts.Forges;

public sealed class ForgeRepositoryRefTests
{
  #region Methods

  [Fact]
  public void Ctor_WithValidValues_ShouldExposeThem()
  {
    var reference = new ForgeRepositoryRef(Forge.GitHub, "42", "1001");

    reference.Forge.ShouldBe(Forge.GitHub);
    reference.ConnectionId.ShouldBe("42");
    reference.ExternalId.ShouldBe("1001");
  }

  [Fact]
  public void Equals_WithSameValues_ShouldBeTrue()
  {
    new ForgeRepositoryRef(Forge.GitHub, "42", "1001").ShouldBe(new ForgeRepositoryRef(Forge.GitHub, "42", "1001"));
  }

  [Fact]
  public void Equals_OnAnotherForge_ShouldBeFalse()
  {
    new ForgeRepositoryRef(Forge.GitHub, "42", "1001").ShouldNotBe(new ForgeRepositoryRef(Forge.GitLab, "42", "1001"));
  }

  [Fact]
  public void Ctor_NullConnection_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => new ForgeRepositoryRef(Forge.GitHub, null!, "1001"));
  }

  [Fact]
  public void Ctor_NullExternalId_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => new ForgeRepositoryRef(Forge.GitHub, "42", null!));
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void Ctor_BlankConnection_ShouldThrow(string connectionId)
  {
    Should.Throw<ArgumentException>(() => new ForgeRepositoryRef(Forge.GitHub, connectionId, "1001"));
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void Ctor_BlankExternalId_ShouldThrow(string externalId)
  {
    Should.Throw<ArgumentException>(() => new ForgeRepositoryRef(Forge.GitHub, "42", externalId));
  }

  [Fact]
  public void Ctor_UndefinedForge_ShouldThrow()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => new ForgeRepositoryRef((Forge)99, "42", "1001"));
  }

  #endregion
}
