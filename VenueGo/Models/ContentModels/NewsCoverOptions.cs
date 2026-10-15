using System.Text.Json;

namespace VenueGo.Models.ContentModels
{
    //公版底稿的字級選項:代碼(存進樣式 JSON)、按鈕文字、畫布上的像素
    public record NewsCoverSize(string Code, string Name, int Pixels);

    //公版底稿內文的一個樣式片段 >> 第 Start 個字到第 End 個字(不含)套用 Color 與 Size
    //位置的算法跟 Fabric.js 一樣:換行不算一個字
    public class NewsCoverStyleSegment
    {
        public int Start { get; set; }
        public int End { get; set; }
        public string Color { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
    }

    //公版底稿的選項清單與樣式驗證
    //提供編輯器 View(按鈕)與 ContentController(存檔前驗證),清單只維護這一份
    public static class NewsCoverOptions
    {
        //內文上限,跟資料庫 News.CoverText nvarchar(200) 一致
        public const int MaxTextLength = 200;

        //標題的樣式是程式固定的(白色粗體),不開放編輯 >> 存空陣列
        public const string TitleStyles = "[]";

        //字級清單,第二個「中」是內文預設字級
        public static readonly List<NewsCoverSize> Sizes = new List<NewsCoverSize>
        {
            new NewsCoverSize("sm", "小", 40),
            new NewsCoverSize("md", "中", 48),
            new NewsCoverSize("lg", "大", 56),
        };
        public const string DefaultSize = "md";

        //顏色清單:Key = 色碼(存進樣式 JSON),Value = 顏色名稱
        //色碼取自前台 main.css 色票,不新增顏色;第一個是內文預設色
        public static readonly List<KeyValuePair<string, string>> Colors = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("#1a2230", "深灰"),
            new KeyValuePair<string, string>("#1d5faf", "品牌藍"),
            new KeyValuePair<string, string>("#b42318", "強調紅"),
            new KeyValuePair<string, string>("#c24e17", "強調橘"),
        };
        public const string DefaultColor = "#1a2230";

        //存成 JSON 時屬性名稱用小寫開頭,例:[{"start":0,"end":3,"color":"#1a2230","size":"lg"}]
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        //驗證內文的樣式 JSON
        //通過 >> 回傳 null,normalizedJson 是整理過的 JSON(存進資料庫用)
        //不通過 >> 回傳錯誤訊息
        public static string? ValidateStyles(string text, string? stylesJson, out string normalizedJson)
        {
            normalizedJson = "[]";

            //1. 沒有任何樣式 >> 全部用預設字級與顏色
            if (string.IsNullOrWhiteSpace(stylesJson))
            {
                return null;
            }

            //2. 轉成片段清單;格式不對(不是 JSON 陣列、欄位型別錯)就擋下
            List<NewsCoverStyleSegment>? segments;
            try
            {
                segments = JsonSerializer.Deserialize<List<NewsCoverStyleSegment>>(stylesJson, JsonOptions);
            }
            catch (JsonException)
            {
                return "封面內文的樣式資料格式不正確,請重新設定樣式";
            }
            if (segments == null)
            {
                return "封面內文的樣式資料格式不正確,請重新設定樣式";
            }

            //3. 算出內文有幾個字(換行不算,跟 Fabric.js 一樣;用 Rune 計算,表情符號這類兩個字元組成的字也只算一個)
            int charCount = text.Replace("\n", "").EnumerateRunes().Count();

            //4. 逐段檢查:依順序排列、不重疊、不超出內文範圍,顏色與字級在清單內
            int previousEnd = 0;
            foreach (NewsCoverStyleSegment segment in segments)
            {
                if (segment.Start < previousEnd || segment.End <= segment.Start || segment.End > charCount)
                {
                    return "封面內文的樣式範圍不正確,請重新設定樣式";
                }
                if (!Colors.Any(c => c.Key == segment.Color))
                {
                    return "封面內文使用了清單以外的顏色";
                }
                if (!Sizes.Any(s => s.Code == segment.Size))
                {
                    return "封面內文使用了清單以外的字級";
                }
                previousEnd = segment.End;
            }

            //5. 重新輸出 >> 只留下這 4 個欄位,表單多送的其他欄位不會被存進資料庫
            normalizedJson = JsonSerializer.Serialize(segments, JsonOptions);
            return null;
        }
    }
}
