namespace VenueGo.Dtos.ContentDtos
{
    //常見問題回答的一個片段 >> FaqAnswerParser 把回答切成「文字」與「連結」片段
    //前台 API、後台預覽共用:前台依 Type 畫出文字或連結,不用自己解析、不用 v-html
    public class FaqAnswerSegmentDto
    {
        //"text" = 一般文字(可能含換行 \n);"link" = 連結
        public string Type { get; set; } = string.Empty;

        //文字內容;連結時是連結上顯示的文字
        public string Text { get; set; } = string.Empty;

        //連結網址;文字片段是 null
        public string? Url { get; set; }

        //true = 站外連結(https:// 開頭,前台開新分頁);false = 站內連結(/ 開頭,前台同頁切換)
        public bool IsExternal { get; set; }
    }
}
