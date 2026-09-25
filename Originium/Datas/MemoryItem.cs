using Microsoft.Data.SqlTypes;
using System.ComponentModel.DataAnnotations;

namespace Originium.Datas;

public class MemoryItem
{
    [Key]
    public uint Id { get; set; }
    public string Content { get; set; } = null!;
    public SqlVector<float> Embedding { get; set; }
}
