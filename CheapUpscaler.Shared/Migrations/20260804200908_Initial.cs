using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheapUpscaler.Shared.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SourceVideoPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    OutputPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    UpscaleType = table.Column<int>(type: "INTEGER", nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ProgressPercentage = table.Column<double>(type: "REAL", nullable: false),
                    CurrentFrame = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalFrames = table.Column<int>(type: "INTEGER", nullable: true),
                    EstimatedTimeRemainingTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ErrorStackTrace = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxRetries = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: true),
                    MachineName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SourceWidth = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceHeight = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceFps = table.Column<double>(type: "REAL", nullable: true),
                    SourceDurationTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    OutputWidth = table.Column<int>(type: "INTEGER", nullable: true),
                    OutputHeight = table.Column<int>(type: "INTEGER", nullable: true),
                    OutputFps = table.Column<double>(type: "REAL", nullable: true),
                    OutputFileSizeBytes = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_CreatedAt",
                table: "Jobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_JobId",
                table: "Jobs",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status",
                table: "Jobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Jobs");
        }
    }
}
