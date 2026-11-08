// ------------------------------------------------------------------------------------------------
// <copyright file="SafeXmlReaderFactoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml;

using CodeCoverage.Infrastructure.Reports;

namespace CodeCoverage.Infrastructure.Tests.Reports;

public sealed class SafeXmlReaderFactoryTests
{
  #region Fields

  private const string DtdInFixture = "http://xxe.invalid/evil.dtd";
  private const string EntityInFixture = "file:///etc/passwd";
  private const string Secret = "secret-that-must-not-leak";

  #endregion

  #region Methods

  [Fact]
  public void Read_XxePayload_ShouldNotResolveEntity()
  {
    var directory = Directory.CreateTempSubdirectory("xxe-");

    try
    {
      var secretPath = Path.Combine(directory.FullName, "secret.txt");
      var dtdPath = Path.Combine(directory.FullName, "empty.dtd");
      File.WriteAllText(secretPath, Secret);
      File.WriteAllText(dtdPath, string.Empty);
      var payload = ReadFixture().Replace(DtdInFixture, new Uri(dtdPath).AbsoluteUri, StringComparison.Ordinal)
                                 .Replace(EntityInFixture, new Uri(secretPath).AbsoluteUri, StringComparison.Ordinal);

      // The payload works: a reader that resolves external entities discloses the secret.
      ReadPermissively(payload).ShouldContain(Secret);

      var refusal = Should.Throw<XmlException>(() => ReadSafely(payload));

      refusal.Message.ShouldNotContain(Secret);
    }
    finally
    {
      directory.Delete(recursive: true);
    }
  }

  [Fact]
  public void Read_ExternalDoctype_ShouldReadWithoutConnecting()
  {
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var report = $"""
                  <?xml version="1.0" encoding="utf-8"?>
                  <!DOCTYPE report PUBLIC "-//JACOCO//DTD Report 1.1//EN" "http://127.0.0.1:{port}/report.dtd">
                  <report name="sample" />
                  """;
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(report));
    using var reader = SafeXmlReaderFactory.Create(stream);

    reader.MoveToContent().ShouldBe(XmlNodeType.Element);

    reader.LocalName.ShouldBe("report");
    reader.GetAttribute("name").ShouldBe("sample");
    listener.Pending().ShouldBeFalse();
  }

  [Fact]
  public void Create_NullStream_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => SafeXmlReaderFactory.Create((Stream)null!));
  }

  [Fact]
  public void Create_NullText_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => SafeXmlReaderFactory.Create((TextReader)null!));
  }

  private static string ReadFixture() =>
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "security", "xxe.xml"));

  private static string ReadSafely(string payload)
  {
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
    using var reader = SafeXmlReaderFactory.Create(stream);

    return ReadAll(reader);
  }

  private static string ReadPermissively(string payload)
  {
    var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = new XmlUrlResolver() };
    using var text = new StringReader(payload);
    using var reader = XmlReader.Create(text, settings);

    return ReadAll(reader);
  }

  private static string ReadAll(XmlReader reader)
  {
    var content = new StringBuilder();

    while (reader.Read())
    {
      content.Append(reader.Value);
    }

    return content.ToString();
  }

  #endregion
}
