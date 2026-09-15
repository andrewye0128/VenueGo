using VenueGo.Data;
using VenueGo.Models.Entities;



namespace VenueGo.Models.VenueModels
{
    public class CSportTypePriceRuleFactory
    {

        //價格規則查詢
        public List<CSportTypePriceRuleWrap> QueryAll()
        {
            List<CSportTypePriceRuleWrap> list = new List<CSportTypePriceRuleWrap>();
            dbVenueContext db= new dbVenueContext();
            var datas = from p in db.SportTypePriceRules select p;

            //對IQueryable集合元素逐項放入list
            foreach (var item in datas)
            {
                CSportTypePriceRuleWrap x = new CSportTypePriceRuleWrap();
                x.sportTypePriceRule = item;
                list.Add(x);
            }


            return list;
        }


        //價格規則新增


        //價格規則修改



        //價格規則刪除
    }
}
