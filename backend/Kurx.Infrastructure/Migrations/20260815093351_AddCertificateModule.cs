using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kurx.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "certificate_id_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Pattern = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NextSequence = table.Column<long>(type: "bigint", nullable: false),
                    Padding = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificate_id_rules", x => x.Id);
                    table.CheckConstraint("ck_certificate_id_rules_sequence", "\"NextSequence\" >= 1");
                    table.ForeignKey(
                        name: "FK_certificate_id_rules_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "certificate_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BackgroundStorageKey = table.Column<string>(type: "text", nullable: true),
                    BackgroundContentType = table.Column<string>(type: "text", nullable: true),
                    BackgroundWidthPx = table.Column<int>(type: "integer", nullable: true),
                    BackgroundHeightPx = table.Column<int>(type: "integer", nullable: true),
                    PageSize = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificate_templates", x => x.Id);
                    table.CheckConstraint("ck_certificate_templates_status", "\"Status\" IN ('Draft', 'Ready', 'Archived')");
                    table.ForeignKey(
                        name: "FK_certificate_templates_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_certificate_templates_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "certificate_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceFileStorageKey = table.Column<string>(type: "text", nullable: true),
                    SourceFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    ColumnMappingJson = table.Column<string>(type: "jsonb", nullable: true),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    PreviewCount = table.Column<int>(type: "integer", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificate_batches", x => x.Id);
                    table.CheckConstraint("ck_certificate_batches_status", "\"Status\" IN ('Draft', 'Mapping', 'PreviewReady', 'Approved', 'Generating', 'Completed', 'Failed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_certificate_batches_certificate_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "certificate_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_certificate_batches_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_certificate_batches_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "certificate_template_fields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StaticText = table.Column<string>(type: "text", nullable: true),
                    X = table.Column<double>(type: "double precision", nullable: false),
                    Y = table.Column<double>(type: "double precision", nullable: false),
                    Width = table.Column<double>(type: "double precision", nullable: false),
                    Height = table.Column<double>(type: "double precision", nullable: false),
                    Rotation = table.Column<double>(type: "double precision", nullable: false),
                    ZOrder = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsMasking = table.Column<bool>(type: "boolean", nullable: false),
                    BackgroundColor = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    FontFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FontSizePt = table.Column<double>(type: "double precision", nullable: true),
                    FontWeight = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    HorizontalAlignment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    VerticalAlignment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificate_template_fields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_certificate_template_fields_certificate_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "certificate_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "certificate_recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceRowNumber = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificate_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_certificate_recipients_certificate_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "certificate_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_certificate_recipients_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_certificate_recipients_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "issued_certificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldValuesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PdfStorageKey = table.Column<string>(type: "text", nullable: true),
                    PngStorageKey = table.Column<string>(type: "text", nullable: true),
                    DocumentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SignatureKeyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Signature = table.Column<string>(type: "text", nullable: true),
                    SupersedesCertificateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issued_certificates", x => x.Id);
                    table.CheckConstraint("ck_issued_certificates_status", "\"Status\" IN ('Issued', 'Revoked', 'Superseded')");
                    table.ForeignKey(
                        name: "FK_issued_certificates_certificate_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "certificate_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_issued_certificates_certificate_recipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "certificate_recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issued_certificates_certificate_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "certificate_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issued_certificates_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_issued_certificates_issued_certificates_SupersedesCertifica~",
                        column: x => x.SupersedesCertificateId,
                        principalTable: "issued_certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "certificate_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Destination = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificate_deliveries", x => x.Id);
                    table.CheckConstraint("ck_certificate_deliveries_status", "\"Status\" IN ('Pending', 'Sent', 'Failed', 'Bounced')");
                    table.ForeignKey(
                        name: "FK_certificate_deliveries_issued_certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "issued_certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "certificate_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificate_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_certificate_events_issued_certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "issued_certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "certificate_revocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReplacementCertificateId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificate_revocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_certificate_revocations_issued_certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "issued_certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_certificate_revocations_issued_certificates_ReplacementCert~",
                        column: x => x.ReplacementCertificateId,
                        principalTable: "issued_certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_certificate_revocations_users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_certificate_batches_CreatedByUserId",
                table: "certificate_batches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_batches_EventId_Status",
                table: "certificate_batches",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_certificate_batches_TemplateId",
                table: "certificate_batches",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_deliveries_CertificateId_Channel",
                table: "certificate_deliveries",
                columns: new[] { "CertificateId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_certificate_deliveries_Status",
                table: "certificate_deliveries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_events_CertificateId_Type",
                table: "certificate_events",
                columns: new[] { "CertificateId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_certificate_id_rules_EventId",
                table: "certificate_id_rules",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_certificate_recipients_BatchId",
                table: "certificate_recipients",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_recipients_EventId",
                table: "certificate_recipients",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_recipients_NormalizedEmail",
                table: "certificate_recipients",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_recipients_UserId",
                table: "certificate_recipients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_revocations_CertificateId",
                table: "certificate_revocations",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_revocations_ReplacementCertificateId",
                table: "certificate_revocations",
                column: "ReplacementCertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_revocations_RevokedByUserId",
                table: "certificate_revocations",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_template_fields_TemplateId_ZOrder",
                table: "certificate_template_fields",
                columns: new[] { "TemplateId", "ZOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_certificate_templates_EventId",
                table: "certificate_templates",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_templates_OwnerUserId",
                table: "certificate_templates",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_templates_OwnerUserId_EventId",
                table: "certificate_templates",
                columns: new[] { "OwnerUserId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_issued_certificates_BatchId",
                table: "issued_certificates",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_issued_certificates_CertificateId",
                table: "issued_certificates",
                column: "CertificateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_issued_certificates_EventId_Status",
                table: "issued_certificates",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_issued_certificates_RecipientId",
                table: "issued_certificates",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_issued_certificates_SupersedesCertificateId",
                table: "issued_certificates",
                column: "SupersedesCertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_issued_certificates_TemplateId",
                table: "issued_certificates",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "certificate_deliveries");

            migrationBuilder.DropTable(
                name: "certificate_events");

            migrationBuilder.DropTable(
                name: "certificate_id_rules");

            migrationBuilder.DropTable(
                name: "certificate_revocations");

            migrationBuilder.DropTable(
                name: "certificate_template_fields");

            migrationBuilder.DropTable(
                name: "issued_certificates");

            migrationBuilder.DropTable(
                name: "certificate_recipients");

            migrationBuilder.DropTable(
                name: "certificate_batches");

            migrationBuilder.DropTable(
                name: "certificate_templates");
        }
    }
}
