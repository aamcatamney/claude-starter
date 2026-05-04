using System.Data;

namespace claude_starter.Data;

public interface IDbConnectionFactory
{
    IDbConnection Create();
}
