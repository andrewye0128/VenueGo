namespace VenueGo.Services
{
    // 開發用假會員
    public class FakeCurrentUser : ICurrentUser
    {
        public int? MemberId => 9001;
        public int? EmployeeId => 9002;
    }

    // 未登入訪客
    public class FakeGuestUser : ICurrentUser
    {
        public int? MemberId => null;
        public int? EmployeeId => null;
    }
}
