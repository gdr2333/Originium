# Originium

一个基于 **Model Context Protocol (MCP)** 的 AI 记忆服务，为 AI 助手提供长期记忆能力。

## 功能特性

- **写入记忆** — 将文本内容存储到 SQL Server 数据库，并自动生成向量嵌入
- **搜索记忆** — 通过向量相似度（余弦距离）检索相关记忆
- **删除记忆** — 按 ID 删除指定记忆
- **并发控制** — 限制嵌入 API 的并发请求数，控制成本

## 技术栈

| 技术 | 版本 |
|------|------|
| C# | .NET 10.0 |
| 框架 | ASP.NET Core |
| ORM | Entity Framework Core |
| 数据库 | SQL Server |
| 协议 | Model Context Protocol (MCP) |
| 嵌入模型 | BGE-M3 (OpenAI 兼容 API) |

## 项目结构

```
Originium/
├── Program.cs                         # 入口点，配置 DI 与 MCP 服务器
├── Config.json                        # 应用配置
├── Datas/
│   ├── Config.cs                      # 配置模型
│   ├── MemoryDb.cs                    # EF Core DbContext
│   └── MemoryItem.cs                  # 记忆实体
├── Services/
│   ├── ConcurrentManager.cs           # 并发限流
│   └── EmbeddingsGeneratorManager.cs  # 嵌入生成服务
├── Tools/
│   └── MemoryTool.cs                  # MCP 工具定义
└── Properties/
    └── launchSettings.json            # 启动配置
```

## 环境要求

- .NET 10.0 SDK
- SQL Server（LocalDB / Express）运行于 `localhost\SQLEXPRESS`
- 可访问的嵌入 API 端点（默认：Huawei ModelArts MaaS）

## 构建与运行

```bash
# 还原依赖
dotnet restore

# 构建
dotnet build

# 运行（自动创建数据库）
dotnet run

# 清除数据库后运行
dotnet run -- --cleardb

# 清除数据库并重新生成嵌入
dotnet run -- --cleardb --regen-embeddings
```

## MCP 工具

| 工具 | 说明 |
|------|------|
| `WriteMemory(content)` | 写入一条记忆 |
| `SearchMemory(content)` | 搜索相关记忆，返回 `SearchResult[]` |
| `DeleteMemory(id)` | 按 ID 删除记忆 |

## 配置

编辑 `Config.json`：

```json
{
  "MemoryDb": "Server=localhost\\SQLEXPRESS;Database=AIMemoryDevDb;Trusted_Connection=True;TrustServerCertificate=true;",
  "OpenAIEmbeddingEndpoint": "https://api.modelarts-maas.com/v1/embeddings",
  "OpenAIEmbeddingModel": "bge-m3",
  "OpenAIEmbeddingDimension": 1024,
  "OpenAIEmbeddingIsFixedDimension": true,
  "OpenAIEmbeddingConcurrent": 4,
  "OpenAIApiKey": "your-api-key"
}
```

## 启动地址

- HTTP: `http://localhost:6122`
- HTTPS: `https://localhost:5103`

## 架构

```
MCP 客户端 (AI 助手)
    │ MCP 协议 (HTTP)
    ▼
Originium (MCP 服务器)
├── MemoryTool — WriteMemory / SearchMemory / DeleteMemory
├── EmbeddingsGeneratorManager — 调用嵌入 API 生成向量
└── MemoryDb (EF Core) — SQL Server 持久化存储
```

## 许可证

[GNU AGPL v3](LICENSE.txt)
