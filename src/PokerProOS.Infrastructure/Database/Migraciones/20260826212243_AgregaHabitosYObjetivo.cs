using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokerProOS.Infrastructure.Database.Migraciones
{
    /// <inheritdoc />
    public partial class AgregaHabitosYObjetivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CumplimientoObjetivo",
                table: "EntradasDeDiario",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObjetivoTecnico",
                table: "EntradasDeDiario",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarcasDeHabito",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Valor = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarcasDeHabito", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarcasDeHabito_Fecha_Clave",
                table: "MarcasDeHabito",
                columns: new[] { "Fecha", "Clave" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarcasDeHabito");

            migrationBuilder.DropColumn(
                name: "CumplimientoObjetivo",
                table: "EntradasDeDiario");

            migrationBuilder.DropColumn(
                name: "ObjetivoTecnico",
                table: "EntradasDeDiario");
        }
    }
}
