using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokerProOS.Infrastructure.Database.Migraciones
{
    /// <inheritdoc />
    public partial class InicialConBitacora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartStrategyCells",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SituationKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SituationLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StackKey = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MinBB = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxBB = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SpotKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SpotLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HandLabel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartStrategyCells", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsultasDeVoz",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Situacion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClaveDeStack = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Spot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Mano = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Resuelta = table.Column<bool>(type: "bit", nullable: false),
                    TextoCrudo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreadaEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultasDeVoz", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpinSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: true),
                    Stake = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BuyIn = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Tournaments = table.Column<int>(type: "int", nullable: false),
                    FreeTournaments = table.Column<int>(type: "int", nullable: false),
                    PrizeTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetResult = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Rakeback = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PromoValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChipEvTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Minutes = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlayedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpinSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpinTournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Site = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TournamentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BuyIn = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HeroRank = table.Column<int>(type: "int", nullable: true),
                    Hands = table.Column<int>(type: "int", nullable: false),
                    HeroAllins = table.Column<int>(type: "int", nullable: false),
                    HeroCallsAllin = table.Column<int>(type: "int", nullable: false),
                    HeroRaises = table.Column<int>(type: "int", nullable: false),
                    HeroLimps = table.Column<int>(type: "int", nullable: false),
                    HeroPreflopFolds = table.Column<int>(type: "int", nullable: false),
                    FirstPlayedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPlayedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RawText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpinTournaments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainerAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Pack = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Spot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StackBB = table.Column<int>(type: "int", nullable: false),
                    Villain = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HandLabel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ExpectedAction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChosenAction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Adjustment = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainerAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartStrategyCells_SituationKey_StackKey_SpotKey_HandLabel",
                table: "ChartStrategyCells",
                columns: new[] { "SituationKey", "StackKey", "SpotKey", "HandLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasDeVoz_Situacion_ClaveDeStack_Spot_Mano",
                table: "ConsultasDeVoz",
                columns: new[] { "Situacion", "ClaveDeStack", "Spot", "Mano" });

            migrationBuilder.CreateIndex(
                name: "IX_SpinTournaments_TournamentId",
                table: "SpinTournaments",
                column: "TournamentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartStrategyCells");

            migrationBuilder.DropTable(
                name: "ConsultasDeVoz");

            migrationBuilder.DropTable(
                name: "SpinSessions");

            migrationBuilder.DropTable(
                name: "SpinTournaments");

            migrationBuilder.DropTable(
                name: "TrainerAttempts");
        }
    }
}
