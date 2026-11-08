// ------------------------------------------------------------------------------------------------
// <copyright file="SettingValueTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Domain.Tests.Settings;

public sealed class SettingValueTests
{
  #region Fields

  private static readonly DateTimeOffset At = new(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);

  private static readonly SettingDefinition Secret =
    SettingDefinitions.Find(SettingDefinitions.GitHubWebhookSecret)!;

  private static readonly SettingDefinition Concurrency =
    SettingDefinitions.Find(SettingDefinitions.ProcessingWorkerConcurrency)!;

  #endregion

  #region Methods

  [Fact]
  public void Create_FromStore_ShouldKeepTheValues()
  {
    var local = new DateTimeOffset(2026, 11, 8, 11, 30, 0, TimeSpan.FromHours(1));

    var value = SettingValue.Create(Concurrency, "8", SettingSource.Store, local, "octocat");

    value.Definition.ShouldBeSameAs(Concurrency);
    value.Key.ShouldBe("Processing:WorkerConcurrency");
    value.Value.ShouldBe("8");
    value.IsDefined.ShouldBeTrue();
    value.Source.ShouldBe(SettingSource.Store);
    value.UpdatedAt.ShouldBe(At);
    value.UpdatedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
    value.UpdatedBy.ShouldBe("octocat");
    value.IsEditable.ShouldBeTrue();
  }

  [Fact]
  public void Create_FromEnvironment_ShouldNotBeEditable()
  {
    var value = SettingValue.Create(Concurrency, "2", SettingSource.Environment, null, null);

    value.IsEditable.ShouldBeFalse();
  }

  [Fact]
  public void Create_DefaultWithoutValue_ShouldNotBeDefined()
  {
    var value = SettingValue.Create(Secret, null, SettingSource.Default, null, null);

    value.Value.ShouldBeNull();
    value.IsDefined.ShouldBeFalse();
    value.IsEditable.ShouldBeTrue();
  }

  [Theory]
  [InlineData(SettingSource.Store)]
  [InlineData(SettingSource.Environment)]
  public void Create_WithoutValueFromProvidingSource_ShouldThrowArgumentException(SettingSource source)
  {
    Should.Throw<ArgumentException>(() => SettingValue.Create(Concurrency, null, source, null, null))
          .ParamName.ShouldBe("value");
  }

  [Fact]
  public void Create_WithUndefinedSource_ShouldThrowArgumentOutOfRangeException()
  {
    Should.Throw<ArgumentOutOfRangeException>(
            () => SettingValue.Create(Concurrency, "4", (SettingSource)42, null, null)
          )
          .ParamName.ShouldBe("source");
  }

  [Fact]
  public void Create_WithNullDefinition_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => SettingValue.Create(null!, "4", SettingSource.Store, null, null))
          .ParamName.ShouldBe("definition");
  }

  [Fact]
  public void Redact_Secret_ShouldDropTheValueAndKeepItDefined()
  {
    var value = SettingValue.Create(Secret, "CfDJ8protected", SettingSource.Store, At, "octocat");

    var redacted = value.Redact();

    redacted.Value.ShouldBeNull();
    redacted.IsDefined.ShouldBeTrue();
    redacted.Source.ShouldBe(SettingSource.Store);
    redacted.UpdatedAt.ShouldBe(At);
    redacted.UpdatedBy.ShouldBe("octocat");
    value.Value.ShouldBe("CfDJ8protected");
  }

  [Fact]
  public void Redact_NotSecret_ShouldReturnTheSameValue()
  {
    var value = SettingValue.Create(Concurrency, "8", SettingSource.Store, At, "octocat");

    value.Redact().ShouldBeSameAs(value);
  }

  #endregion
}
