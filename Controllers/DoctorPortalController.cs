using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using web.Models;

namespace web.Controllers
{
    public class DoctorPortalController : Controller
    {
        private readonly ClinicDbEntities1 db = new ClinicDbEntities1();

        private bool IsDoctor()
        {
            var role = Session["UserRole"]?.ToString();
            return role == "Doctor";
        }

        private Doctor GetCurrentDoctor()
        {
            if (Session["UserId"] == null) return null;

            int userId;
            if (!int.TryParse(Session["UserId"].ToString(), out userId))
                return null;

            return db.Doctors
                     .Include(d => d.Department)
                     .FirstOrDefault(d => d.UserId == userId && d.IsActive);
        }

        public ActionResult MySchedule(DateTime? date, string status = "", string q = "")
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            var doctor = GetCurrentDoctor();
            if (doctor == null)
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Lịch khám của bác sĩ";
            ViewBag.Ctrl = "DoctorPortal";

            var selectedDate = date ?? DateTime.Today;

            var query = db.Appointments
                          .Include(a => a.Patient)
                          .Include(a => a.Doctor)
                          .Include(a => a.Doctor.Department)
                          .Where(a => a.DoctorId == doctor.Id &&
                                      DbFunctions.TruncateTime(a.AppointmentDate) == selectedDate.Date);

            if (!string.IsNullOrWhiteSpace(status))
            {
                status = status.Trim();
                query = query.Where(a => a.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(a =>
                    a.Patient.FullName.Contains(q) ||
                    a.Patient.CCCD.Contains(q) ||
                    a.Patient.Phone.Contains(q));
            }

            var appointments = query
                .OrderBy(a => a.TimeSlot)
                .ToList();

            ViewBag.CurrentDoctor = doctor;
            ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedStatus = status;
            ViewBag.Query = q;

            ViewBag.TotalCount = appointments.Count;
            ViewBag.PendingCount = appointments.Count(x => x.Status == "Pending");
            ViewBag.ConfirmedCount = appointments.Count(x => x.Status == "Confirmed");
            ViewBag.DoneCount = appointments.Count(x => x.Status == "Done");
            ViewBag.CancelledCount = appointments.Count(x => x.Status == "Cancelled");

            // 7 ngày sắp tới (bao gồm hôm nay) - số lịch mỗi ngày cho doctor này
            var rangeStart = DateTime.Today;
            var rangeEnd = DateTime.Today.AddDays(6);

            var allInRange = db.Appointments
                .Where(a => a.DoctorId == doctor.Id
                         && a.Status != "Cancelled"
                         && DbFunctions.TruncateTime(a.AppointmentDate) >= rangeStart
                         && DbFunctions.TruncateTime(a.AppointmentDate) <= rangeEnd)
                .GroupBy(a => DbFunctions.TruncateTime(a.AppointmentDate))
                .Select(g => new {
                    Day = g.Key,
                    Total = g.Count(),
                    Pending = g.Count(x => x.Status == "Pending")
                })
                .ToList();

            var upcomingDates = new List<UpcomingDay>();
            for (int i = 0; i < 7; i++)
            {
                var d = DateTime.Today.AddDays(i);
                var match = allInRange.FirstOrDefault(x => x.Day == d);
                upcomingDates.Add(new UpcomingDay
                {
                    Date = d,
                    Total = match != null ? match.Total : 0,
                    Pending = match != null ? match.Pending : 0
                });
            }
            ViewBag.UpcomingDates = upcomingDates;

            // Tổng lịch trong 30 ngày tới (để hiển thị quick info)
            var next30 = DateTime.Today.AddDays(30);
            ViewBag.UpcomingTotal = db.Appointments.Count(a =>
                a.DoctorId == doctor.Id
                && a.Status != "Cancelled"
                && DbFunctions.TruncateTime(a.AppointmentDate) >= rangeStart
                && DbFunctions.TruncateTime(a.AppointmentDate) <= next30);

            return View(appointments);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();

            base.Dispose(disposing);
        }
    }

    public class UpcomingDay
    {
        public DateTime Date { get; set; }
        public int Total { get; set; }
        public int Pending { get; set; }
    }
}