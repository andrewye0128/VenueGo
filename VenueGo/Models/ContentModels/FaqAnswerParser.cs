using System.Text.RegularExpressions;
using VenueGo.Dtos.ContentDtos;

namespace VenueGo.Models.ContentModels
{
    //解析結果 >> 片段 + 錯誤(擋存檔) + 提醒(不擋存檔)
    public class FaqAnswerParseResult
    {
        public List<FaqAnswerSegmentDto> Segments { get; set; } = new List<FaqAnswerSegmentDto>();

        //網址不合法的連結,存檔時要擋下
        public List<string> Errors { get; set; } = new List<string>();

        //疑似用了全形括號的連結,只提醒,不擋存檔(一般文字裡的全形括號很常見)
        public List<string> Warnings { get; set; } = new List<string>();
    }

    //常見問題回答的連結解析 >> 連結寫法 [連結文字](網址),網址只接受 / 或 https:// 開頭
    //前台 API 與後台預覽共用這一份規則
    public static class FaqAnswerParser
    {
        //半形連結格式:[文字](網址)
        //文字不能含方括號與換行;網址不能含括號與空白
        private static readonly Regex LinkPattern = new Regex(@"\[([^\[\]\r\n]+)\]\(([^()\s]*)\)");

        //半形連結的開頭 [文字]( >> 用來找出「開頭像連結、但整段不符合格式」的寫法
        //例:[說明](/booking 少了右括號、[點我](javascript:alert(1)) 網址裡有括號
        private static readonly Regex LinkStartPattern = new Regex(@"\[([^\[\]\r\n]+)\]\(");

        //疑似連結但用了全形括號:左方括號、右方括號、左圓括號至少有一個是全形
        //例:［說明］（/booking）、[說明]（/booking）、【說明】(/booking)
        private static readonly Regex FullWidthLinkPattern = new Regex(@"[\[［【]([^\[\]［］【】\r\n]+)[\]］】]\s*[(（]");

        public static FaqAnswerParseResult Parse(string? answer)
        {
            FaqAnswerParseResult result = new FaqAnswerParseResult();
            if (string.IsNullOrEmpty(answer))
            {
                return result;
            }

            //換行統一成 \n(瀏覽器表單送來的是 \r\n),前台用 white-space: pre-line 顯示
            string text = answer.Replace("\r\n", "\n");

            //1. 依序找出半形連結,連結前面的部分是文字片段
            int position = 0;
            HashSet<int> linkStarts = new HashSet<int>();
            foreach (Match match in LinkPattern.Matches(text))
            {
                linkStarts.Add(match.Index);

                string linkText = match.Groups[1].Value;
                string url = match.Groups[2].Value;

                AddText(result, text.Substring(position, match.Index - position));

                if (IsValidUrl(url))
                {
                    result.Segments.Add(new FaqAnswerSegmentDto
                    {
                        Type = "link",
                        Text = linkText,
                        Url = url,
                        IsExternal = url.StartsWith("https://"),
                    });
                }
                else
                {
                    //網址不合法:記錄錯誤,這段照原樣當文字顯示(不能變成可以點的連結)
                    result.Errors.Add($"連結「{linkText}」的網址「{url}」不正確,網址要用 / 或 https:// 開頭");
                    AddText(result, match.Value);
                }

                position = match.Index + match.Length;
            }

            //2. 最後一個連結後面剩下的文字
            AddText(result, text.Substring(position));

            //3. 開頭是 [文字]( 但整段不符合連結格式(少了右括號、網址裡有括號或空白) >> 當成文字顯示,並記錄錯誤
            foreach (Match match in LinkStartPattern.Matches(text))
            {
                if (!linkStarts.Contains(match.Index))
                {
                    result.Errors.Add($"連結「{match.Groups[1].Value}」的格式不完整,請確認有右括號 ),而且網址裡沒有括號或空白");
                }
            }

            //4. 找出疑似全形括號的連結(全部是半形的已經在上面處理,這裡排除)
            foreach (Match match in FullWidthLinkPattern.Matches(text))
            {
                bool isAllHalfWidth = match.Value.StartsWith("[") && match.Value.Contains("](");
                if (!isAllHalfWidth)
                {
                    result.Warnings.Add($"「{match.Value.TrimEnd('(', '（')}」看起來像連結,但用了全形括號;要做成連結請改成半形 [文字](網址)");
                }
            }

            return result;
        }

        //網址只接受站內 / 開頭(但不能是 // 開頭,那代表其他網站)或站外 https:// 開頭
        private static bool IsValidUrl(string url)
        {
            if (url.StartsWith("/") && !url.StartsWith("//"))
            {
                return true;
            }

            return url.StartsWith("https://") && url.Length > "https://".Length;
        }

        //加入文字片段;空字串不加
        //前一個片段也是文字時(例如網址不合法的連結被當成文字)就接在後面,不要拆成好幾段
        private static void AddText(FaqAnswerParseResult result, string text)
        {
            if (text.Length == 0)
            {
                return;
            }

            FaqAnswerSegmentDto? last = result.Segments.LastOrDefault();
            if (last != null && last.Type == "text")
            {
                last.Text += text;
                return;
            }

            result.Segments.Add(new FaqAnswerSegmentDto { Type = "text", Text = text });
        }
    }
}
