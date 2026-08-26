using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokerProOS.Infrastructure.Database.Migraciones
{
    /// <inheritdoc />
    public partial class AgregaDiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntradasDeDiario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Intencion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NivelDeJuego = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    Disparador = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Mesas = table.Column<int>(type: "int", nullable: true),
                    Minutos = table.Column<int>(type: "int", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreadaEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadaEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntradasDeDiario", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntradasDeDiario_Fecha",
                table: "EntradasDeDiario",
                column: "Fecha",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntradasDeDiario");
        }
    }
}
