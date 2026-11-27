using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K12.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseKindAndTypicalGrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "curriculum",
                table: "Courses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                // Courses imported before this column are all whole grades (Grade 3 math).
                defaultValue: "Grade");

            migrationBuilder.AddColumn<int>(
                name: "TypicalGrade",
                schema: "curriculum",
                table: "Courses",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "curriculum",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "TypicalGrade",
                schema: "curriculum",
                table: "Courses");
        }
    }
}
