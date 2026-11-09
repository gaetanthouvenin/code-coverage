// ------------------------------------------------------------------------------------------------
// <copyright file="GitShaConverterTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Conventions;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Conventions;

public sealed class GitShaConverterTests
{
  #region Fields

  private const string Sha = "a94a8fe5ccb19ba61c4c0873d391e987982fbbd3";

  // Never opened: reading the model does not reach the server.
  private const string UnusedConnection = "Server=localhost;Database=unused";

  private static readonly GitShaConverter Converter = new();

  #endregion

  #region Methods

  [Fact]
  public void ConvertToProvider_ShouldWriteTheCharacters()
  {
    Converter.ConvertToProvider(GitSha.Parse(Sha)).ShouldBe(Sha);
  }

  [Fact]
  public void ConvertFromProvider_ShouldReadTheSha()
  {
    Converter.ConvertFromProvider(Sha).ShouldBe(GitSha.Parse(Sha));
  }

  [Theory]
  [InlineData("not-a-sha")]
  [InlineData("a94a8fe")]
  [InlineData("g94a8fe5ccb19ba61c4c0873d391e987982fbbd3")]
  public void ConvertFromProvider_NotASha_ShouldThrow(string stored)
  {
    Should.Throw<ArgumentException>(() => Converter.ConvertFromProvider(stored));
  }

  // The conversion must not change the column, and the migration with it.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public void Model_ShaColumns_ShouldStayBoundedText(DatabaseProvider provider)
  {
    using var context = DatabaseFixture.CreateContext(provider, UnusedConnection);
    var head = context.Model.FindEntityType(typeof(Branch)).ShouldNotBeNull().FindProperty(nameof(Branch.HeadSha)).ShouldNotBeNull();
    var yaml = context.Model.FindEntityType(typeof(TrackedRepository)).ShouldNotBeNull().FindProperty(nameof(TrackedRepository.DefaultBranchYamlSha)).ShouldNotBeNull();

    foreach (var column in new[] { head, yaml })
    {
      column.GetTypeMapping().Converter.ShouldBeOfType<GitShaConverter>().ProviderClrType.ShouldBe(typeof(string));
      column.GetMaxLength().ShouldBe(GitSha.Length);
    }

    head.IsNullable.ShouldBeFalse();
    yaml.IsNullable.ShouldBeTrue();
  }

  #endregion
}
