using System.Security.Cryptography;
using System.Runtime.Intrinsics.Arm.Arm64;
using System.Globalization;
using System.Collections.Generic;
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
using System.IO;
using System.Text;

namespace cowin_poller
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly string baseURL = "https://cdn-api.co-vin.in/api/v2/";

        private static Timer timer;

        private static Dictionary<int, Coordinates> pinMap = new Dictionary<int, Coordinates>();

        private static SHA256 sha256Hash = SHA256.Create();

        private static Dictionary<long, string> changeMap = new Dictionary<long, string>();
        public Program()
        {
            client.DefaultRequestHeaders
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
        }

        // static async Task Main(string[] args)
        // {
        //     //https://github.com/bhattbhavesh91/cowin-vaccination-slot-availability/blob/main/district_mapping.csv
        //     //Poll("atulmirajkar@gmail.com", 363, 18.65, 73.8, 44, 15);
        //     await ProcessRequest("atulmirajkar@gmail.com", 363, 18.65, 73.8, 44);
        // }
        static void Main(string[] args)
        {
            createPinMap();
            //https://github.com/bhattbhavesh91/cowin-vaccination-slot-availability/blob/main/district_mapping.csv
            Poll("atulmirajkar@gmail.com", 363, 18.65, 73.8, 44, 5);
        }

        private static void createPinMap()
        {
            //http://www.geonames.org/export/zip/ --> IN.zip --> contains only maharashtra
            string filePath = "pin.csv";
            Dictionary<int, List<Coordinates>> meanMap = new Dictionary<int, List<Coordinates>>();
            using (StreamReader sr = File.OpenText(filePath))
            {
                string s;
                while ((s = sr.ReadLine()) != null)
                {
                    var splitArr = s.Split(",", 3);
                    var pin = Int32.Parse(splitArr[0]);
                    if (!meanMap.ContainsKey(pin))
                    {
                        meanMap[pin] = new List<Coordinates>();
                    }
                    meanMap[pin].Add(new Coordinates(double.Parse(splitArr[1]), double.Parse(splitArr[2])));
                }
            }

            foreach (int pin in meanMap.Keys)
            {
                double lat;
                double lng;
                getMeanLatLng(meanMap[pin], out lat, out lng);
                pinMap[pin] = new Coordinates(lat, lng);
            }

        }

        private static void getMeanLatLng(List<Coordinates> coordinates, out double lat, out double lng)
        {
            double totalLat = 0.0;
            double totalLng = 0.0;
            lat = 0.0;
            lng = 0.0;
            if (coordinates == null || coordinates.Count == 0) return;

            foreach (Coordinates coord in coordinates)
            {
                totalLat += coord.lat;
                totalLng += coord.lng;
            }

            lat = totalLat / coordinates.Count;
            lng = totalLng / coordinates.Count;
        }
        private static void Poll(string email, int districtID, double interestedLat, double interestedLng, int minAgeLimit, int minutes = 15)
        {
            var state = new CallbackState { email = email, districtID = districtID, interestedLat = interestedLat, interestedLng = interestedLng, minAgeLimit = minAgeLimit, minutes = minutes };
            timer = new Timer(processRequestCallback, state, 1000, Timeout.Infinite);
            Thread.Sleep(Timeout.Infinite);
        }

        public static async void processRequestCallback(object state)
        {
            var callbackState = state as CallbackState;
            await ProcessRequest(callbackState.email, callbackState.districtID, callbackState.interestedLat, callbackState.interestedLng, callbackState.minAgeLimit);
            timer.Change(callbackState.minutes * 60 * 1000, Timeout.Infinite);
        }

        private static async Task ProcessRequest(string email, int districtID, double interestedLat, double interestedLng, int minAgeLimit)
        {
            if (email == "" || districtID < 1) return;
            var url = baseURL + "appointment/sessions/public/calendarByDistrict?district_id=" + districtID + "&date=" + getTodaysDate();
            var streamCalendar = client.GetStreamAsync(url);
            var json = await streamCalendar;
            var calendar = await JsonSerializer.DeserializeAsync<Calendar>(json);

            //sort calendars by interested lat and interested long
            foreach (Center center in calendar.Centers)
            {
                center.interestedLat = interestedLat;
                center.interestedLng = interestedLng;
                if (pinMap.ContainsKey(center.Pincode))
                {
                    center.Lat = pinMap[center.Pincode].lat;
                    center.Long = pinMap[center.Pincode].lng;
                }
            }
            calendar.Centers.Sort();
            var htmlContent = createContent(calendar, minAgeLimit);
            if (htmlContent == "") return;
            await sendEmail(email, htmlContent);
        }

        private static async Task sendEmail(string email, string htmlContent)
        {
            //https://sendgrid.com/docs/for-developers/sending-email/v3-csharp-code-example/
            //https://github.com/sendgrid/sendgrid-csharp/#installation
            var client = new SendGridClient("");    //todo enter api key
            var from = new EmailAddress("atulmirajkar@gmail.com", "atul");
            var subject = "Sending with SendGrid is Fun";
            var to = new EmailAddress(email);
            var msg = MailHelper.CreateSingleEmail(from, to, subject, "", htmlContent);
            await client.SendEmailAsync(msg).ConfigureAwait(false);
        }

        private static string createContent(Calendar calendar, int minAgeLimit)
        {
            string result = "";

            foreach (Center center in calendar.Centers)
            {
                if (center.Lat == 0 || center.Long == 0) continue;

                string sessionStr = "";
                foreach (SessionCalendar sessionCal in center.Sessions)
                {
                    if (sessionCal.MinAgeLimit < minAgeLimit || sessionCal.AvailableCapacity < 1) continue;
                    sessionStr += $"<div></div>";
                    sessionStr += $"<div>Date: {sessionCal.Date}</div>";
                    sessionStr += $"<div>Min Age: {sessionCal.MinAgeLimit}</div>";
                    sessionStr += $"<div>Vaccine: {sessionCal.Vaccine}</div>";
                    sessionStr += $"<div>Available: {sessionCal.AvailableCapacity}</div>";
                    sessionStr += $"<div>Slots:</div>";
                    foreach (string slot in sessionCal.Slots)
                    {
                        sessionStr += $"<div>{slot}</div>";
                    }
                }

                if (sessionStr == "") continue;


                string centerStr = "";
                centerStr = centerStr + $"<h1>{center.Name}</h1>";
                if (center.NameL != "")
                {
                    centerStr = centerStr + $"<div>{center.NameL}</div>";
                }
                centerStr = centerStr + $"<div>{center.Address}</div>";
                if (center.AddressL != "")
                {
                    centerStr = centerStr + $"<div>{center.AddressL}</div>";
                }
                centerStr = centerStr + $"<div>{center.DistrictName}</div>";
                centerStr = centerStr + $"<div>{center.BlockName}</div>";
                if (center.BlockNameL != "")
                {
                    centerStr = centerStr + $"<div>{center.BlockNameL}</div>";
                }
                centerStr = centerStr + $"<div>{center.Pincode}</div>";
                centerStr = centerStr + $"<div>Fee Type: {center.FeeType}</div>";

                //check hash
                if (!changeMap.ContainsKey(center.CenterId) || !VerifyHash(changeMap[center.CenterId], GetHash(centerStr)){
                    result += centerStr + sessionStr;
                }
            }

            return result;
        }



        private static string getTodaysDate()
        {
            DateTime date = DateTime.Today;
            IFormatProvider culture = new System.Globalization.CultureInfo("hi-IN", true);
            return date.GetDateTimeFormats(culture)[0];
        }

        private static string GetHash(string input)
        {

            // Convert the input string to a byte array and compute the hash.
            byte[] data = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            var sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }

        // Verify a hash against a string.
        private static bool VerifyHash(string input, string hash)
        {
            // Hash the input.
            var hashOfInput = GetHash(input);

            // Create a StringComparer an compare the hashes.
            StringComparer comparer = StringComparer.OrdinalIgnoreCase;

            return comparer.Compare(hashOfInput, hash) == 0;
        }
    }
}
