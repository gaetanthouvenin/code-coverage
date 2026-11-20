// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Connections;

public sealed class ForgeConnectionTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

  private static readonly Uri BaseAddress = new("https://dev.azure.com/contoso/");

  private static readonly TimeSpan Warning = TimeSpan.FromDays(14);

  #endregion

  #region Methods

  [Fact]
  public void Create_Valid_ShouldBeActiveFromNow()
  {
    var tenant = Guid.NewGuid();
    var connection = ForgeConnection.Create(
      "conn-1",
      7,
      Forge.AzureDevOps,
      ForgeConnectionKind.AzureDevOpsEntraApplication,
      "ABC-Account",
      BaseAddress,
      tenant,
      "service-account",
      "protected-credential",
      "protected-secret",
      Now.AddDays(90),
      Now.ToOffset(TimeSpan.FromHours(2)));

    connection.Id.ShouldBe("conn-1");
    connection.OrganizationId.ShouldBe(7);
    connection.Forge.ShouldBe(Forge.AzureDevOps);
    connection.Kind.ShouldBe(ForgeConnectionKind.AzureDevOpsEntraApplication);
    connection.ExternalAccountId.ShouldBe("abc-account");
    connection.BaseAddress.ShouldBe(BaseAddress);
    connection.TenantId.ShouldBe(tenant);
    connection.ServiceAccountExternalId.ShouldBe("service-account");
    connection.CredentialProtected.ShouldBe("protected-credential");
    connection.WebhookSecretProtected.ShouldBe("protected-secret");
    connection.WebhookSecretPreviousProtected.ShouldBeNull();
    connection.ExpiresAt.ShouldBe(Now.AddDays(90));
    connection.Status.ShouldBe(ForgeConnectionStatus.Active);
    connection.ConnectedAt.ShouldBe(Now);
    connection.ConnectedAt.Offset.ShouldBe(TimeSpan.Zero);
    connection.SuspendedAt.ShouldBeNull();
    connection.LastCheckedAt.ShouldBeNull();
    connection.LastError.ShouldBeNull();
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void Create_EmptyAccount_ShouldThrow(string account)
  {
    Should.Throw<ArgumentException>(() => Create(externalAccountId: account));
  }

  [Fact]
  public void Create_GitHub_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => Create(forge: Forge.GitHub));
  }

  [Theory]
  [InlineData("")]
  [InlineData("Upper")]
  [InlineData("-leading")]
  [InlineData("with/slash")]
  [InlineData("with space")]
  public void Create_MalformedId_ShouldThrow(string id)
  {
    Should.Throw<ArgumentException>(() => Create(id: id));
  }

  [Fact]
  public void Create_TooLongId_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => Create(id: new string('a', ForgeConnection.IdMaxLength + 1)));
  }

  [Fact]
  public void Create_RelativeAddress_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => Create(baseAddress: new Uri("contoso", UriKind.Relative)));
  }

  [Fact]
  public void Create_NotPositiveOrganization_ShouldThrow()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => Create(organizationId: 0));
  }

  [Theory]
  [InlineData("", "secret")]
  [InlineData("credential", " ")]
  public void Create_EmptySecret_ShouldThrow(string credential, string secret)
  {
    Should.Throw<ArgumentException>(() => Create(credential: credential, secret: secret));
  }

  [Fact]
  public void Suspend_Active_ShouldRecordTheDate()
  {
    var connection = Create();

    connection.Suspend(Now.AddHours(1));

    connection.Status.ShouldBe(ForgeConnectionStatus.Suspended);
    connection.SuspendedAt.ShouldBe(Now.AddHours(1));
  }

  [Fact]
  public void Suspend_Suspended_ShouldKeepTheFirstDate()
  {
    var connection = Create();
    connection.Suspend(Now.AddHours(1));

    connection.Suspend(Now.AddHours(2));

    connection.SuspendedAt.ShouldBe(Now.AddHours(1));
  }

  [Fact]
  public void RotateWebhookSecret_Current_ShouldKeepItAsPrevious()
  {
    var connection = Create(secret: "first");

    connection.RotateWebhookSecret("second");
    connection.WebhookSecretProtected.ShouldBe("second");
    connection.WebhookSecretPreviousProtected.ShouldBe("first");

    connection.RotateWebhookSecret("third");
    connection.WebhookSecretProtected.ShouldBe("third");
    connection.WebhookSecretPreviousProtected.ShouldBe("second");
  }

  [Fact]
  public void RotateWebhookSecret_Empty_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => Create().RotateWebhookSecret(" "));
  }

  [Fact]
  public void RotateCredential_New_ShouldReplaceTheCredentialAndItsExpiry()
  {
    var connection = Create(credential: "first", secret: "secret", expiresAt: Now.AddDays(10));
    var expiresAt = new DateTimeOffset(2027, 5, 1, 10, 0, 0, TimeSpan.FromHours(2));

    connection.RotateCredential("second", "service-account", expiresAt);

    connection.CredentialProtected.ShouldBe("second");
    connection.ServiceAccountExternalId.ShouldBe("service-account");
    connection.ExpiresAt.ShouldBe(expiresAt);
    connection.ExpiresAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
    connection.WebhookSecretProtected.ShouldBe("secret");
    connection.WebhookSecretPreviousProtected.ShouldBeNull();
  }

  [Fact]
  public void RotateCredential_NoServiceAccountNorExpiry_ShouldKeepTheAccountAndClearTheExpiry()
  {
    var connection = Create(expiresAt: Now.AddDays(10));
    connection.RotateCredential("second", "service-account", null);

    connection.RotateCredential("third", null, null);

    connection.CredentialProtected.ShouldBe("third");
    connection.ServiceAccountExternalId.ShouldBe("service-account");
    connection.ExpiresAt.ShouldBeNull();
  }

  [Fact]
  public void RotateCredential_Failing_ShouldClearTheFailure()
  {
    var connection = Create();
    connection.RecordCheck(Now, "TF400813: the identity is not authorized.", Warning);

    connection.RotateCredential("second", null, null);

    connection.Status.ShouldBe(ForgeConnectionStatus.Active);
    connection.LastError.ShouldBeNull();
    connection.LastCheckedAt.ShouldBe(Now);
  }

  [Fact]
  public void RotateCredential_Expiring_ShouldMarkActive()
  {
    var connection = Create(expiresAt: Now.AddDays(10));
    connection.RecordCheck(Now, null, Warning);

    connection.RotateCredential("second", null, Now.AddDays(90));

    connection.Status.ShouldBe(ForgeConnectionStatus.Active);
  }

  [Fact]
  public void RotateCredential_Suspended_ShouldStaySuspended()
  {
    var connection = Create();
    connection.RecordCheck(Now, "failure", Warning);
    connection.Suspend(Now.AddHours(1));

    connection.RotateCredential("second", null, null);

    connection.Status.ShouldBe(ForgeConnectionStatus.Suspended);
    connection.LastError.ShouldBeNull();
  }

  [Theory]
  [InlineData(" ", null)]
  [InlineData("credential", " ")]
  public void RotateCredential_Blank_ShouldThrow(string credential, string? serviceAccount)
  {
    var connection = Create();

    Should.Throw<ArgumentException>(() => connection.RotateCredential(credential, serviceAccount, null));
    connection.CredentialProtected.ShouldBe("protected-credential");
  }

  [Fact]
  public void RotateCredential_ServiceAccountTooLong_ShouldThrow()
  {
    var tooLong = new string('a', ForgeConnection.ServiceAccountExternalIdMaxLength + 1);

    Should.Throw<ArgumentException>(() => Create().RotateCredential("credential", tooLong, null));
  }

  [Fact]
  public void RecordCheck_Error_ShouldMarkFailing()
  {
    var connection = Create();

    connection.RecordCheck(Now.AddHours(1), new string('x', ForgeConnection.LastErrorMaxLength + 10), Warning);

    connection.Status.ShouldBe(ForgeConnectionStatus.Failing);
    connection.LastCheckedAt.ShouldBe(Now.AddHours(1));
    connection.LastError.ShouldNotBeNull().Length.ShouldBe(ForgeConnection.LastErrorMaxLength);
  }

  [Fact]
  public void RecordCheck_ExpiresWithinTheWarning_ShouldMarkExpiring()
  {
    var connection = Create(expiresAt: Now.AddDays(10));

    connection.RecordCheck(Now, null, Warning);

    connection.Status.ShouldBe(ForgeConnectionStatus.Expiring);
    connection.LastError.ShouldBeNull();
  }

  [Fact]
  public void RecordCheck_ReportedExpiryWithinTheWarning_ShouldReplaceTheExpiryAndMarkExpiring()
  {
    var connection = Create(expiresAt: Now.AddDays(90));

    connection.RecordCheck(Now, null, Warning, Now.AddDays(5).ToOffset(TimeSpan.FromHours(2)));

    connection.Status.ShouldBe(ForgeConnectionStatus.Expiring);
    connection.ExpiresAt.ShouldBe(Now.AddDays(5));
    connection.ExpiresAt.ShouldNotBeNull().Offset.ShouldBe(TimeSpan.Zero);
  }

  [Fact]
  public void RecordCheck_FailureWithAReportedExpiry_ShouldKeepTheKnownExpiry()
  {
    var connection = Create(expiresAt: Now.AddDays(90));

    connection.RecordCheck(Now, "refused", Warning, Now.AddDays(5));

    connection.Status.ShouldBe(ForgeConnectionStatus.Failing);
    connection.ExpiresAt.ShouldBe(Now.AddDays(90));
  }

  [Fact]
  public void RecordCheck_NoReportedExpiry_ShouldKeepTheKnownExpiry()
  {
    var connection = Create(expiresAt: Now.AddDays(10));

    connection.RecordCheck(Now, null, Warning, null);

    connection.Status.ShouldBe(ForgeConnectionStatus.Expiring);
    connection.ExpiresAt.ShouldBe(Now.AddDays(10));
  }

  [Fact]
  public void RecordCheck_SuccessAfterFailure_ShouldMarkActive()
  {
    var connection = Create(expiresAt: Now.AddDays(90));
    connection.RecordCheck(Now, "refused", Warning);

    connection.RecordCheck(Now.AddDays(1), null, Warning);

    connection.Status.ShouldBe(ForgeConnectionStatus.Active);
    connection.LastError.ShouldBeNull();
  }

  [Fact]
  public void RecordCheck_Suspended_ShouldKeepTheSuspension()
  {
    var connection = Create();
    connection.Suspend(Now);

    connection.RecordCheck(Now.AddHours(1), null, Warning);

    connection.Status.ShouldBe(ForgeConnectionStatus.Suspended);
    connection.LastCheckedAt.ShouldBe(Now.AddHours(1));
  }

  private static ForgeConnection Create(
    string id = "conn-1",
    long organizationId = 7,
    Forge forge = Forge.AzureDevOps,
    string externalAccountId = "account",
    Uri? baseAddress = null,
    string credential = "protected-credential",
    string secret = "protected-secret",
    DateTimeOffset? expiresAt = null) =>
    ForgeConnection.Create(
      id,
      organizationId,
      forge,
      ForgeConnectionKind.AzureDevOpsEntraApplication,
      externalAccountId,
      baseAddress ?? BaseAddress,
      null,
      null,
      credential,
      secret,
      expiresAt,
      Now);

  #endregion
}
