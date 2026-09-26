# Originium-MCP

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![MCP](https://img.shields.io/badge/MCP-2.2.0-blue)

一个基于 MCP 的 AI 记忆服务，为 AI 助手提供长期记忆能力。

## TODO
### 今天也在努力推行源石计划，没有对泰拉文明手软，请普瑞赛斯放心!!!🫡
- [x] 强化 MCP 工具描述（强引导措辞，提升 AI 主动调用率）
- [x] 新增 MCP Prompts：`auto_remember`（自动记忆模式）、`recall_first`（先回忆再回答）
- [x] 新增 MCP Resources：`memory://recent`（最近记忆）、`memory://item/{id}`（按 ID 读取）
- [x] 新增 SQL Server 全文检索路径（`SearchKeywords`）
- [x] 数据库初始化改为 EF Core 迁移机制
- [ ] 补齐单元测试项目
- [ ] 记忆分桶 / 分区（按会话隔离）
- [ ] 嵌入向量维度可配置迁移策略
- [ ] WebUI 管理面板（记忆浏览/搜索/删除可视化）

## 功能特性

- **写入记忆** — 将文本内容存储到 SQL Server 数据库，并自动生成向量嵌入
- **语义搜索** — 通过向量相似度（余弦距离）检索相关记忆
- **关键词搜索** — 通过 SQL Server 全文检索按关键词快速查找，无需生成向量
- **删除记忆** — 按 ID 删除指定记忆
- **MCP 原语** — 提供 Prompts（自动记忆 / 先回忆再回答）与 Resources（最近记忆 / 按 ID 读取）
- **并发控制** — 限制嵌入 API 的并发请求数，控制成本
- **维度校验** — 启动时校验数据库向量列维度与配置是否一致，避免静默错误

## 技术栈

| 技术 | 版本 |
|------|------|
| C# | .NET 10.0 |
| 框架 | ASP.NET Core |
| ORM | Entity Framework Core 10 |
| 数据库 | SQL Server（需启用全文检索功能） |
| 协议 | Model Context Protocol (MCP) 2.2.0 |
| 嵌入模型 | BGE-M3 (OpenAI 兼容 API) |

## 项目结构

```
Originium/
├── Program.cs                         # 入口点，配置 DI 与 MCP 服务器
├── Config.json                        # 应用配置（已被 .gitignore 忽略，需自行创建）
├── Datas/
│   ├── Config.cs                      # 配置模型
│   ├── MemoryDb.cs                    # EF Core DbContext
│   ├── MemoryDbFactory.cs             # 设计时 DbContext 工厂（供 dotnet ef 使用）
│   └── MemoryItem.cs                  # 记忆实体
├── Migrations/                        # EF Core 迁移（含全文检索目录/索引创建）
├── Services/
│   ├── ConcurrentManager.cs           # 并发限流
│   └── EmbeddingsGeneratorManager.cs  # 嵌入生成服务
├── Tools/
│   ├── MemoryTool.cs                  # MCP 工具定义
│   ├── MemoryPromptType.cs            # MCP Prompts 定义
│   └── MemoryResourceType.cs          # MCP Resources 定义
└── Properties/
    └── launchSettings.json            # 启动配置
```

## 快速开始

1. 确保已安装 .NET 10.0 SDK 和 SQL Server（LocalDB / Express 即可），并启用**全文检索（Full-Text Search）**功能
2. 在 `Originium/` 目录下创建 `Config.json`，填入数据库连接串、嵌入 API 端点与 API Key（参考下方[配置](#配置)章节）
3. 运行 `dotnet run`，首次启动会自动应用 EF Core 迁移创建表结构、全文检索目录与索引
4. 将服务地址告知你的 MCP 客户端（AI 助手），由客户端自行接入

## 环境要求

- .NET 10.0 SDK
- SQL Server（LocalDB / Express）运行于 `localhost\SQLEXPRESS`，且已安装全文检索组件
- 可访问的嵌入 API 端点（默认：Huawei ModelArts MaaS）

## 构建与运行

```bash
# 还原依赖
dotnet restore

# 构建
dotnet build

# 运行（自动应用数据库迁移）
dotnet run

# 清除数据库后运行（删除数据库并由迁移重建）
dotnet run -- --cleardb

# 重新生成嵌入
dotnet run -- --regen-embeddings
```

> **参数说明**
> - `--cleardb`：启动前删除现有数据库（开发环境重置用），随后由迁移机制重建
> - `--regen-embeddings`：对库中已有记忆重新调用嵌入 API 生成向量，用于切换嵌入模型或维度后重建索引

### 数据库迁移

表结构与全文检索索引通过 EF Core 迁移管理。修改实体或向量维度后新增迁移：

```bash
dotnet ef migrations add <迁移名称> --project Originium
```

## MCP 工具

| 工具 | 说明 |
|------|------|
| `WriteMemory(content)` | 写入一条记忆 |
| `SearchMemory(content)` | 按语义相似度搜索相关记忆，返回 `SearchResult[]` |
| `SearchKeywords(keywords, limit = 10)` | 按关键词全文检索记忆，返回 `SearchResult[]` |
| `DeleteMemory(id)` | 按 ID 删除记忆 |

## MCP Prompts

| Prompt | 说明 |
|--------|------|
| `auto_remember` | 进入自动记忆模式：指示 AI 主动识别并持久化关键信息 |
| `recall_first(topic?)` | 先回忆再回答：按主题检索记忆后作答 |

## MCP Resources

| Resource | 说明 |
|----------|------|
| `memory://recent` | 最近写入的记忆列表（最多 20 条） |
| `memory://item/{id}` | 按 ID 读取一条记忆 |

## 配置

`Config.json` 已被 `.gitignore` 忽略，请手动创建并编辑：

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
> 因为 SQL Server 的限制，向量维度最大为 1998 维。
> `OpenAIEmbeddingIsFixedDimension` 用于指定模型是否为固定维数，为 `false` 时将会发送指定维度的参数。

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
├── MemoryTool — WriteMemory / SearchMemory / SearchKeywords / DeleteMemory
├── MemoryPromptType — auto_remember / recall_first
├── MemoryResourceType — memory://recent / memory://item/{id}
├── EmbeddingsGeneratorManager — 调用嵌入 API 生成向量
└── MemoryDb (EF Core) — SQL Server 持久化存储 + 全文检索
```

## 许可证

[GNU AGPL v3](LICENSE.txt)
