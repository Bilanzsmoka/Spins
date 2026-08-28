using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokerProOS.Infrastructure.Database.Migraciones
{
    /// <inheritdoc />
    public partial class AgregaProgresoDeEntrenamiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProgresosDeCasilla",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Situacion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClaveDeStack = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Spot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Mano = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AciertosSeguidos = table.Column<int>(type: "int", nullable: false),
                    IntervaloEnDias = table.Column<int>(type: "int", nullable: false),
                    Vence = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualizadaEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgresosDeCasilla", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgresosDeCasilla_UsuarioId_Situacion_ClaveDeStack_Spot_Mano",
                table: "ProgresosDeCasilla",
                columns: new[] { "UsuarioId", "Situacion", "ClaveDeStack", "Spot", "Mano" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgresosDeCasilla_UsuarioId_Vence",
                table: "ProgresosDeCasilla",
                columns: new[] { "UsuarioId", "Vence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgresosDeCasilla");
        }
    }
}
