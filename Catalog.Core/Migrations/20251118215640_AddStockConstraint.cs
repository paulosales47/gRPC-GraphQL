using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddStockConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "Stock",
                table: "Products",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql(@"ALTER TABLE Products ADD CONSTRAINT CK_Products_Stock_NonNegative CHECK (Stock >= 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Stock",
                table: "Products",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.Sql(@"ALTER TABLE Products DROP CONSTRAINT CK_Products_Stock_NonNegative;");
        }
    }
}
