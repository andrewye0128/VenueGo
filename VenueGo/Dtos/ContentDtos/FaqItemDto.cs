namespace VenueGo.Dtos.ContentDtos
{
    //前台常見問題的一則問題 >> 供 GET /api/faqs 使用
    //只回傳切好的回答片段,不回傳原始 answer 文字(前台不用自己解析、不用 v-html)
    public class FaqItemDto
    {
        public int Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public List<FaqAnswerSegmentDto> AnswerSegments { get; set; } = new List<FaqAnswerSegmentDto>();
    }
}
