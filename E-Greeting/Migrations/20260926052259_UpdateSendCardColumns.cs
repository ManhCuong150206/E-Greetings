using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Greeting.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSendCardColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Account",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    password = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    role = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "USER"),
                    status = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Account__3213E83F17BDAFD7", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Category",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Category__3213E83F0A0D957E", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Service",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    price = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Service__3213E83FA65AD915", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Feedback",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    account_id = table.Column<int>(type: "int", nullable: false),
                    content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Feedback__3213E83FB4B47255", x => x.id);
                    table.ForeignKey(
                        name: "FK_Feedback_Account",
                        column: x => x.account_id,
                        principalTable: "Account",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "Template",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    image_path = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    category_id = table.Column<int>(type: "int", nullable: false),
                    layout_type = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    config_data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_free = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Template__3213E83F8E333B4D", x => x.id);
                    table.ForeignKey(
                        name: "FK_Template_Category",
                        column: x => x.category_id,
                        principalTable: "Category",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "Subscription",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    account_id = table.Column<int>(type: "int", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    service_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Subscrip__3213E83FB4C4DF78", x => x.id);
                    table.ForeignKey(
                        name: "FK_Subscription_Account",
                        column: x => x.account_id,
                        principalTable: "Account",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_Subscription_Service",
                        column: x => x.service_id,
                        principalTable: "Service",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "Wishlist",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    account_id = table.Column<int>(type: "int", nullable: false),
                    template_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Wishlist__3213E83F162F0DC9", x => x.id);
                    table.ForeignKey(
                        name: "FK_Wishlist_Account",
                        column: x => x.account_id,
                        principalTable: "Account",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_Wishlist_Template",
                        column: x => x.template_id,
                        principalTable: "Template",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "Payment",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    subscription_id = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "UNPAID"),
                    payment_method = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Payment__3213E83F53F97D03", x => x.id);
                    table.ForeignKey(
                        name: "FK_Payment_Subscription",
                        column: x => x.subscription_id,
                        principalTable: "Subscription",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "SentCard",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    account_id = table.Column<int>(type: "int", nullable: true),
                    template_id = table.Column<int>(type: "int", nullable: true),
                    subscription_id = table.Column<int>(type: "int", nullable: true),
                    image_path = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    audio_path = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    scheduled_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    sent_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    SenderEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    recipient_email = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    subject = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CardHtmlContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AudioUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    open_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    recipient_reply = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SentCard__3213E83FD2F21826", x => x.id);
                    table.ForeignKey(
                        name: "FK_SentCard_Account",
                        column: x => x.account_id,
                        principalTable: "Account",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_SentCard_Subscription",
                        column: x => x.subscription_id,
                        principalTable: "Subscription",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_SentCard_Template",
                        column: x => x.template_id,
                        principalTable: "Template",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionRecipient",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    subscription_id = table.Column<int>(type: "int", nullable: false),
                    email = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Subscrip__3213E83F93C44F83", x => x.id);
                    table.ForeignKey(
                        name: "FK_SubRecipient_Subscription",
                        column: x => x.subscription_id,
                        principalTable: "Subscription",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "UQ__Account__AB6E61649F81B483",
                table: "Account",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Feedback_account_id",
                table: "Feedback",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_subscription_id",
                table: "Payment",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_SentCard_account_id",
                table: "SentCard",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_SentCard_subscription_id",
                table: "SentCard",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_SentCard_template_id",
                table: "SentCard",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_Subscription_account_id",
                table: "Subscription",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_Subscription_service_id",
                table: "Subscription",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRecipient_subscription_id",
                table: "SubscriptionRecipient",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_Template_category_id",
                table: "Template",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_Wishlist_template_id",
                table: "Wishlist",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "UQ_Wishlist_Account_Template",
                table: "Wishlist",
                columns: new[] { "account_id", "template_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Feedback");

            migrationBuilder.DropTable(
                name: "Payment");

            migrationBuilder.DropTable(
                name: "SentCard");

            migrationBuilder.DropTable(
                name: "SubscriptionRecipient");

            migrationBuilder.DropTable(
                name: "Wishlist");

            migrationBuilder.DropTable(
                name: "Subscription");

            migrationBuilder.DropTable(
                name: "Template");

            migrationBuilder.DropTable(
                name: "Account");

            migrationBuilder.DropTable(
                name: "Service");

            migrationBuilder.DropTable(
                name: "Category");
        }
    }
}
