using M5Database;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace M5Service_Zorlu
{
    public class Import
    {
        public async Task Main(CancellationToken stoppingToken)
        {
            await new Import().Izin();
            await Task.Delay(1000, stoppingToken);
            return;
        }


        public string GetToken()
        {
            RestClient restClient = new RestClient("https://sso.zorlu.com.tr/DynamicHRService/token");
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "password");
            request.AddParameter("username", "6A58A229E63F4BF792762B41F32E5C2E");
            request.AddParameter("password", "64AEA240EB5374C3DC4E3E9DB5B1229619555FDC");
            var result = JsonConvert.DeserializeObject<ResponseForToken>(restClient.Execute(request).Content);
            Console.WriteLine(result.access_token);
            return result.access_token;
        }


        public ResultForLeave GetLeave(long index)
        {
            RestClient restClient = new RestClient("https://sso.zorlu.com.tr/DynamicHRService/DynamicQuery");
            var request = new RestRequest(Method.POST);
            request.AddHeader("Authorization", "Bearer " + GetToken());
            request.AddHeader("Content-Type", "application/json; charset=utf-8");
            request.AddJsonBody(new { queryName = "MeyerDeltaLeaveSF", parameters = new { start = index, take = 100 } });
            var result = restClient.Execute(request);
            ResultForLeave resultForPerson = JsonConvert.DeserializeObject<ResultForLeave>(result.Content);
            return resultForPerson;
        }
        public async Task Izin()
        {
            bool test = true;

            while (test)
            {
                var BaslangicIndex = Data.SqlFill("select BaslangicIndex from IzinEntegrasyonIndex");
                long a = Convert.ToInt64(BaslangicIndex[0].BaslangicIndex);
                var leaves = GetLeave(a);
                if (leaves.results.Length < 100)
                {
                    test = false;
                }
                foreach (var leave in leaves.results)
                {
                    

                    M5Database.IzinEnt.MeyerIzin meyerIzin = new IzinEnt.MeyerIzin();
                    meyerIzin.formid = leave.pk;
                    meyerIzin.aciklama = leave.leaveDescription;
                    if (string.IsNullOrWhiteSpace(leave.startTime))
                    {

                        meyerIzin.baslangic = leave.startDate + " 00:00:00.000";
                        meyerIzin.bitis = DateTime.Parse(leave.endDate).AddDays(1).ToString("yyyy-MM-dd 00:00:00.000");
                    }
                    else
                    {

                        meyerIzin.baslangic = leave.startDate + " " + leave.startTime + ".000";
                        meyerIzin.bitis = DateTime.Parse(leave.endDate + " " + leave.endTime).ToString("yyyy-MM-dd HH:mm:ss.000");
                    }
                    meyerIzin.islemtarihi = DateTime.Now;
                    if (leave.status == "D")
                    {
                        meyerIzin.islemtipi = "d";
                    }
                    else
                    {
                        meyerIzin.islemtipi = "i";
                    }
                    meyerIzin.izinkodu = Convert.ToInt32(leave.leaveType).ToString();
                    meyerIzin.sicilno = leave.companyCode + leave.employeeNo;//COMPANY CODE EKLE
                    var exs = Data.SqlFill("select * from Izintalepleri inner join sicil on Izintalepleri.SicilId= sicil.Id and sicil.okod5+sicil.sicilno='" + meyerIzin.sicilno +
                        "' and dbo.KesitHesapla(Baslangic,Bitis, '" + DateTime.Parse(meyerIzin.baslangic).ToString("yyyy-MM-dd HH:mm:ss.fff") + "' ,'"
                        + DateTime.Parse(meyerIzin.bitis).ToString("yyyy-MM-dd HH:mm:ss.fff") + "','NM',0)>0 and durum>=0");
                    if (!exs.Any())
                    {
                        meyerIzin.SaveWithOnlyActiveRecords();
                    }

                }
                if (leaves.results.Length != 0)
                {
                    Console.WriteLine("GİRDİ1");
                    Data.SqlFill("update IzinEntegrasyonIndex set BaslangicIndex='" + (leaves.results.Last().id + 1) + "'");
                }
            }
        }
    }
}
//}
//CREATE TABLE[dbo].[IzinEntegrasyonIndex] (

//    [BaslangicIndex][nvarchar](max) NULL
//) ON[PRIMARY] TEXTIMAGE_ON[PRIMARY]
//GO