using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNUFLearning.Migrations
{
    public partial class AddAiGradingToExamDetail : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiFeedback",
                table: "ExamDetails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EssayScore",
                table: "ExamDetails",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SimilarityPercent",
                table: "ExamDetails",
                type: "float",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiFeedback",
                table: "ExamDetails");

            migrationBuilder.DropColumn(
                name: "EssayScore",
                table: "ExamDetails");

            migrationBuilder.DropColumn(
                name: "SimilarityPercent",
                table: "ExamDetails");
        }
    }
}