using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K12.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressAndParentPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "progress");

            migrationBuilder.AddColumn<string>(
                name: "PinHash",
                schema: "accounts",
                table: "ParentAccounts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PinLockedUntil",
                schema: "accounts",
                table: "ParentAccounts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PinSetAt",
                schema: "accounts",
                table: "ParentAccounts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PinWrongTries",
                schema: "accounts",
                table: "ParentAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KidProgress",
                schema: "progress",
                columns: table => new
                {
                    KidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KidProgress", x => x.KidId);
                });

            migrationBuilder.CreateTable(
                name: "PracticeSessions",
                schema: "progress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastActiveAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Seconds = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeSessions_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "curriculum",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Mistakes",
                schema: "progress",
                columns: table => new
                {
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FirstMissedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastMissedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TimesMissed = table.Column<int>(type: "int", nullable: false),
                    FixedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mistakes", x => new { x.KidId, x.QuestionId });
                    table.ForeignKey(
                        name: "FK_Mistakes_KidProgress_KidId",
                        column: x => x.KidId,
                        principalSchema: "progress",
                        principalTable: "KidProgress",
                        principalColumn: "KidId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mistakes_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "content",
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PracticeDays",
                schema: "progress",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    KidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Answered = table.Column<int>(type: "int", nullable: false),
                    Correct = table.Column<int>(type: "int", nullable: false),
                    Seconds = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeDays", x => new { x.KidId, x.Date });
                    table.ForeignKey(
                        name: "FK_PracticeDays_KidProgress_KidId",
                        column: x => x.KidId,
                        principalSchema: "progress",
                        principalTable: "KidProgress",
                        principalColumn: "KidId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillProgress",
                schema: "progress",
                columns: table => new
                {
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Answered = table.Column<int>(type: "int", nullable: false),
                    Correct = table.Column<int>(type: "int", nullable: false),
                    Recent = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    LastAnsweredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillProgress", x => new { x.KidId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_SkillProgress_KidProgress_KidId",
                        column: x => x.KidId,
                        principalSchema: "progress",
                        principalTable: "KidProgress",
                        principalColumn: "KidId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillProgress_Skills_SkillId",
                        column: x => x.SkillId,
                        principalSchema: "curriculum",
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionAnswers",
                schema: "progress",
                columns: table => new
                {
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Response = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionAnswers", x => new { x.SessionId, x.QuestionId });
                    table.ForeignKey(
                        name: "FK_SessionAnswers_PracticeSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "progress",
                        principalTable: "PracticeSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionAnswers_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "content",
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mistakes_QuestionId",
                schema: "progress",
                table: "Mistakes",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSessions_CourseId",
                schema: "progress",
                table: "PracticeSessions",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSessions_KidId_StartedAt",
                schema: "progress",
                table: "PracticeSessions",
                columns: new[] { "KidId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionAnswers_QuestionId",
                schema: "progress",
                table: "SessionAnswers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillProgress_SkillId",
                schema: "progress",
                table: "SkillProgress",
                column: "SkillId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mistakes",
                schema: "progress");

            migrationBuilder.DropTable(
                name: "PracticeDays",
                schema: "progress");

            migrationBuilder.DropTable(
                name: "SessionAnswers",
                schema: "progress");

            migrationBuilder.DropTable(
                name: "SkillProgress",
                schema: "progress");

            migrationBuilder.DropTable(
                name: "PracticeSessions",
                schema: "progress");

            migrationBuilder.DropTable(
                name: "KidProgress",
                schema: "progress");

            migrationBuilder.DropColumn(
                name: "PinHash",
                schema: "accounts",
                table: "ParentAccounts");

            migrationBuilder.DropColumn(
                name: "PinLockedUntil",
                schema: "accounts",
                table: "ParentAccounts");

            migrationBuilder.DropColumn(
                name: "PinSetAt",
                schema: "accounts",
                table: "ParentAccounts");

            migrationBuilder.DropColumn(
                name: "PinWrongTries",
                schema: "accounts",
                table: "ParentAccounts");
        }
    }
}
