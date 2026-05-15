using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNUFLearning.Migrations
{
    public partial class AddScoreToExamQuestion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Score",
                table: "ExamQuestions",
                type: "float",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Score",
                table: "ExamQuestions");
        }
    }
}
