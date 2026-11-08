// ------------------------------------------------------------------------------------------------
// <copyright file="DataProtectionSecretProtectorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Security;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Security;

[Collection(DatabaseCollection.Name)]
public sealed class DataProtectionSecretProtectorTests(DatabaseFixture database)
{
  #region Fields

  private const string Secret = "-----BEGIN RSA PRIVATE KEY----- s3cr3t";

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public void Protect_ShouldNotExposeThePlaintextAndRoundTrip()
  {
    var protector = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());

    var protectedValue = protector.Protect(Secret);

    protectedValue.ShouldNotContain("s3cr3t");
    protector.Unprotect(protectedValue).ShouldBe(Secret);
  }

  // The dedicated purpose isolates the secrets from every other use of the key ring (antiforgery, cookies).
  [Fact]
  public void Unprotect_ValueOfAnotherPurpose_ShouldThrowCryptographicException()
  {
    var provider = new EphemeralDataProtectionProvider();
    var foreign = provider.CreateProtector("Another.Purpose").Protect(Secret);

    Should.Throw<CryptographicException>(() => new DataProtectionSecretProtector(provider).Unprotect(foreign));
  }

  [Fact]
  public void Unprotect_TamperedValue_ShouldThrowCryptographicException()
  {
    var protector = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());
    var protectedValue = protector.Protect(Secret);
    var tampered = protectedValue[..^2] + (protectedValue[^2] == 'A' ? "B" : "A") + protectedValue[^1];

    Should.Throw<CryptographicException>(() => protector.Unprotect(tampered));
  }

  [Fact]
  public void ProtectAndUnprotect_NullValue_ShouldThrowArgumentNullException()
  {
    var protector = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());

    Should.Throw<ArgumentNullException>(() => protector.Protect(null!));
    Should.Throw<ArgumentNullException>(() => protector.Unprotect(null!));
  }

  // Two hosts (distinct content roots, as the Api and the Web) on the same database: the key created
  // by the first one is stored in DataProtectionKeys and read back by the second one.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Unprotect_FromOtherHostInstance_ShouldRoundTrip(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);

    await using (var context = DatabaseFixture.CreateContext(provider, connectionString))
    {
      await context.Database.MigrateAsync(Ct);
    }

    await using var api = BuildHost(provider, connectionString, "api");
    await using var web = BuildHost(provider, connectionString, "web");

    var protectedValue = api.GetRequiredService<ISecretProtector>().Protect(Secret);

    web.GetRequiredService<ISecretProtector>().Unprotect(protectedValue).ShouldBe(Secret);

    await using var check = DatabaseFixture.CreateContext(provider, connectionString);
    (await check.DataProtectionKeys.CountAsync(Ct)).ShouldBe(1);
  }

  private static ServiceProvider BuildHost(DatabaseProvider provider, string connectionString, string host)
  {
    var configuration = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                          ["ConnectionStrings:CodeCoverage"] = connectionString,
                          ["CodeCoverage:Database:Provider"] = provider.ToString(),
                        })
                        .Build();
    var environment = A.Fake<IHostEnvironment>();
    A.CallTo(() => environment.EnvironmentName).Returns(Environments.Development);
    A.CallTo(() => environment.ContentRootPath).Returns(Path.Combine(Path.GetTempPath(), $"codecoverage-{host}"));

    var services = new ServiceCollection()
                   .AddSingleton<IConfiguration>(configuration)
                   .AddSingleton(environment)
                   .AddLogging()
                   .AddInfrastructure();

    services.AddCodeCoverageDataProtection(configuration, environment);

    return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
  }

  #endregion
}
