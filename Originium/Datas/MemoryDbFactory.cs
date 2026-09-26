using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging.Abstractions;

namespace Originium.Datas;

public class MemoryDbFactory : IDesignTimeDbContextFactory<MemoryDb>
{
    public MemoryDb CreateDbContext(string[] args)
    {
        var config = JsonSerializer.Deserialize<Config>(File.ReadAllText("Config.json"))!;
        var opt = new DbContextOptionsBuilder<MemoryDb>()
            .UseSqlServer(config.MemoryDb)
            .Options;
        return new(opt, config, NullLogger<MemoryDb>.Instance);
    }
}