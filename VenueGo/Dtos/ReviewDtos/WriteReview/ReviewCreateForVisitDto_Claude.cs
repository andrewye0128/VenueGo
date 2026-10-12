namespace VenueGo.Dtos.ReviewDtos.WriteReview
{

    /// <summary>送出現場評論。</summary>
    public sealed class ReviewCreateForVisitDto : ReviewCreateInputDto
    {
        /// <summary>要不要匿名。現場評論限會員之後（10/9），一律由會員自己選。</summary>
        public bool IsAnonymous { get; set; }

        /// <summary>預設公開。館方不作為的預設結果是公開，不是壓下來。</summary>
        public bool IsPublic { get; set; } = true;
    }
}
