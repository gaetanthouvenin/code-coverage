// ------------------------------------------------------------------------------------------------
// <copyright file="DataProtectionConfigurationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Security;

namespace CodeCoverage.Infrastructure.Tests.Security;

public sealed class DataProtectionConfigurationTests
{
  #region Methods

  [Fact]
  public void Configure_InProductionWithoutProtection_ShouldThrow()
  {
    var exception = Should.Throw<InvalidOperationException>(() => Configure(Environments.Production, []));

    exception.Message.ShouldContain("DataProtection__AllowUnprotectedKeys");
  }

  [Fact]
  public void Configure_InProductionWithUnprotectedKeysAllowed_ShouldNotThrow()
  {
    Should.NotThrow(() => Configure(Environments.Production, new() { ["DataProtection:AllowUnprotectedKeys"] = "true" }));
  }

  [Theory]
  [InlineData("Development")]
  [InlineData("Staging")]
  [InlineData("Testing")]
  public void Configure_OutsideProductionWithoutProtection_ShouldNotThrow(string environment)
  {
    Should.NotThrow(() => Configure(environment, []));
  }

  [Fact]
  public void Configure_WithCertificateAndKeyVault_ShouldThrow()
  {
    var exception = Should.Throw<InvalidOperationException>(() => Configure(Environments.Production, new()
    {
      ["DataProtection:CertificatePath"] = "dp.pfx",
      ["DataProtection:KeyVaultKeyId"] = "https://vault.vault.azure.net/keys/dp",
    }));

    exception.Message.ShouldContain("only one");
  }

  [Fact]
  public void Configure_WithRelativeKeyVaultKeyId_ShouldThrow()
  {
    var exception = Should.Throw<InvalidOperationException>(
      () => Configure(Environments.Production, new() { ["DataProtection:KeyVaultKeyId"] = "keys/dp" })
    );

    exception.Message.ShouldContain("DataProtection:KeyVaultKeyId");
  }

  [Fact]
  public async Task Configure_InProductionWithCertificate_ShouldEncryptTheKeysWithIt()
  {
    var path = Path.Combine(Path.GetTempPath(), $"codecoverage-dp-{Guid.NewGuid():N}.pfx");

    try
    {
      await File.WriteAllBytesAsync(path, CreateCertificate("p@ss"), TestContext.Current.CancellationToken);

      await using var services = Configure(Environments.Production, new()
      {
        ["DataProtection:CertificatePath"] = path,
        ["DataProtection:CertificatePassword"] = "p@ss",
      });

      services.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlEncryptor.ShouldBeOfType<CertificateXmlEncryptor>();
    }
    finally
    {
      File.Delete(path);
    }
  }

  // Building the encryptor resolves no key: Key Vault is only reached when a key is created or read.
  [Fact]
  public async Task Configure_InProductionWithKeyVault_ShouldEncryptTheKeysWithIt()
  {
    await using var services = Configure(Environments.Production, new()
    {
      ["DataProtection:KeyVaultKeyId"] = "https://codecoverage.vault.azure.net/keys/dataprotection",
    });

    var encryptor = services.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlEncryptor;

    encryptor.ShouldNotBeNull();
    encryptor.GetType().Name.ShouldContain("AzureKeyVault");
  }

  [Fact]
  public async Task Configure_ShouldShareTheKeyRingThroughTheDatabase()
  {
    await using var services = Configure(Environments.Development, []);

    services.GetRequiredService<IOptions<KeyManagementOptions>>()
            .Value.XmlRepository.ShouldBeOfType<EntityFrameworkCoreXmlRepository<CodeCoverageDbContext>>();
    services.GetRequiredService<IOptions<DataProtectionOptions>>()
            .Value.ApplicationDiscriminator.ShouldBe(DataProtectionConfiguration.ApplicationName);
    services.GetRequiredService<ISecretProtector>().ShouldBeOfType<DataProtectionSecretProtector>();
  }

  private static ServiceProvider Configure(string environmentName, Dictionary<string, string?> settings)
  {
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    var environment = A.Fake<IHostEnvironment>();
    A.CallTo(() => environment.EnvironmentName).Returns(environmentName);

    var services = new ServiceCollection()
                   .AddSingleton<IConfiguration>(configuration)
                   .AddLogging()
                   .AddInfrastructure();

    services.AddCodeCoverageDataProtection(configuration, environment);

    return services.BuildServiceProvider();
  }

  private static byte[] CreateCertificate(string password)
  {
    using var key = RSA.Create(2048);
    var request = new CertificateRequest("CN=CodeCoverage Data Protection", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

    return certificate.Export(X509ContentType.Pkcs12, password);
  }

  #endregion
}
