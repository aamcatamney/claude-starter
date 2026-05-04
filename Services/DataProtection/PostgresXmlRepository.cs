using System.Xml.Linq;
using Dapper;
using Microsoft.AspNetCore.DataProtection.Repositories;
using claude_starter.Data;

namespace claude_starter.Services.DataProtection;

public sealed class PostgresXmlRepository : IXmlRepository
{
    private readonly IDbConnectionFactory _factory;

    public PostgresXmlRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var conn = _factory.Create();
        var rows = conn.Query<string>("SELECT xml FROM data_protection_keys");
        return rows.Select(XElement.Parse).ToList();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var conn = _factory.Create();
        conn.Execute(
            @"INSERT INTO data_protection_keys (friendly_name, xml)
              VALUES (@friendlyName, @xml)",
            new { friendlyName, xml = element.ToString(SaveOptions.DisableFormatting) });
    }
}
