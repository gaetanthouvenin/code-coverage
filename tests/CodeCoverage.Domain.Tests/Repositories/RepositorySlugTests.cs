// ------------------------------------------------------------------------------------------------
// <copyright file="RepositorySlugTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Domain.Tests.Repositories;

public sealed class RepositorySlugTests
{
  #region Methods

  [Fact]
  public void Parse_OwnerAndName_ShouldSplit()
  {
    var slug = RepositorySlug.Parse("Octo-Org/Hello.World");

    slug.Owner.ShouldBe("Octo-Org");
    slug.Namespace.ShouldBeEmpty();
    slug.Name.ShouldBe("Hello.World");
  }

  [Fact]
  public void Parse_CodecovSeparator_ShouldSplit()
  {
    var slug = RepositorySlug.Parse("owner::::repo");

    slug.Owner.ShouldBe("owner");
    slug.Namespace.ShouldBeEmpty();
    slug.Name.ShouldBe("repo");
  }

  [Theory]
  [InlineData("group/sub/sub2/repo")]
  [InlineData("group:::sub:::sub2::::repo")]
  public void Parse_GitLabSubgroups_ShouldSplitAtLastSeparator(string value)
  {
    var slug = RepositorySlug.Parse(value);

    slug.Owner.ShouldBe("group");
    slug.Namespace.ShouldBe("sub/sub2");
    slug.Name.ShouldBe("repo");
  }

  [Fact]
  public void Parse_AzureDevOpsProjectWithSpace_ShouldKeepTheSpace()
  {
    var slug = RepositorySlug.Parse("org/My Project/repo");

    slug.Namespace.ShouldBe("My Project");
  }

  [Theory]
  [InlineData("")]
  [InlineData("no-separator")]
  [InlineData("/repo")]
  [InlineData("owner/")]
  [InlineData("owner//repo")]
  [InlineData("owner/sub//repo")]
  [InlineData("owner/sub/")]
  [InlineData("owner::::")]
  [InlineData("::::repo")]
  [InlineData("owner:::::repo")]
  [InlineData("owner:::::::repo")]
  [InlineData("owner/re:po")]
  [InlineData("owner/sub::::repo")]
  [InlineData("own er/repo")]
  [InlineData("~stale-1001/repo")]
  [InlineData("owner/..")]
  [InlineData("owner/../repo")]
  [InlineData("owner/ repo")]
  [InlineData("owner/re\npo")]
  [InlineData("owner/re\\po")]
  public void Parse_Invalid_ShouldThrow(string value)
  {
    Should.Throw<ArgumentException>(() => RepositorySlug.Parse(value));
  }

  [Fact]
  public void Parse_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => RepositorySlug.Parse(null!));
  }

  [Fact]
  public void Parse_NameOverTheLimit_ShouldThrow()
  {
    var value = "owner/" + new string('a', TrackedRepository.NameMaxLength + 1);

    Should.Throw<ArgumentException>(() => RepositorySlug.Parse(value));
  }

  [Fact]
  public void Parse_NamespaceOverTheLimit_ShouldThrow()
  {
    var value = "owner/" + new string('a', TrackedRepository.NamespaceMaxLength + 1) + "/repo";

    Should.Throw<ArgumentException>(() => RepositorySlug.Parse(value));
  }

  [Fact]
  public void TryParse_Invalid_ShouldReturnFalse()
  {
    RepositorySlug.TryParse("no-separator", out var slug).ShouldBeFalse();

    slug.ShouldBeNull();
  }

  [Fact]
  public void TryParse_Null_ShouldReturnFalse()
  {
    RepositorySlug.TryParse(null, out var _).ShouldBeFalse();
  }

  [Fact]
  public void Create_ReportedLevels_ShouldKeepTheirCaseAndNormalize()
  {
    var slug = RepositorySlug.Create("Group", "Sub/Sub2", "Repo");

    slug.Namespace.ShouldBe("Sub/Sub2");
    slug.Name.ShouldBe("Repo");
    slug.NormalizedNamespace.ShouldBe("sub/sub2");
    slug.NormalizedName.ShouldBe("repo");
  }

  [Fact]
  public void Create_InvalidNamespace_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => RepositorySlug.Create("group", "sub//sub2", "repo"));
  }

  [Theory]
  [InlineData("owner::::repo", "owner/repo")]
  [InlineData("group:::sub::::repo", "group/sub/repo")]
  public void ToString_ShouldWriteSlashes(string value, string expected)
  {
    RepositorySlug.Parse(value).ToString().ShouldBe(expected);
  }

  #endregion
}
