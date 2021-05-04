//----------------------------------------------------
// Copyright 2021 Epic Systems Corporation
//----------------------------------------------------

using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace cowin_poller
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly string baseURL = "https://cdn-api.co-vin.in/api/v2/";
        public Program()
        {
            client.DefaultRequestHeaders
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
        }

        static async Task Main(string[] args)
        {
            //https://github.com/bhattbhavesh91/cowin-vaccination-slot-availability/blob/main/district_mapping.csv
            //Poll("atulmirajkar@gmail.com", 363, 18.65, 73.8, 44, 15);
            await ProcessRequest("atulmirajkar@gmail.com", 363, 18.65, 73.8, 44);
        }

        private static void Poll(string email, int districtID, double interestedLat, double interestedLng, int minAgeLimit, int minutes = 15)
        {
            var timer = new Timer(async (obj) => await ProcessRequest(email, districtID, interestedLat, interestedLng, minAgeLimit),null,0,minutes*60*1000);
        }

        private static async Task ProcessRequest(string email, int districtID, double interestedLat, double interestedLng, int minAgeLimit)
        {
            if (email == "" || districtID < 1) return;

            var streamCalendar = client.GetStreamAsync(baseURL + "appointment/sessions/public/calendarByDistrict?district_id=" + districtID + "&date=" + getTodaysDate());
            var calendar = await JsonSerializer.DeserializeAsync<Calendar>(await streamCalendar);

            //sort calendars by interested lat and interested long
            foreach(Center center in calendar.Centers)
            {
                center.interestedLat = interestedLat;
                center.interestedLng = interestedLng; 
            }
            calendar.Centers.Sort();
            var htmlContent = createContent(calendar, minAgeLimit);
            await sendEmail(email,htmlContent);
        }

        private static async Task sendEmail(string email, string htmlContent)
        {
            //https://sendgrid.com/docs/for-developers/sending-email/v3-csharp-code-example/
            //https://github.com/sendgrid/sendgrid-csharp/#installation
            var client = new SendGridClient("");    //todo
            var from = new EmailAddress("atulmirajkar@gmail.com", "atul");
            var subject = "Sending with SendGrid is Fun";
            var to = new EmailAddress(email);
            var msg = MailHelper.CreateSingleEmail(from, to, subject, "", htmlContent);
            await client.SendEmailAsync(msg).ConfigureAwait(false);
        }

        private static string createContent(Calendar calendar, int minAgeLimit)
        {
            string result = "";
            
            foreach(Center center in calendar.Centers)
            {
                string sessionStr = "";
                foreach (SessionCalendar sessionCal in center.Sessions)
                {
                    if (sessionCal.MinAgeLimit < minAgeLimit) continue;

                    sessionStr += $"<div>Date: {sessionCal.Date}</div>";
                    sessionStr += $"<div>Min Age: {sessionCal.MinAgeLimit}</div>";
                    sessionStr += $"<div>Vaccine: {sessionCal.Vaccine}</div>";
                    sessionStr += $"<div>Slots:</div>";
                    foreach (string slot in sessionCal.Slots)
                    {
                        sessionStr += $"<div>{slot}</div>";
                    }
                }

                if (sessionStr == "") continue;


                string centerStr = "";
                centerStr = centerStr + $"<h1>{center.Name}</h1>";
                centerStr = centerStr + $"<h2>{center.Address}</h2>";
                centerStr = centerStr + $"<h2>{center.DistrictName}</h2>";
                centerStr = centerStr + $"<h2>{center.BlockName}</h2>";
                centerStr = centerStr + $"<h2>{center.Pincode}</h2>";

                centerStr = centerStr + $"<div>Fee Type: {center.FeeType}</div>";
                foreach(VaccineFee vaccineFee in center.VaccineFees)
                {
                    centerStr = centerStr + $"<div>Vaccine: {vaccineFee.Vaccine}. Fee: {vaccineFee.Fee}</div>";
                }

                result += centerStr + sessionStr;
            }

            return result;
        }

    

        private static string getTodaysDate()
        {
            DateTime date = DateTime.Today;
            IFormatProvider culture = new System.Globalization.CultureInfo("hi-IN", true);
            return date.GetDateTimeFormats(culture)[0];
        }
    }
}
