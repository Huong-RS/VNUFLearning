using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNUFLearning.Migrations
{
    public partial class AddExamSourceFileMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceFileUrl",
                table: "Exams",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileObjectName",
                table: "Exams",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileName",
                table: "Exams",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileType",
                table: "Exams",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SourceFileSize",
                table: "Exams",
                type: "bigint",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceFileUrl",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "SourceFileObjectName",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "SourceFileName",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "SourceFileType",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "SourceFileSize",
                table: "Exams");
        }
    }
}
