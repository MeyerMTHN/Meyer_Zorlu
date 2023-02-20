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
            await new Import().SicilFull();
            //if (DateTime.Now.Hour == 2)
            //{
            //    await new Import().SicilFull();
            //}
            //else
            //{
            //    await new Import().Sicil();
            //}
            //await new Import().SicilFull();
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
        public ResultForPerson GetPerson(long index)
        {
            RestClient restClient = new RestClient("https://sso.zorlu.com.tr/DynamicHRService/DynamicQuery");
            var request = new RestRequest(Method.POST);
            request.AddHeader("Authorization", "Bearer " + GetToken());
            request.AddHeader("Content-Type", "application/json; charset=utf-8");
            request.AddJsonBody(new { queryName = "MeyerDelta", parameters = new { start = index, take = 100 } });
            var result = restClient.Execute(request);
            ResultForPerson resultForPerson = JsonConvert.DeserializeObject<ResultForPerson>(result.Content);
            return resultForPerson;
        }

        public ResultForPerson GetPerson()
        {
            RestClient restClient = new RestClient("https://sso.zorlu.com.tr/DynamicHRService/DynamicQuery");
            var request = new RestRequest(Method.POST);
            request.AddHeader("Authorization", "Bearer " + GetToken());
            request.AddHeader("Content-Type", "application/json; charset=utf-8");
            request.AddJsonBody(new { queryName = "MeyerFull", parameters = new { } });
            var result = restClient.Execute(request);
            ResultForPerson resultForPerson = JsonConvert.DeserializeObject<ResultForPerson>(result.Content);
            return resultForPerson;
        }
        public async Task SicilFull()
        {

            var Persons = GetPerson().results;
            foreach (var Person in Persons)
            {
                M5Database.SicilEnt.MeyerSicil meyerSicil = new SicilEnt.MeyerSicil();
                meyerSicil.ad = Person.adSoyad.Replace(Person.soyad, "");
                meyerSicil.soyad = Person.soyad;
                meyerSicil.sicilno = Person.sicilNo;
                meyerSicil.firma = Person.sirketIsmi;
                meyerSicil.personelno = Person.tckn;
                meyerSicil.okod5 = Person.sirketKodu;
                meyerSicil.okod19 = "x";
                if (Convert.ToDateTime(Person.grubaGirisTarihi).ToString("yyyy-MM-dd") == "9999-12-31")
                {
                    meyerSicil.giristarih = "0001-01-01";
                }
                else
                {

                    meyerSicil.giristarih = Convert.ToDateTime(Person.grubaGirisTarihi).ToString("yyyy-MM-dd");
                }
                if (Convert.ToDateTime(Person.cikisTarihi).ToString("yyyy-MM-dd") == "9999-12-31")
                {
                    meyerSicil.cikistarih = "0001-01-01";
                }
                else
                {

                    meyerSicil.cikistarih = Convert.ToDateTime(Person.cikisTarihi).ToString("yyyy-MM-dd");
                }
                if (Convert.ToDateTime(Person.dogumTarihi).ToString("yyyy-MM-dd") == "9999-12-31")
                {
                    meyerSicil.dogumtarih = "0001-01-01";
                }
                else
                {

                    meyerSicil.dogumtarih = Convert.ToDateTime(Person.dogumTarihi).ToString("yyyy-MM-dd");
                }
                meyerSicil.pozisyon = Person.pozisyon;
                meyerSicil.gorev = Person.unvan;
                meyerSicil.bolum = Person.departman;
                meyerSicil.direktorluk = Person.ustDepartman;
                meyerSicil.yaka = Person.beyazMavi;
                meyerSicil.email = Person.email;
                meyerSicil.ceptelefon = Person.cepTelefonu; 
                if (!string.IsNullOrEmpty(Person.acikAdres))
                {
                    meyerSicil.adres = Person.acikAdres.Replace("'", "");
                }
                meyerSicil.fromsap = "1";
                if (Person.cinsiyet == "Erkek")
                {

                    meyerSicil.cinsiyet = "1";
                }
                else
                {
                    meyerSicil.cinsiyet = "2";
                }
                meyerSicil.SaveFull();
            }
        }
        public async Task Sicil()
        {
            bool test = true;
            //int i = 1;
            while (test)
            {
                var BaslangicIndex = Data.SqlFill("select BaslangicIndex from EntegrasyonIndex");
                long a = Convert.ToInt64(BaslangicIndex[0].BaslangicIndex);
                var Persons = GetPerson(a).results;
                if (Persons.Length < 100)
                {
                    test = false;
                }
                foreach (var Person in Persons)
                {
                    if (Person.adSoyad is not null || Person.grubaGirisTarihi is not null)
                    {
                        Console.WriteLine(a);
                        M5Database.SicilEnt.MeyerSicil meyerSicil = new SicilEnt.MeyerSicil();
                        meyerSicil.ad = Person.adSoyad.Replace(Person.soyad, "");
                        meyerSicil.soyad = Person.soyad;
                        meyerSicil.sicilno = Person.sicilNo;
                        meyerSicil.firma = Person.sirketIsmi;
                        meyerSicil.personelno = Person.tckn;
                        if (Convert.ToDateTime(Person.grubaGirisTarihi).ToString("yyyy-MM-dd") == "9999-12-31")
                        {
                            meyerSicil.giristarih = "0001-01-01";
                        }
                        else
                        {

                            meyerSicil.giristarih = Convert.ToDateTime(Person.grubaGirisTarihi).ToString("yyyy-MM-dd");
                        }
                        if (Convert.ToDateTime(Person.cikisTarihi).ToString("yyyy-MM-dd") == "9999-12-31")
                        {
                            meyerSicil.cikistarih = "0001-01-01";
                        }
                        else
                        {

                            meyerSicil.cikistarih = Convert.ToDateTime(Person.cikisTarihi).ToString("yyyy-MM-dd");
                        }
                        if (Convert.ToDateTime(Person.dogumTarihi).ToString("yyyy-MM-dd") == "9999-12-31")
                        {
                            meyerSicil.dogumtarih = "0001-01-01";
                        }
                        else
                        {

                            meyerSicil.dogumtarih = Convert.ToDateTime(Person.dogumTarihi).ToString("yyyy-MM-dd");
                        }
                        meyerSicil.pozisyon = Person.pozisyon;
                        meyerSicil.gorev = Person.unvan;
                        meyerSicil.bolum = Person.departman;
                        meyerSicil.direktorluk = Person.ustDepartman;
                        if (Person.telefon is not null)
                        {
                            meyerSicil.telefon1 = Person.telefon.Replace(" ", "");
                        }
                        meyerSicil.okod1 = Person.grubaGirisTarihi;
                        meyerSicil.okod3 = Person.istenCikisSebebi;
                        meyerSicil.okod4 = Person.yonetici;
                        meyerSicil.yaka = Person.beyazMavi;
                        meyerSicil.email = Person.email;
                        meyerSicil.okod5 = Person.sirketKodu;
                        meyerSicil.ceptelefon = Person.cepTelefonu;
                        if (!string.IsNullOrEmpty(Person.acikAdres))
                        {
                            meyerSicil.adres = Person.acikAdres.Replace("'", "");
                        }
                        meyerSicil.fromsap = "1";
                        if (Person.cinsiyet == "Erkek")
                        {

                            meyerSicil.cinsiyet = "1";
                        }
                        else
                        {
                            meyerSicil.cinsiyet = "2";
                        }
                        meyerSicil.Save();
                    }
                    else
                    {

                        var olan = Data.SqlFill("select ad,soyad,cikistarih,okod5 firma from sicil where okod5+SicilNo  ='" + Person.sirketKodu + Person.sicilNo + "'");

                        if (olan.Count != 0)
                        {
                            if (olan[0].cikistarih == null)
                            {
                                M5Database.SicilEnt.MeyerSicil meyerSicil = new SicilEnt.MeyerSicil();
                                meyerSicil.sicilno = Person.sicilNo;
                                if (olan[0].ad == null)
                                {
                                    meyerSicil.ad = "";
                                    meyerSicil.soyad = "";
                                }
                                meyerSicil.okod5 = Person.sirketKodu;
                                meyerSicil.cikistarih = DateTime.Now.ToString("yyyy-MM-dd");
                                meyerSicil.Save();
                            }
                        }

                    }
                }
                if (Persons.Length != 0)
                {
                    Data.SqlFill("update EntegrasyonIndex set BaslangicIndex='" + (Persons.Last().id + 1) + "'");
                }
            }
        }
    }
}
