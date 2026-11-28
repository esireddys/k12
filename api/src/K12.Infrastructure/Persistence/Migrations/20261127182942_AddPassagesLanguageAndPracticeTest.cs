using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K12.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPassagesLanguageAndPracticeTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Courses_Jurisdiction_Subject_Code",
                schema: "curriculum",
                table: "Courses");

            migrationBuilder.AddColumn<Guid>(
                name: "PassageId",
                schema: "content",
                table: "Questions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReportingCategory",
                schema: "content",
                table: "Questions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "curriculum",
                table: "Courses",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<string>(
                name: "PracticeTestJson",
                schema: "curriculum",
                table: "Courses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Passages",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Passages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Passages_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "curriculum",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_PassageId",
                schema: "content",
                table: "Questions",
                column: "PassageId");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_Jurisdiction_Subject_Code_Language",
                schema: "curriculum",
                table: "Courses",
                columns: new[] { "Jurisdiction", "Subject", "Code", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Passages_CourseId",
                schema: "content",
                table: "Passages",
                column: "CourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Passages_PassageId",
                schema: "content",
                table: "Questions",
                column: "PassageId",
                principalSchema: "content",
                principalTable: "Passages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Passages_PassageId",
                schema: "content",
                table: "Questions");

            migrationBuilder.DropTable(
                name: "Passages",
                schema: "content");

            migrationBuilder.DropIndex(
                name: "IX_Questions_PassageId",
                schema: "content",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Courses_Jurisdiction_Subject_Code_Language",
                schema: "curriculum",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "PassageId",
                schema: "content",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "ReportingCategory",
                schema: "content",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Language",
                schema: "curriculum",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "PracticeTestJson",
                schema: "curriculum",
                table: "Courses");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_Jurisdiction_Subject_Code",
                schema: "curriculum",
                table: "Courses",
                columns: new[] { "Jurisdiction", "Subject", "Code" },
                unique: true);
        }
    }
}
