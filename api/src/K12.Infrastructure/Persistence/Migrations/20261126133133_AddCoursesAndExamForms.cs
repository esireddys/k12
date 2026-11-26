using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K12.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoursesAndExamForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryNumber",
                schema: "curriculum",
                table: "Skills",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CourseId",
                schema: "curriculum",
                table: "Skills",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Designation",
                schema: "curriculum",
                table: "Skills",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Courses",
                schema: "curriculum",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Jurisdiction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FrameworkCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FrameworkName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExamForms",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Exam = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Season = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamForms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamForms_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "curriculum",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportingCategories",
                schema: "curriculum",
                columns: table => new
                {
                    Number = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportingCategories", x => new { x.CourseId, x.Number });
                    table.ForeignKey(
                        name: "FK_ReportingCategories_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "curriculum",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExamFormItems",
                schema: "content",
                columns: table => new
                {
                    Number = table.Column<int>(type: "int", nullable: false),
                    ExamFormId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamFormItems", x => new { x.ExamFormId, x.Number });
                    table.ForeignKey(
                        name: "FK_ExamFormItems_ExamForms_ExamFormId",
                        column: x => x.ExamFormId,
                        principalSchema: "content",
                        principalTable: "ExamForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExamFormItems_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "content",
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Skills_CourseId_Code",
                schema: "curriculum",
                table: "Skills",
                columns: new[] { "CourseId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Courses_Jurisdiction_Subject_Code",
                schema: "curriculum",
                table: "Courses",
                columns: new[] { "Jurisdiction", "Subject", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamFormItems_QuestionId",
                schema: "content",
                table: "ExamFormItems",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamForms_CourseId_Exam_Year_Season",
                schema: "content",
                table: "ExamForms",
                columns: new[] { "CourseId", "Exam", "Year", "Season" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Skills_Courses_CourseId",
                schema: "curriculum",
                table: "Skills",
                column: "CourseId",
                principalSchema: "curriculum",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Skills_Courses_CourseId",
                schema: "curriculum",
                table: "Skills");

            migrationBuilder.DropTable(
                name: "ExamFormItems",
                schema: "content");

            migrationBuilder.DropTable(
                name: "ReportingCategories",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "ExamForms",
                schema: "content");

            migrationBuilder.DropTable(
                name: "Courses",
                schema: "curriculum");

            migrationBuilder.DropIndex(
                name: "IX_Skills_CourseId_Code",
                schema: "curriculum",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "CategoryNumber",
                schema: "curriculum",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "CourseId",
                schema: "curriculum",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "Designation",
                schema: "curriculum",
                table: "Skills");
        }
    }
}
