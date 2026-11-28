using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K12.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParentAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "accounts");

            migrationBuilder.CreateTable(
                name: "ParentAccounts",
                schema: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LoginSubject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSignedInAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KidProfiles",
                schema: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Avatar = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KidProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KidProfiles_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "curriculum",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_KidProfiles_ParentAccounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalSchema: "accounts",
                        principalTable: "ParentAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KidProfiles_CourseId",
                schema: "accounts",
                table: "KidProfiles",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_KidProfiles_ParentAccountId",
                schema: "accounts",
                table: "KidProfiles",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentAccounts_LoginProvider_LoginSubject",
                schema: "accounts",
                table: "ParentAccounts",
                columns: new[] { "LoginProvider", "LoginSubject" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KidProfiles",
                schema: "accounts");

            migrationBuilder.DropTable(
                name: "ParentAccounts",
                schema: "accounts");
        }
    }
}
