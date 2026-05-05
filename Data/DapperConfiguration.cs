using System.Runtime.CompilerServices;
using Dapper;

namespace claude_starter.Data;

internal static class DapperConfiguration
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }
}
