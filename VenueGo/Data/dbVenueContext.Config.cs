using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace VenueGo.Data;

public partial class dbVenueContext
{
    // ==========================================
    // 【新增】
    // 無參數建構子
    // 讓我們可以使用：
    // dbVenueContext db = new dbVenueContext();
    // ==========================================
    public dbVenueContext()
    {
    }


    // ==========================================
    // 當使用 new dbVenueContext() 時，
    // 自己讀取 appsettings.json
    // 與 appsettings.Local.json
    // ==========================================
    protected override void OnConfiguring(
        DbContextOptionsBuilder optionsBuilder)
    {
        // 如果 Program.cs 已經設定過資料庫，
        // 就不要再重複設定。
        if (!optionsBuilder.IsConfigured)
        {
            IConfigurationRoot configuration =
                new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())

                    // 團隊共用設定
                    .AddJsonFile(
                        "appsettings.json",
                        optional: false,
                        reloadOnChange: true)

                    // 每個人自己電腦的設定
                    .AddJsonFile(
                        "appsettings.Local.json",
                        optional: true,
                        reloadOnChange: true)

                    .Build();


            string? connectionString =
                configuration.GetConnectionString(
                    "DefaultConnection"
                );


            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "找不到 ConnectionStrings:DefaultConnection，" +
                    "請確認 appsettings.Local.json 是否存在並設定正確。"
                );
            }


            optionsBuilder.UseSqlServer(connectionString);
        }
    }
}