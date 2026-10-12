using System.Reflection.Metadata.Ecma335;


namespace VenueGo.Models.VenueModels
{
    public static class SportTypeIconOptions
    {
        //用於存放運動類型的icon代號清單
        //1. 預設的icon圖示
        public const string DefaultIcon = "i-mdi-help-circle-outline";
        //2. 可選的icon圖示清單 >> 集合存取
        public static readonly List<KeyValuePair<string, string>> All = new List<KeyValuePair<string, string>>
        {
        //對List加入icon代號與對應的顯示名稱(大括號為List.Add的語法糖)
        //順序就是後台圖示格子的排列順序;新增圖示只要在這裡加一行
        //球類
        new KeyValuePair<string,string>("i-mdi-basketball", "籃球"),
        new KeyValuePair<string,string>("i-mdi-badminton", "羽球"),
        new KeyValuePair<string,string>("i-mdi-tennis", "網球"),
        new KeyValuePair<string,string>("i-mdi-table-tennis", "桌球"),
        new KeyValuePair<string,string>("i-mdi-racquetball", "匹克球"),
        new KeyValuePair<string,string>("i-mdi-volleyball", "排球"),
        new KeyValuePair<string,string>("i-mdi-soccer", "足球"),
        new KeyValuePair<string,string>("i-mdi-baseball", "棒壘球"),
        new KeyValuePair<string,string>("i-mdi-football", "美式足球"),
        new KeyValuePair<string,string>("i-mdi-rugby", "橄欖球"),
        new KeyValuePair<string,string>("i-mdi-handball", "手球"),
        new KeyValuePair<string,string>("i-mdi-cricket", "板球"),
        new KeyValuePair<string,string>("i-mdi-hockey-sticks", "曲棍球"),
        new KeyValuePair<string,string>("i-mdi-golf", "高爾夫"),
        new KeyValuePair<string,string>("i-mdi-bowling", "保齡球"),
        new KeyValuePair<string,string>("i-mdi-billiards", "撞球"),
        //水上
        new KeyValuePair<string,string>("i-mdi-swim", "游泳"),
        new KeyValuePair<string,string>("i-mdi-diving", "跳水"),
        //健身、武術
        new KeyValuePair<string,string>("i-mdi-dumbbell", "健身"),
        new KeyValuePair<string,string>("i-mdi-weight-lifter", "舉重"),
        new KeyValuePair<string,string>("i-mdi-yoga", "瑜珈"),
        new KeyValuePair<string,string>("i-mdi-gymnastics", "體操"),
        new KeyValuePair<string,string>("i-mdi-karate", "空手道"),
        new KeyValuePair<string,string>("i-mdi-boxing-glove", "拳擊"),
        new KeyValuePair<string,string>("i-mdi-fencing", "擊劍"),
        //其他
        new KeyValuePair<string,string>("i-mdi-run", "跑步"),
        new KeyValuePair<string,string>("i-mdi-bike", "自行車"),
        new KeyValuePair<string,string>("i-mdi-skateboarding", "滑板"),
        new KeyValuePair<string,string>("i-mdi-ski", "滑雪"),
        new KeyValuePair<string,string>("i-mdi-dance-ballroom", "舞蹈"),
        };

        //3.驗證傳入
        public static bool IsValid(string? iconName)
        {
            //驗證傳入的iconName是否存在於All清單內
            return All.Any(x => x.Key == iconName);
        }

        //4. 修正icon標籤名稱為 @mdi 可以用的 class 格式
        public static string ToFontClass(string iconName)
        {
            //去除 icon 標籤名稱的 "i-"字元(剛好是前兩個)
            return "mdi " + iconName.Substring(2);
        }



    }
}
