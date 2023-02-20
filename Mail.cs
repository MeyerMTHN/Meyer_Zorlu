using System;
using M5Database;
using System.Threading.Tasks;
using System.Threading;
using RestSharp;
using System.Net;
using Newtonsoft.Json;
using System.Collections.Generic;
using M5Service_Zorlu.Model;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using StackExchange.Redis;
using Nancy.Json;
using static M5Service_Zorlu.Model.ClientCredentials;

namespace M5Service_Zorlu
{
    public class Import
    {
       
        
        public string  to { get; set; }
        public string cc { get; set; }
        public string bcc { get; set; }

        public bool SendStatus { get; set; } = false;
        public string  FromTo { get; set; }

        List<dynamic> MailTemplate;       

        DateTime SonCalisma = new(2000, 01, 01);
        
        public static ConnectionMultiplexer redis;
        public  async Task Main(CancellationToken stoppingToken)
        {
            await Task.Delay(1000, stoppingToken);
            new Import().ServiceMail();
            return; 
        }

        public static ConnectionMultiplexer RedisConnect()
        {
            var options = ConfigurationOptions.Parse("localhost:6379");
            options.Password = "1878";
            options.AsyncTimeout = 600000;
            options.ConnectTimeout = 600000;
          
            options.SyncTimeout = 600000;
            if (redis != null)
            {
                if (!redis.IsConnected && !redis.IsConnecting)
                {
                    redis = ConnectionMultiplexer.Connect(options);
                }
            }
            else
            {
                redis = ConnectionMultiplexer.Connect(options);
            }

            return redis;
        }
        public static long createredissession(string ip, string extrainfo, string tokenid, string id, string secureid, string controllername = "")
        {
            List<dynamic> sonuc =Data.SqlFill("select * from loginangel l with (nolock) inner join sicil s with (nolock) on l.xsicilId=s.Id  where l.Id= " + id.ToString());
            string sicilid = sonuc[0].xSicilID.ToString();
            string loginname = sonuc[0].LoginName.ToString() + " (" + sonuc[0].Ad.ToString() + " " + sonuc[0].Soyad.ToString() + ")";

            IDatabase db = RedisConnect().GetDatabase(2);

            IEnumerable<HashEntry> m3 = db.HashScan("sessionHash", tokenid, 500, CommandFlags.None);
            foreach (HashEntry he in m3)
            {

             
                string name = he.Name;
                string value = he.Value;

                session mys = JsonConvert.DeserializeObject<session>(he.Value);
                if (controllername == mys.terminalgrubu && controllername == "maillogin")
                {
                    db.HashDelete("sessionHash", name);
                    Data.SqlFill( "exec [dbo].[sp_session_sonuc] '" + mys.tokenid.ToString() + "', 'Another Login')");

                }

            }


            session mses = new session() { sicilid = sicilid, ip = ip, extrainfo = extrainfo, id = id, secureid = secureid, terminalgrubu = controllername, language = "tr", loginname = loginname, tokenid = tokenid, xtime = DateTime.Now.Ticks.ToString() };
            db.HashSet("sessionHash", new HashEntry[] { new HashEntry(mses.tokenid, JsonConvert.SerializeObject(mses)) });

            return 1;
        }
        public static long removesession(string tokenid)
        {
         
            IDatabase db = RedisConnect().GetDatabase(2);

            db.HashDelete("sessionHash", tokenid);

            Data.SqlFill("exec [dbo].[sp_session_sonuc] '" + tokenid.ToString() + "', 'LogOut'");

            return 1;
        }        
        public static string CreateTokenId()
        {
            string TokenId = "";
            Guid g = Guid.NewGuid();
            Random rnd = new ();
            int card = rnd.Next(52);
            string GuidString = g.ToString() + "-" + DateTime.Now.Millisecond.ToString() + "-" + card;
            TokenId = GuidString;
            TokenId = TokenId.Replace('=', '#');
            return TokenId;
        }
        public static string UploadFileToFtp(string ftpServerIP, string ftpkullanici, string ftpsifre, string dosyaAdi, string dizinAdi)
        {


            FileInfo dosyaBilgisi = new (dosyaAdi);        

            FtpWebRequest ftpIstegi;

            string ftps = "";

            if (!ftpServerIP.Contains("ftp://"))
            {
                ftps = "ftp://";
            }

            if (dizinAdi == "")
            {

                ftpIstegi = (FtpWebRequest)FtpWebRequest.Create(new Uri(

                          ftps + ftpServerIP + "/" + dosyaBilgisi.Name));
            }
            else
            {

                ftpIstegi = (FtpWebRequest)FtpWebRequest.Create(new Uri(

                  ftps + ftpServerIP + "/" + dizinAdi + "/" + dosyaBilgisi.Name));

            }


            ftpIstegi.Credentials = new NetworkCredential(ftpkullanici, ftpsifre);



            // Baglantiyi sürekli açik tutuyor.

            ftpIstegi.KeepAlive = false;



            // Yapilacak islem (Upload)

            ftpIstegi.Method = WebRequestMethods.Ftp.UploadFile;



            //Verinin gönderim sekli.

            ftpIstegi.UseBinary = true;



            //Sunucuya gönderilecek dosya uzunlugu bilgisi

            ftpIstegi.ContentLength = dosyaBilgisi.Length;



            // Buffer uzunlugu 2048 byte

            int bufferUzunlugu = 2048;

            byte[] buff = new byte[10000000];

            int sayi;



            FileStream stream = dosyaBilgisi.OpenRead();



            try
            {

                Stream str = ftpIstegi.GetRequestStream();



                sayi = stream.Read(buff, 0, bufferUzunlugu);



                while (sayi != 0)
                {

                    str.Write(buff, 0, sayi);

                    sayi = stream.Read(buff, 0, bufferUzunlugu);

                }




                str.Close();

                stream.Close();
                return "";

            }

            catch (Exception ex)
            {

                return ex.Message;

            }

        }

