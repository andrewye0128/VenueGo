namespace VenueGo.Dtos.VenueDtos
{
    //給 SportTypeIntroPageDto 使用 >> 一種運動類型的介紹
    public class SportTypeIntroDto
    {
        //運動類型 Id,前台 v-for 的 :key 使用
        public int SportTypeId { get; set; }

        //運動類型名稱,例如「羽球」
        public string SportName { get; set; } = string.Empty;

        //代表照片的網址路徑(例如 /images/sporttypes/xxx.jpg),可以直接放進 <img src>;沒有上傳照片時是 null
        public string? PhotoPath { get; set; }

        //注意事項,保留後台輸入的換行(\n);沒有填寫時是 null
        public string? Notice { get; set; }

        //收費標準;null = 沒有價格規則或價格規則停用,前台顯示「請洽櫃台詢問」
        public SportTypePriceDto? Price { get; set; }
    }
}
