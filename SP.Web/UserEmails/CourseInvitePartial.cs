﻿using SP.DataAccess;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace SP.Web.UserEmails
{
    public partial class CourseInvite : IMailBody
    {
        public string Title { get { return "Please Confirm - simulation Course " + FormattedDate(StartTime); } }
        private CourseParticipant _courseParticipant;
        public CourseParticipant CourseParticipant
        {
            get { return _courseParticipant; }
            set
            {
                _courseParticipant = value;
                ToStringFormatProvider = _courseParticipant.Department.Institution.Culture.GetCultureInfo();
            }
        }
        public string BaseUrl { get; set; }
        TimeZoneInfo _tzi;
        TimeZoneInfo Tzi
        {
            get
            {
                return _tzi ?? (_tzi = TimeZoneInfo.FindSystemTimeZoneById(CourseParticipant.Course.Department.Institution.StandardTimeZone));
            }
        }
        DateTime LocalTime(DateTime date)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(date, Tzi);
        }
        string FormattedDate(DateTime date)
        {
            return LocalTime(date).ToString("g", ToStringHelper.FormatProvider);
        }
        string _rsvpFormat;
        string RsvpFormat
        {
            get
            {
                if (_rsvpFormat == null)
                {
                    _rsvpFormat = BaseUrl + "index.html#/rsvp?ParticipantId={0}&CourseId={1}&Attending={2}";
                }
                return _rsvpFormat;
            }
        }

        public IFormatProvider ToStringFormatProvider
        {
            get
            {
                return ToStringHelper.FormatProvider;
            }
            set
            {
                ToStringHelper.FormatProvider = value;
            }
        }

        public string CourseName
        {
            get {
                string returnVar = CourseParticipant.Course.CourseFormat.CourseType.Description;
                if (!Regex.IsMatch(returnVar, @"(\bcourse\b)|(\bsim(ulation)?\b)", RegexOptions.IgnoreCase))
                {
                    returnVar += " course";
                }
                return returnVar;
            } 
        }
        private DateTime _startTime;
        private DateTime StartTime
        {
            get
            {
                if (_startTime == default(DateTime))
                {
                    _startTime = LocalTime(CourseParticipant.Course.StartUtc);
                }
                return _startTime;
            }
        }
        public string StartTimeText
        {
            get
            {
                return string.Format(ToStringHelper.FormatProvider, "{0:D} at {0:t}", StartTime);
            }
        }

        public string FinishTime
        {
            get
            {
                return (LocalTime(CourseParticipant.Course.FinishTimeUtc())).ToString("g", ToStringHelper.FormatProvider);
            }
        }

        public string FacultyMeetingTime
        {
            get
            {
                return CourseParticipant.Course.FacultyMeetingTimeUtc.Value.ToString("g", ToStringHelper.FormatProvider);
            }
        }

        public string GetNotificationUrl(bool canAttend)
        {
            return string.Format(RsvpFormat, CourseParticipant.ParticipantId, CourseParticipant.CourseId, canAttend?"1":"0");
        }
        
    }
}