        private  void ServiceMail()
        {   
            try
            {
                List<int> Silinecekler = new();

                TimeSpan tsc = new (DateTime.Now.Ticks - SonCalisma.Ticks);

                if (tsc.TotalSeconds > 60 * 5)
                {
                    try
                    {   MailTemplate = Data.SqlFill("exec mailtemplatekontrol");                  
                       
                        SonCalisma = DateTime.Now;

                        List<dynamic> Parameters = Data.SqlFill("select * from parameters with(nolock) where ad like 'Mail%'");

                     
                        foreach (var drp in Parameters)
                        {
                            if (drp.Ad.ToString() == "Mail")
                            {
                                if (drp.Deger.ToString() == "1")
                                {
                                    SendStatus = true;
                                }
                                else
                                {
                                    SendStatus = false;
                                }
                               
                            }

                            if (drp.Ad.ToString() == "MailFrom")
                            {
                                FromTo = drp.Deger.ToString();
                            }
                        }

                    }
                    catch (Exception Err)
                    {
                        Data.mylogger.LogError($"Hata : {Err.Message}");                      
                    }
                }

                try
                {                   
                    foreach (var dr in MailTemplate)
                    {

                        string tokenid = "";
                        try
                        {
                            //mail var mı?

                            string spparams = dr.ReportParams;

                            bool gonder = false;

                            //geçici kapalı redis gecisi
                            if (dr.timertype == "a")
                            {
                                try
                                {

                                    if ((Convert.ToInt32(dr.months) == DateTime.Now.Day || (Convert.ToInt32(dr.months) == 0 && DateTime.Now.AddDays(1).Month != DateTime.Now.Month)) && ((DateTime)dr.startdate) <= DateTime.Now)
                                    {

                                        string[] zaman = dr.hours.ToString().Split(':');
                                        string saat = zaman[0];
                                        string dakika = zaman[1];
                                        int deger = (Convert.ToInt32(saat) * 60) + Convert.ToInt32(dakika);
                                        int deger2 = DateTime.Now.Hour * 60 + DateTime.Now.Minute;

                                        if (deger2 > deger)
                                        {
                                            spparams = dr.ReportParams.ToString();


                                            string[] ara = spparams.Split('|');

                                            if (dr.maildatetype == "1")
                                            {
                                                //dün
                                                DateTime haftabasi = DateTime.Now.AddDays(-1);
                                                DateTime haftasonu = DateTime.Now.AddDays(-1);


                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftabasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftasonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                            }

                                            if (dr.maildatetype.ToString() == "2")
                                            {

                                            
                                                //bugün

                                                DateTime haftabasi = DateTime.Now;
                                                DateTime haftasonu = DateTime.Now;


                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftabasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftasonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }


                                            }

                                            if (dr.maildatetype.ToString() == "3")
                                            {
                                                //geçen hafta

                                                DateTime haftabasi = DateTime.Now.AddDays(-1 * Convert.ToInt32(DateTime.Now.DayOfWeek));
                                                DateTime haftasonu = haftabasi.AddDays(6);

                                                haftabasi.AddDays(-7);
                                                haftasonu.AddDays(-7);


                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftabasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftasonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }


                                            }

                                            if (dr.maildatetype.ToString() == "4")
                                            {
                                                //buhafta

                                                DateTime haftabasi = DateTime.Now.AddDays(-1 * Convert.ToInt32(DateTime.Now.DayOfWeek));
                                                DateTime haftasonu = haftabasi.AddDays(6);


                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftabasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftasonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }




                                            }

                                            if (dr.maildatetype.ToString() == "5")
                                            {
                                                //geçenay

                                                DateTime aybasi = DateTime.Now.AddDays(-1 * (DateTime.Now.Day - 1));

                                                aybasi = aybasi.AddMonths(-1);
                                                DateTime aysonu = aybasi.AddMonths(1).AddDays(-1);


                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + aybasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + aysonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }






                                            }

                                            if (dr.maildatetype.ToString() == "6")
                                            {
                                                //buay

                                                DateTime aybasi = DateTime.Now.AddDays(-1 * (DateTime.Now.Day - 1));
                                                DateTime aysonu = aybasi.AddMonths(1).AddDays(-1);

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + aybasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + aysonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }


                                            }

                                            if (dr.maildatetype.ToString() == "7")
                                            {
                                                //raportemplate

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarih"))
                                                    {
                                                        string degerim = s.Split(':')[1];
                                                        degerim = degerim.Replace('_', ':');
                                                        DateTime tarihim = Convert.ToDateTime(degerim);
                                                        DateTime kayittarih = Convert.ToDateTime(dr.recorddate);
                                                        int minutem = (tarihim.Hour * 60) + tarihim.Minute;

                                                        TimeSpan ts = new TimeSpan(tarihim.Ticks - kayittarih.Ticks);
                                                        int fark = Convert.ToInt32(ts.TotalDays);
                                                        tarihim = DateTime.Now.AddDays(fark).Date.AddMinutes(minutem);
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + tarihim.ToString("dd-MM-yyyy HH_mm_ss"));
                                                    }
                                                }

                                            }


                                            gonder = true;

                                        }
                                    }
                                }
                                catch (Exception Err)
                                {
                                    Data.mylogger.LogError($"TimerType: {Err.Message}");                                   
                                }
                            }

                            //geçici kapalı redis gecisi
                            if (dr.timertype== "g")
                            {

                                try
                                {


                                

                                    string[] gunler = dr.Days.ToString().Split(',');

                                    int gindex = Convert.ToInt32(DateTime.Now.DayOfWeek);

                                    if (gindex == 0)
                                    {
                                        gindex = 7;
                                    }


                             

                                    if (gunler[gindex - 1] == "1" && ((DateTime)dr.startdate) <= DateTime.Now)
                                    {

                                     
                                        string[] zaman = dr.hours.ToString().Split(':');
                                        string saat = zaman[0];
                                        string dakika = zaman[1];
                                        int deger = (Convert.ToInt32(saat) * 60) + Convert.ToInt32(dakika);
                                        int deger2 = DateTime.Now.Hour * 60 + DateTime.Now.Minute;

                                        if (deger2 > deger)
                                        {
                                           
                                            spparams = dr.ReportParams.ToString();
                                            string[] ara = spparams.Split('|');


                                            
                                            if (dr.maildatetype.ToString() == "1")
                                            {
                              
                                                //dün
                                                DateTime haftabasi = DateTime.Now.AddDays(-1);
                                                DateTime haftasonu = DateTime.Now.AddDays(-1);


                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftabasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftasonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                            }

                                            if (dr.maildatetype.ToString() == "2")
                                            {
                                                //bugün

                                              
                                               
                                                DateTime haftabasi = DateTime.Now;
                                                DateTime haftasonu = DateTime.Now;


                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftabasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftasonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }


                                            }

                                            if (dr.maildatetype.ToString() == "3")
                                            {
                                                //geçen hafta

                                                DateTime haftabasi = DateTime.Now.AddDays((-1 * Convert.ToInt32(DateTime.Now.DayOfWeek) + 1));
                                                DateTime haftasonu = haftabasi.AddDays(6);

                                                haftabasi = haftabasi.AddDays(-7);
                                                haftasonu = haftasonu.AddDays(-7);


                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftabasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftasonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }


                                            }

                                            if (dr.maildatetype.ToString() == "4")
                                            {
                                                //buhafta

                                                DateTime haftabasi = DateTime.Now.AddDays((-1 * Convert.ToInt32(DateTime.Now.DayOfWeek) + 1));
                                                DateTime haftasonu = haftabasi.AddDays(6);



                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftabasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftasonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }




                                            }

                                            if (dr.maildatetype.ToString() == "5")
                                            {
                                                //geçenay

                                                DateTime aybasi = DateTime.Now.AddDays(-1 * (DateTime.Now.Day - 1));

                                                aybasi = aybasi.AddMonths(-1);
                                                DateTime aysonu = aybasi.AddMonths(1).AddDays(-1);


                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + aybasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + aysonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }






                                            }

                                            if (dr.maildatetype.ToString() == "6")
                                            {
                                                //buay

                                             
                                                DateTime aybasi = DateTime.Now.AddDays(-1 * (DateTime.Now.Day - 1));
                                                DateTime aysonu = aybasi.AddMonths(1).AddDays(-1);

                                         
                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                    
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + aybasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                     
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + aysonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                            

                                            }

                                            if (dr.maildatetype.ToString() == "7")
                                            {
                                                //raportemplate

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarih"))
                                                    {
                                                        string degerim = s.Split(':')[1];
                                                        degerim = degerim.Replace('_', ':');
                                                        DateTime tarihim = Convert.ToDateTime(degerim);
                                                        DateTime kayittarih = Convert.ToDateTime(dr.recorddate);
                                                        int minutem = (tarihim.Hour * 60) + tarihim.Minute;

                                                        TimeSpan ts = new TimeSpan(tarihim.Ticks - kayittarih.Ticks);
                                                        int fark = Convert.ToInt32(ts.TotalDays);
                                                        tarihim = DateTime.Now.AddDays(fark).Date.AddMinutes(minutem);
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + tarihim.ToString("dd-MM-yyyy HH_mm_ss"));
                                                    }
                                                }

                                            }

                                            if (dr.timertype == "o")
                                            {

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("terminal"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + dr["sicilid"].ToString());
                                                    }
                                                }
                                            }
                                            gonder = true;

                                        }
                                    }
                                }
                                catch (Exception Err)
                                {
                                    Data.mylogger.LogError($"TimerType : {Err.Message}");
                                }

                            }

                            //geçici kapalı redis gecisi
                            if (dr.timertype== "v")
                            {

                                try
                                {

                                    if (((DateTime)dr.startdate) <= DateTime.Now)
                                    {
                                        string hours = ((DateTime)dr.vardiyagonderimzamani).ToString("HH:mm");
                                        string[] zaman = hours.Split(':');
                                        string saat = zaman[0];
                                        string dakika = zaman[1];
                                        int deger = (Convert.ToInt32(saat) * 60) + Convert.ToInt32(dakika);
                                        int deger2 = DateTime.Now.Hour * 60 + DateTime.Now.Minute;

                                        if (deger2 > deger)
                                        {
                                            spparams = dr.ReportParams.ToString();

                                            string[] ara = spparams.Split('|');


                                            if (dr.maildatetype.ToString() == "1")
                                            {
                                                //dün
                                                DateTime haftabasi = DateTime.Now.AddDays(-1);
                                                DateTime haftasonu = DateTime.Now.AddDays(-1);


                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftabasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftasonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                            }

                                            if (dr.maildatetype.ToString() == "2")
                                            {
                                                //bugün

                                                DateTime haftabasi = DateTime.Now;
                                                DateTime haftasonu = DateTime.Now;


                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftabasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftasonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }


                                            }

                                            if (dr.maildatetype.ToString() == "3")
                                            {
                                                //geçen hafta

                                                DateTime haftabasi = DateTime.Now.AddDays(-1 * Convert.ToInt32(DateTime.Now.DayOfWeek));
                                                DateTime haftasonu = haftabasi.AddDays(6);

                                                haftabasi.AddDays(-7);
                                                haftasonu.AddDays(-7);


                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftabasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftasonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }


                                            }

                                            if (dr.maildatetype.ToString() == "4")
                                            {
                                                //buhafta

                                                DateTime haftabasi = DateTime.Now.AddDays(-1 * Convert.ToInt32(DateTime.Now.DayOfWeek));
                                                DateTime haftasonu = haftabasi.AddDays(6);


                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftabasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + haftasonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }




                                            }

                                            if (dr.maildatetype.ToString() == "5")
                                            {
                                                //geçenay

                                                DateTime aybasi = DateTime.Now.AddDays(-1 * (DateTime.Now.Day - 1));

                                                aybasi = aybasi.AddMonths(-1);
                                                DateTime aysonu = aybasi.AddMonths(1).AddDays(-1);


                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + aybasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + aysonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }






                                            }

                                            if (dr.maildatetype.ToString() == "6")
                                            {
                                                //buay

                                                DateTime aybasi = DateTime.Now.AddDays(-1 * (DateTime.Now.Day - 1));
                                                DateTime aysonu = aybasi.AddMonths(1).AddDays(-1);

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbas") || s.ToLower().Contains("bastarih"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + aybasi.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarihbit") || s.ToLower().Contains("bittarih") || s.ToLower().Contains("tarihson"))
                                                    {
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + aysonu.ToString("dd-MM-yyyy 00_00_00"));
                                                    }
                                                }


                                            }

                                            if (dr.maildatetype.ToString() == "7")
                                            {
                                                //raportemplate

                                                foreach (string s in ara)
                                                {
                                                    if (s.ToLower().Contains("tarih"))
                                                    {
                                                        string degerim = s.Split(':')[1];
                                                        degerim = degerim.Replace('_', ':');
                                                        DateTime tarihim = Convert.ToDateTime(degerim);
                                                        DateTime kayittarih = Convert.ToDateTime(dr.recorddate);
                                                        int minutem = (tarihim.Hour * 60) + tarihim.Minute;

                                                        TimeSpan ts = new TimeSpan(tarihim.Ticks - kayittarih.Ticks);
                                                        int fark = Convert.ToInt32(ts.TotalDays);
                                                        tarihim = DateTime.Now.AddDays(fark).Date.AddMinutes(minutem);
                                                        spparams = spparams.Replace(s, s.Split(':')[0] + ':' + tarihim.ToString("dd-MM-yyyy HH_mm_ss"));
                                                    }
                                                }

                                            }

                                      


                                            foreach (string s in ara)
                                            {
                                                if (s.ToLower().Contains("mesai"))
                                                {
                                                    spparams = spparams.Replace(s, s.Split(':')[0] + ':' + dr.Mesailer.ToString());
                                                }
                                            }

                                          
                                        }
                                    }

                                }
                                catch (Exception Err)
                                {
                                    Data.mylogger.LogError($"TimerType : {Err.Message}");                                   
                                }


                            }                      

                            List<dynamic> ft = Data.SqlFill("select * from parameters with (nolock) where ad= 'apilink'");
                   
                            string host = "";

                            foreach (var drf in ft)
                            {
                                host = drf.Deger + '/';

                            }

                            if (gonder &&  host != "")
                            {

                                if (dr.SendingType.ToString() == "s")
                                {                                   
                                    List<dynamic> dt = Data.SqlFill("select Id from loginangel with (nolock) where xsicilId =" + dr.sicilid.ToString());
                                                                       

                                    if (dt.Count == 0)
                                    {                                      
                                        dt = Data.SqlFill ("select top 1 Id from loginangel with (nolock) where loginname = 'meyer'");
                                    }

                                  

                                    string loginid = dt[0].Id.ToString();
                                   
                                    tokenid = CreateTokenId();

                                    IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());

                                    string ip = "127.0.0.1";

                                    foreach (IPAddress addr in localIPs)
                                    {
                                        if (addr.AddressFamily == AddressFamily.InterNetwork)
                                        {
                                            ip = addr.ToString();
                                        }
                                    }



                                    createredissession(ip, dr.timertype, tokenid, loginid, "", "maillogin");
                                    Data.SqlFill("insert into sessioninfo (ip,tokenid,loginid,logintime,extrainfo) select '" + ip + "','" + tokenid + "'," + loginid + ",getdate(),'AngelMailService' from LoginAngel where Id=" + loginid);

                                }

                                if (dr.SendingType.ToString() == "e" || dr.SendingType.ToString() == "f")
                                {

                                 
                                    List<dynamic> dt =  Data.SqlFill("select top 1 Id from loginangel with (nolock) where loginname = 'meyer'");


                                    if (dt.Count == 0)
                                    {

                                        dt = Data.SqlFill("select top 1 Id from loginangel with (nolock) where admin<>0");
                                    }

                                    string loginid = dt[0].Id.ToString();

                                    tokenid = CreateTokenId();

                                    IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());

                                    string ip = "127.0.0.1";

                                    foreach (IPAddress addr in localIPs)
                                    {
                                        if (addr.AddressFamily == AddressFamily.InterNetwork)
                                        {
                                            ip = addr.ToString();
                                        }
                                    }


                                    createredissession(ip, dr.timertype, tokenid, loginid, "", "maillogin");
                                    
                                    Data.SqlFill("insert into sessioninfo (ip,tokenid,loginid,logintime,extrainfo) select '" + ip + "','" + tokenid + "'," + loginid + ",getdate(),'AngelMailService' from LoginAngel where Id=" + loginid);

                                }


                                string reportparams = "";
                                string spname = dr.spname.ToString();
                                string reportid = dr.ReportId.ToString();

                                reportparams = "tokenid=" + tokenid + "&islemtipi=s&reportid=" + reportid + "&params=" + spparams;

                                
                                string apihost = host;
                                string url = "";
 

                                if (dr.icerikformat.ToString() == "pdf")
                                {

                               
                                    
                                    if (dr.ReportType.ToString() == "C")
                                    {
                                      
                                        host = host + "report?Name=";
                                        url = reportparams;

                                    }
                                    else
                                    {
                                       
                                        reportparams = reportparams + "&angelreportid=" + dr.AngelReportId.ToString();
                                        host = host + "customreport?Name=";
                                        url = reportparams;

                                    }

                                  
                                }

                                if (dr.icerikformat.ToString() == "excel")
                                {
                                    
                                    if (dr.ReportType.ToString() == "C")
                                    {

                                        host = host + "excelreport?Name=";
                                        url = reportparams;

                                    }
                                    else
                                    {
                                        reportparams = reportparams + "&angelreportid=" + dr.AngelReportId.ToString();
                                        host = host + "excelcustomreport?Name=";
                                        url = reportparams;

                                    }
                                }

                                if (dr.icerikformat.ToString() == "html")
                                {
                                    if (dr.ReportType.ToString() == "C")
                                    {

                                        host = host + "htmlreport?Name=";
                                        url = reportparams;

                                    }
                                    else
                                    {
                                        reportparams = reportparams + "&angelreportid=" + dr.AngelReportId.ToString();
                                        host = host + "htmlcustomreport?Name=";
                                        url = reportparams;

                                    }
                                }

                                if (dr.icerikformat.ToString() == "text")
                                {
                                    if (dr.ReportType.ToString() == "C")
                                    {

                                        host = host + "textreport?Name=";
                                        url = reportparams;

                                    }
                                    else
                                    {
                                        reportparams = reportparams + "&angelreportid=" + dr.AngelReportId.ToString();
                                        host = host + "textcustomreport?Name=";
                                        url = reportparams;

                                    }
                                }


                             
                                string serviceUrl = host + System.Web.HttpUtility.UrlEncode(url);

                               
                                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(serviceUrl);
                                request.ContentType = "application/json; charset=utf-8";
                                request.Method = "GET";
                                request.KeepAlive = false;

                               

                                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;

                                List<cl_report> view = new ();

                                using (var Response = (HttpWebResponse)request.GetResponse())
                                {

                                    using (var reader = new StreamReader(Response.GetResponseStream()))
                                    {                                      

                                        JavaScriptSerializer js = new ();

                                        var objText = reader.ReadToEnd();
                                    
                                        JavaScriptSerializer serializer = new ();
                                   
                                        view = serializer.Deserialize<List<cl_report>>(objText);
                                    
                                        string rapor = apihost + view[0].link;                                    

                                        WebClient myWebClient = new ();

                                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls |  SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;

                                        myWebClient.DownloadFile(rapor.Replace("api/", ""), view[0].link);                                  

                                        MemoryStream ms = new ();
                                        FileStream fs = new (view[0].link, FileMode.Open);

                                        fs.CopyTo(ms);
                                        fs.Close();

                                        string sendto = "";

                                        if (dr.SendingType.ToString() == "e")
                                        {
                                            sendto = dr.SendingPerson.ToString();
                                        }
                                        if (dr.SendingType.ToString() == "s")
                                        {
                                            sendto = dr.sicilemail.ToString();
                                        }
                                        if (dr.SendingType.ToString() == "v")
                                        {
                                            sendto = dr.sicilemail.ToString();
                                        }



                                        string subject = dr.MailSubject.ToString();

                                        string icerikformat = dr.icerikformat.ToString();

                                      
                                        if (icerikformat == "excel" && dr.ReportType.ToString() == "C")
                                        {
                                            icerikformat = "excel";
                                        }
                                        else if (icerikformat == "excel" && dr.ReportType.ToString() != "C")
                                        {
                                            icerikformat = "excelx";
                                        }
                                        
                                        if (dr.SendingType.ToString() == "f")
                                        {
                                          
                                            string ftpServerIP = "";
                                            string ftpkullanici = "";
                                            string ftpsifre = "";
                                            string dosyaAdi = "";
                                            string dizinAdi = "";

                                           List<dynamic> dff = Data.SqlFill("select * from parameters with (nolock) where ad like '%ftp%'");
                                           
                                            foreach (var dffr in dff)
                                            {
                                                if (dffr.Ad.ToString() == "ftpkullanici")
                                                {
                                                    ftpkullanici = dffr.Deger.ToString();
                                                }
                                                if (dffr.Ad.ToString() == "ftpsifre")
                                                {
                                                    ftpsifre = dffr.Deger.ToString();
                                                }
                                                if (dffr.Ad.ToString() == "ftpsunucu")
                                                {
                                                    ftpServerIP = dffr.Deger.ToString();
                                                }
                                            }


                                            dosyaAdi = subject;

                                            using (FileStream file = new (dosyaAdi, FileMode.Create, System.IO.FileAccess.Write))
                                            {
                                                byte[] bytes = new byte[ms.Length];
                                                ms.Read(bytes, 0, (int)ms.Length);
                                                file.Write(bytes, 0, bytes.Length);
                                                ms.Close();
                                            }

                                            string cevap = UploadFileToFtp(ftpServerIP, ftpkullanici, ftpsifre, dosyaAdi, dizinAdi);

                                            File.Delete(view[0].link);
                                        }
                                        else
                                        {
                                           Data.SqlFill("insert into mailduty ([to],[from],[subject],[body],[recorddate],[PdfFile],[MailServiceId],[AttachFormat],[datainfo]) select '"+ sendto + "','','" + subject + "','file:" + view[0].link + "',getdate(),@PdfFile," + dr.Id.ToString() + ",'" + icerikformat + "','" + view[0].html + "'", ms);
                                            
                                            
                                            File.Delete(view[0].link);
                                        }
                                    }

                                }

                                removesession(tokenid);

                                Silinecekler.Add((Int32)dr.Id);

                               Data.SqlFill( "delete from sessioninfo  with (rowlock)  where tokenid='" + tokenid + "'");

                            }

                        }
                        catch (Exception Err1)
                        {
                            Silinecekler.Add((Int32)dr.Id);
                            removesession(tokenid);
                            Data.mylogger.LogError($" MAIL : {Err1}");

                        }


                    }
                }
                catch (Exception Ex)
                {
                    Data.mylogger.LogError(Ex.Message);
                }

                foreach (int i in Silinecekler)
                {
                    for (int sil = 0; sil < MailTemplate.Count; sil++)
                    {
                        if ((Int32)MailTemplate[sil].Id == i)
                        {
                            MailTemplate.Remove(MailTemplate[sil]);
                            break;
                        }

                    }
                }    

                List<dynamic> Mails = Data.SqlFill("Select top 1 * from MailDuty with (nolock) where recorddate<getdate() and SendTime is null");
                           

                foreach (var drm in Mails)
                {
                    if (!string.IsNullOrEmpty(drm.From))
                    {
                        FromTo = drm.From;
                    }
                    string sunucucevap  = SendMail(Token(), drm.To.ToString(), drm.Subject.ToString(), drm.Body.ToString(), (byte[])drm.PdfFile, FromTo, SendStatus);                    

                    Data.SqlFill("update MailDuty set SendTime=getdate(), result='" + sunucucevap + "' where ID=" + drm.Id.ToString());

                }
            }
            catch (Exception Err)
            {
                Data.mylogger.LogError($"!System Mail Gönderiminde Hata : ({Err.Message})!");              
            }
            
       
        }
        static string Token()
        {
            var client = new RestClient("https://sso.zorlu.com.tr/identityserver/OAuth2/ClientCredentials") { Timeout = -1 };

            var request = new RestRequest(Method.POST);

            request.AddHeader("Content-Type", "application/json");

            var body = @"{
                " + "\n" +
                            $@"  ""client_id"": ""{Data.has_client_id()}"",
                " + "\n" +
                            $@"  ""client_secret"": ""{Data.has_client_secret()}"",
                " + "\n" +
                            @"  ""scope"": ""jwt"",
                " + "\n" +
                            @"  ""grant_type"": ""client_credentials""
                " + "\n" +
                            @"}";

            request.AddParameter("application/json", body, ParameterType.RequestBody);

            IRestResponse response = client.Execute(request);

            return  response.StatusCode switch
            {
                HttpStatusCode.OK => JsonConvert.DeserializeObject<Token>(response.Content).access_token,
                HttpStatusCode.BadRequest => JsonConvert.DeserializeObject<ClientCredentials.Error>(response.Content).errorDescription,
                _ => JsonConvert.DeserializeObject<ClientCredentials.Error>(response.Content).errorDescription,
            };
             
        }



        /// <summary>
        /// Zorlu Servisine mail gönder 
        /// </summary>
        /// <param name="Token"></param>
        /// <param name="Mail"></param>
        /// <param name="Subject"></param>
        /// <param name="Body"></param>
        /// <returns></returns>
        string SendMail(string Token, string Mail, string Subject, string Body, byte[] PdfFile, string x_FromTo, bool x_SendStatus)
        {
            if (x_SendStatus == false)
            {
                return "Mail Sending is disabled!! ";
            }
            
            var client = new RestClient("https://sso.zorlu.com.tr/MailService/SendMail") { Timeout = -1 };

            var request = new RestRequest(Method.POST);

            string[] Mails = Mail.Split(";");

            // To
            if (string.IsNullOrEmpty(Mail) || Mails.Length > 3)
            {
                return $"There is an error in the mail !? Please Controls...";
            }
            else
            {
                foreach (var item in Mails[0].Split(","))
                {
                    to += $@" ""{item}"" ,";
                }
                to = to.Substring(0, to.Length - 1);
            }

            //cc
            if (Mails.Length >= 2)
            {

                foreach (var item in Mails[1].Split(","))
                {
                    cc += $@" ""{item}"" ,";
                }
                cc = cc.Substring(0, cc.Length - 1);
            }

            //bcc
            if (Mails.Length == 3)
            {
                foreach (var item in Mails[2].Split(","))
                {
                    bcc += $@" ""{item}"" ,";
                }
                bcc = bcc.Substring(0, bcc.Length - 1);
            }

            request.AddHeader("Authorization", $"Bearer {Token}");



            var Script = @"{
                    " + "\n" +
                    $@"  ""app_id"": ""{Data.has_client_id()}"",
                    " + "\n" +
                    $@"  ""app_keyhash"": ""{Data.has_client_secret()}"",
                    " + "\n" +
                    $@"  ""body"": ""{Body}"",
                    " + "\n" +
                    $@"  ""subject"": ""{Subject}"",  
                    " + "\n" +
                    @"  ""is_html"": true,
                    " + "\n";


            Script = Script + "\n" +
                            @"  ""to_list"": [
                    " + "\n" +
                            $@"    {to}
                    " + "\n" +
                            @"  ], 
                    " + "\n";

            if (!string.IsNullOrEmpty(cc))
            {
                Script = Script + "\n" +
                                   @"  ""cc_list"": [
                    " + "\n" +
                                   $@"{cc}                                    
                    " + "\n" +
                                   @"  ], 
                    " + "\n";
            }

            if (!string.IsNullOrEmpty(bcc))
            {
                Script = Script + "\n" +
                                   @"  ""bcc_list"": [
                    " + "\n" +
                                   $@"{bcc}
                    " + "\n" +
                                   @"  ], 
                    " + "\n";
            }
            //zorlupsmpdks@zorlu.com
           
            Script = Script + "\n" +

                    $@"  ""from"": ""{x_FromTo}"",
                    " + "\n";

            if (PdfFile.Length > 0)
            {
                Script = Script + "\n" +
                              @"  ""attachments"": [
                                " + "\n" +
                                @"    {
                                " + "\n" +
                                $@"      ""file_name"": ""{Body}"",
                                " + "\n" +
                                $@"      ""content"": ""{Convert.ToBase64String(PdfFile)}""
                                " + "\n" +
                                @"    }
                                " + "\n" +
                                @"  ]";
            }


            Script = Script + "\n" + @"}";

            request.AddParameter("application/json", Script, ParameterType.RequestBody);
           
            IRestResponse response = client.Execute(request);

            string result;

            try
            {
                if (JsonConvert.DeserializeObject<Success>(response.Content).queue_id == 0)
                {
                    result = $"!Error : ({JsonConvert.DeserializeObject<ClientCredentials.Error>(response.Content).errorDescription})!";
                }
                else
                {
                    result = $"Succes({JsonConvert.DeserializeObject<Success>(response.Content).queue_id})";
                }
            }
            catch (Exception ex)
            {
                result = $"!Sytem Error : ({ex.Message})!";
            }

            return result;

        }

    }
}
