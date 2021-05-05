//----------------------------------------------------
// Copyright 2021 Epic Systems Corporation
//----------------------------------------------------

using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace cowin_poller
{
    public partial class Calendar
    {
        [JsonPropertyName("centers")]
        public List<Center> Centers { get; set; }
    }

    public class Center : IComparable<Center>
    {
        [JsonPropertyName("center_id")]
        public long CenterId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("name_l")]
        public string NameL { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("address_l")]
        public string AddressL { get; set; }

        [JsonPropertyName("state_name")]
        public string StateName { get; set; }

        [JsonPropertyName("state_name_l")]
        public string StateNameL { get; set; }

        [JsonPropertyName("district_name")]
        public string DistrictName { get; set; }

        [JsonPropertyName("district_name_l")]
        public string DistrictNameL { get; set; }

        [JsonPropertyName("block_name")]
        public string BlockName { get; set; }

        [JsonPropertyName("block_name_l")]
        public string BlockNameL { get; set; }

        [JsonPropertyName("pincode")]
        public int Pincode { get; set; }

        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("long")]
        public double Long { get; set; }

        [JsonPropertyName("from")]
        public string From { get; set; }

        [JsonPropertyName("to")]
        public string To { get; set; }

        [JsonPropertyName("fee_type")]
        public string FeeType { get; set; }

        [JsonPropertyName("vaccine_fees")]
        public List<VaccineFee> VaccineFees { get; set; }

        [JsonPropertyName("sessions")]
        public List<SessionCalendar> Sessions { get; set; }

        public double interestedLat { get; set; }
        public double interestedLng { get; set; }
        public double distance{get; set;}
        public int CompareTo([AllowNull] Center other)
        {
            if (other == null) return 1;

            var thisDistance = Math.Sqrt(((this.Lat - interestedLat) * (this.Lat - interestedLat)) + ((this.Long - interestedLng) * (this.Long - interestedLng)));
            this.distance = thisDistance;
            var otherDistance = Math.Sqrt(((other.Lat - interestedLat) * (other.Lat - interestedLat)) + ((other.Long - interestedLng) * (other.Long - interestedLng)));
            return thisDistance.CompareTo(otherDistance);
        }
    }

    public class SessionCalendar
    {
        [JsonPropertyName("session_id")]
        public Guid SessionId { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("available_capacity")]
        public long AvailableCapacity { get; set; }

        [JsonPropertyName("min_age_limit")]
        public long MinAgeLimit { get; set; }

        [JsonPropertyName("vaccine")]
        public string Vaccine { get; set; }

        [JsonPropertyName("slots")]
        public List<string> Slots { get; set; }

    }

    public partial class VaccineFee
    {
        [JsonPropertyName("vaccine")]
        public string Vaccine { get; set; }

        [JsonPropertyName("fee")]
        public string Fee { get; set; }
    }
}
