using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokerProOS.Infrastructure.Database.Migraciones
{
    /// <inheritdoc />
    public partial class AgregaBitacoraDeRespuestas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RespuestasRegistradas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Situacion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ClaveDeStack = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Spot = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Mano = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    AccionElegida = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AccionCorrecta = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Acerto = table.Column<bool>(type: "bit", nullable: false),
                    Milisegundos = table.Column<int>(type: "int", nullable: false),
                    RespondidaEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespuestasRegistradas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasRegistradas_UsuarioId_RespondidaEn",
                table: "RespuestasRegistradas",
                columns: new[] { "UsuarioId", "RespondidaEn" });

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasRegistradas_UsuarioId_Situacion_ClaveDeStack_Spot_Mano",
                table: "RespuestasRegistradas",
                columns: new[] { "UsuarioId", "Situacion", "ClaveDeStack", "Spot", "Mano" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RespuestasRegistradas");
        }
    }
}
