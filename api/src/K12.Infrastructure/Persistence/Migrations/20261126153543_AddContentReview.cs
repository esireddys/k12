using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K12.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiReviewJson",
                schema: "content",
                table: "Questions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OwnerDecidedAt",
                schema: "content",
                table: "Questions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerDecidedBy",
                schema: "content",
                table: "Questions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerNote",
                schema: "content",
                table: "Questions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerOutcome",
                schema: "content",
                table: "Questions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiReviewJson",
                schema: "content",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "OwnerDecidedAt",
                schema: "content",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "OwnerDecidedBy",
                schema: "content",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "OwnerNote",
                schema: "content",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "OwnerOutcome",
                schema: "content",
                table: "Questions");
        }
    }
}
