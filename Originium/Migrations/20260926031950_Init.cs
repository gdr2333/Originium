using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Originium.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Memories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Embedding = table.Column<SqlVector<float>>(type: "vector(1024)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memories", x => x.Id);
                });

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'ft_memory')
                    CREATE FULLTEXT CATALOG ft_memory;
                """, suppressTransaction: true);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'dbo.Memories'))
                    CREATE FULLTEXT INDEX ON dbo.Memories (Content LANGUAGE 2052)
                        KEY INDEX PK_Memories ON ft_memory
                        WITH CHANGE_TRACKING AUTO;
                """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'dbo.Memories'))
                    DROP FULLTEXT INDEX ON dbo.Memories;
                IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'ft_memory')
                    DROP FULLTEXT CATALOG ft_memory;
                """, suppressTransaction: true);

            migrationBuilder.DropTable(
                name: "Memories");
        }
    }
}
