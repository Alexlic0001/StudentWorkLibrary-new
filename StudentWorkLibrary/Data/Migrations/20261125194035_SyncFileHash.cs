using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentWorkLibrary.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncFileHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "StudentWorks",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "StudentWorks");
        }
    }
}
