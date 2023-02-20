using M5Database;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

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
            return result.access_token;
        }

        class x
        {
            public string CompanyCode { get; set; }
            public string EmployeeNo { get; set; }
            public string StartDate { get; set; }
            public string EndDate { get; set; }
            public string SequenceNo { get; set; }
            public string LeaveType { get; set; }
            public string LeaveDescription { get; set; }
            public int DurationHour { get; set; }
            public int DurationDay { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public string CreateDate { get; set; }
            public string Operation { get; set; }
            public string IsPlanned { get; set; }
        }
        List<x> liste = new List<x>();
        public void SetLeave(string CompanyCode, string EmployeeNo, string StartDate, string EndDate,
            string LeaveType, string LeaveDescription, string StartTime, string EndTime, string CreatedDate,
            string Operation, string IsPlanned, int flag)
        {
            
            if (flag == 1)
            {
                x izin = new x();
                izin.CompanyCode = CompanyCode;
                izin.EmployeeNo = EmployeeNo;
                izin.StartDate = StartDate;
                    izin.EndDate = EndDate;
                izin.SequenceNo = "000";
                    izin.LeaveType = LeaveType;
                izin.LeaveDescription = LeaveDescription;
                izin.DurationHour = 0;
                izin.DurationDay = 0;
                izin.StartTime = StartTime;
                    izin.EndTime = EndTime;
                izin.CreateDate = DateTime.Now.ToString();
                izin.Operation = Operation;
                izin.IsPlanned = IsPlanned;
                liste.Add(izin);

                Console.WriteLine("kato 1 e girdi");
            }

            if (flag == 2)
            {
                RestClient restClient = new RestClient("https://sso.zorlu.com.tr/DynamicHRService/DynamicInsert");
                var request = new RestRequest(Method.POST);
                request.AddHeader("Authorization", "Bearer "+ GetToken());
                request.AddHeader("Content-Type", "application/json; charset=utf-8");
                request.AddJsonBody(new
                {
                    queryName = "MeyerInsertLeave",
                    data = liste
                });
                foreach (var item in liste)
                {
                    Console.WriteLine(item.StartDate);
                }
                Console.WriteLine("KATO SAYISI : "+liste.Count);
                var result = restClient.Execute(request);
                Console.WriteLine(result.ResponseStatus);
                Console.WriteLine(result.Content.ToString());
                ResultForLeave resultForLeave = JsonConvert.DeserializeObject<ResultForLeave>(result.Content);
                if (resultForLeave.errorMessage is not null)
                {

                }
             //   return resultForPerson;
            }
        }
        public async Task Izin()
        {
            Console.WriteLine("1");
            var izinTalepleri = Data.SqlFill("select s.okod5,s.SicilNo,i.Kod,* from IzinTalepleri it left join SerbestZamanIzinleri sb on it.ID = sb.FormID inner join IzinTipleri i on it.Izintipi = i.ID inner join sicil s on it.SicilId = s.ID where i.Id='35' and (sb.Id is null or sb.Durum <> it.Durum) and SicilNo <> '' and OKod5 <> ''");
            Console.WriteLine("333");
            var gonderilenTalepler = Data.SqlFill("select * from SerbestZamanIzinleri");
            Console.WriteLine("2");
            foreach (var item in izinTalepleri)
            {
                var gonderilendeVarMi = gonderilenTalepler.Where(x => x.FormID == item.Id);
                // create table SerbestZamanIzinleri(Id int, FormID int,Durum int)
                Console.WriteLine("3");
                if (gonderilendeVarMi.Count() == 0)
                {
                    SetLeave(item.okod5, item.SicilNo, item.Baslangic.ToString("yyyy-MM-dd"), item.Bitis.ToString("yyyy-MM-dd"),
                    "001", "Meyer Serbest Zaman İzni", item.Baslangic.ToString("HH:mm:ss"), item.Bitis.ToString("HH:mm:ss"),
                    item.TalepTarih.ToString("yyyy-MM-dd HH:mm:ss.fff"), item.Durum.ToString() == "-1" || item.Durum.ToString() == "-2" ? "D" : "I", item.Durum.ToString() == "0" ? "true" : "false" ,1);
                    Console.WriteLine("Başlangıç Tarihi : " + item.Baslangic + " || Bitiş Tarihi : " + item.Bitis + " || Sicil Numarası : " + item.SicilNo + " Company Code : " + item.okod5);
                    Data.SqlFill("insert into SerbestZamanIzinleri (FormId,Durum) select '" + item.Id + "','" + item.Durum + "'");
                }
                else if (gonderilendeVarMi.Count() != 0)
                {
                    if (gonderilendeVarMi.FirstOrDefault().Durum != item.Durum)
                    {
                        Console.WriteLine("22222222");
                        string isPlanned = "";
                        if (item.Durum.ToString() == "0" || item.Durum.ToString()=="-1")
                        {
                            isPlanned = "true";
                        }
                        else
                        {
                            isPlanned = "false";
                        }
                        SetLeave(item.okod5, item.SicilNo, item.Baslangic.ToString("yyyy-MM-dd"), item.Bitis.ToString("yyyy-MM-dd"),
                    "001", "Meyer Serbest Zaman İzni", item.Baslangic.ToString("HH:mm:ss"), item.Bitis.ToString("HH:mm:ss"),
                    item.TalepTarih.ToString("yyyy-MM-dd HH:mm:ss.fff"), item.Durum.ToString() == "-1" || item.Durum.ToString() == "-2" ? "D" : "U", isPlanned, 1);
                        Data.SqlFill("update SerbestZamanIzinleri set Durum='" + item.Durum + "' where FormId = '" + item.Id + "'");
                    }
                }
                Console.WriteLine("5");

            }

            if (izinTalepleri.Count !=0)
            {

                SetLeave("", "", "", "", "", "", "", "", "", "", "", 2);
            }


        }
    }
}
