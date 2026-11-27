using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K12.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKidNamesAndQuestionReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KidName",
                schema: "curriculum",
                table: "ReportingCategories",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuestionReports",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionVersion = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reporter = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionReports_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "content",
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReports_ClosedAt_ReportedAt",
                schema: "content",
                table: "QuestionReports",
                columns: new[] { "ClosedAt", "ReportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReports_QuestionId",
                schema: "content",
                table: "QuestionReports",
                column: "QuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionReports",
                schema: "content");

            migrationBuilder.DropColumn(
                name: "KidName",
                schema: "curriculum",
                table: "ReportingCategories");
        }
    }
}
