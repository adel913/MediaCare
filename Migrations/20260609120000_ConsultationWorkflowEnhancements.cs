using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalApp.API.Migrations
{
    /// <inheritdoc />
    public partial class ConsultationWorkflowEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ConsultationFee",
                table: "Appointments",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PaymentStatus",
                table: "Appointments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FamilyMemberId",
                table: "MedicalRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "MedicalRecords",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecords_FamilyMemberId",
                table: "MedicalRecords",
                column: "FamilyMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_AppointmentDate_StartTime",
                table: "Appointments",
                columns: new[] { "DoctorId", "AppointmentDate", "StartTime" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] <> 4");

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalRecords_FamilyMembers_FamilyMemberId",
                table: "MedicalRecords",
                column: "FamilyMemberId",
                principalTable: "FamilyMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(@"
                UPDATE a
                SET a.ConsultationFee = COALESCE(dc.ConsultationFee, d.ConsultationFee, 0)
                FROM Appointments a
                INNER JOIN Doctors d ON a.DoctorId = d.Id
                OUTER APPLY (
                    SELECT TOP 1 dc.ConsultationFee
                    FROM DoctorClinics dc
                    WHERE dc.DoctorId = a.DoctorId AND dc.IsActive = 1 AND dc.IsDeleted = 0
                    ORDER BY dc.Id
                ) dc
                WHERE a.ConsultationFee = 0;
            ");

            migrationBuilder.Sql(@"
                UPDATE Appointments
                SET PaymentStatus = CASE
                    WHEN IsPaid = 1 THEN 1
                    WHEN Status = 4 AND RefundStatus IN (1, 2) THEN 2
                    ELSE 0
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MedicalRecords_FamilyMembers_FamilyMemberId",
                table: "MedicalRecords");

            migrationBuilder.DropIndex(
                name: "IX_MedicalRecords_FamilyMemberId",
                table: "MedicalRecords");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_AppointmentDate_StartTime",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ConsultationFee",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "FamilyMemberId",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "MedicalRecords");
        }
    }
}
