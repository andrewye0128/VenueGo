namespace VenueGo.Dtos.VenueDtos
{
    //給 預約功能使用 >> 運動類型下拉選單的運動類型名稱與icon
    //屬性名稱會轉成 JSON 的 id、name、icon(跟前台約定的格式)
    public class SportTypeOptionDto
    {
        //運動類型 Id
        public int Id { get; set; }

        //運動類型名稱,例如"羽球"
        public string Name { get; set; } = "";//一定要有值,故預設給空字串避免null

        //完整 Iconify 名稱,例如"i-mdi-badminton";沒設定時 Factory 會給預設圖示,所以不會是 null
        public string Icon { get; set; } = "";
    }
}
