# Originium-MCP

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![MCP](https://img.shields.io/badge/MCP-2.2.0-blue)

一个基于 MCP 的 AI 记忆服务，为 AI 助手提供长期记忆能力。

## TODO
### 今天也在努力推行源石计划，没有对泰拉文明手软，请普瑞赛斯放心!!!🫡
- [ ] 强化 MCP 工具描述（强引导措辞，提升 AI 主动调用率）
- [ ] 新增 MCP Prompts：`auto_remember`（自动记忆模式）、`recall_first`（先回忆再回答）
- [ ] 新增 MCP Resources：`memory://recent`（最近记忆）、`memory://item/{id}`（按 ID 读取）
- [ ] 补齐单元测试项目
- [ ] 记忆分桶 / 分区（按会话隔离）
- [ ] 嵌入向量维度可配置迁移策略
- [ ] WebUI 管理面板（记忆浏览/搜索/删除可视化）

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
| ORM | Entity Framework Core 10 |
| 数据库 | SQL Server |
| 协议 | Model Context Protocol (MCP) 2.2.0 |
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

## 快速开始

1. 确保已安装 .NET 10.0 SDK 和 SQL Server（LocalDB / Express 即可）
2. 在 `Originium/` 目录下创建 `Config.json`，填入数据库连接串、嵌入 API 端点与 API Key（参考下方[配置](#配置)章节）
3. 运行 `dotnet run`，首次启动会自动创建数据库表结构
4. 将服务地址告知你的 MCP 客户端（AI 助手），由客户端自行接入

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

# 重新生成嵌入
dotnet run -- --regen-embeddings
```

> **参数说明**
> - `--cleardb`：启动前清空现有数据库（开发环境重置用）
> - `--regen-embeddings`：对库中已有记忆重新调用嵌入 API 生成向量，用于切换嵌入模型或维度后重建索引

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

> 默认指向华为 ModelArts MaaS 的 BGE-M3 端点，可替换为任意 OpenAI 兼容的嵌入 API（如本地 Ollama、vLLM 等），只需保证返回格式与维度一致。
> 因为SQL Server的限制，向量维度最大为1998维
> OpenAIEmbeddingIsFixedDimension用于指定模型是否为固定维数，为否时将会发送指定维度的参数。
## 启动地址

- HTTP: `http://localhost:6122`
- HTTPS: `https://localhost:5103`

> 端口定义在 `Properties/launchSettings.json`，修改该文件可自定义。生产环境建议通过 `ASPNETCORE_URLS` 环境变量覆盖。

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
