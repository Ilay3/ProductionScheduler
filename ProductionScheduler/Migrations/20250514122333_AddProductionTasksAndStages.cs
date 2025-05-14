using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductionScheduler.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionTasksAndStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Details",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Details", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MachineTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DetailId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlannedStartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PlannedEndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualStartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualEndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionTasks_Details_DetailId",
                        column: x => x.DetailId,
                        principalTable: "Details",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Machines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MachineTypeId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Machines_MachineTypes_MachineTypeId",
                        column: x => x.MachineTypeId,
                        principalTable: "MachineTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RouteStages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DetailId = table.Column<int>(type: "INTEGER", nullable: false),
                    OperationNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OperationName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MachineTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    StandardTimePerUnit = table.Column<double>(type: "REAL", nullable: false),
                    OrderInRoute = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RouteStages_Details_DetailId",
                        column: x => x.DetailId,
                        principalTable: "Details",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RouteStages_MachineTypes_MachineTypeId",
                        column: x => x.MachineTypeId,
                        principalTable: "MachineTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionTaskStages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductionTaskId = table.Column<int>(type: "INTEGER", nullable: false),
                    RouteStageId = table.Column<int>(type: "INTEGER", nullable: false),
                    MachineId = table.Column<int>(type: "INTEGER", nullable: true),
                    QuantityToProcess = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderInTask = table.Column<int>(type: "INTEGER", nullable: false),
                    PlannedStartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PlannedEndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PlannedDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    ActualStartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualEndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualDuration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StandardTimePerUnitAtExecution = table.Column<double>(type: "REAL", nullable: false),
                    PlannedSetupTime = table.Column<double>(type: "REAL", nullable: false),
                    ParentProductionTaskStageId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionTaskStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionTaskStages_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionTaskStages_ProductionTaskStages_ParentProductionTaskStageId",
                        column: x => x.ParentProductionTaskStageId,
                        principalTable: "ProductionTaskStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionTaskStages_ProductionTasks_ProductionTaskId",
                        column: x => x.ProductionTaskId,
                        principalTable: "ProductionTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionTaskStages_RouteStages_RouteStageId",
                        column: x => x.RouteStageId,
                        principalTable: "RouteStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Details_Code",
                table: "Details",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Machines_MachineTypeId",
                table: "Machines",
                column: "MachineTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Machines_Name",
                table: "Machines",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachineTypes_Name",
                table: "MachineTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionTasks_DetailId",
                table: "ProductionTasks",
                column: "DetailId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionTaskStages_MachineId",
                table: "ProductionTaskStages",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionTaskStages_ParentProductionTaskStageId",
                table: "ProductionTaskStages",
                column: "ParentProductionTaskStageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionTaskStages_ProductionTaskId",
                table: "ProductionTaskStages",
                column: "ProductionTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionTaskStages_RouteStageId",
                table: "ProductionTaskStages",
                column: "RouteStageId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStages_DetailId",
                table: "RouteStages",
                column: "DetailId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStages_MachineTypeId",
                table: "RouteStages",
                column: "MachineTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionTaskStages");

            migrationBuilder.DropTable(
                name: "Machines");

            migrationBuilder.DropTable(
                name: "ProductionTasks");

            migrationBuilder.DropTable(
                name: "RouteStages");

            migrationBuilder.DropTable(
                name: "Details");

            migrationBuilder.DropTable(
                name: "MachineTypes");
        }
    }
}
