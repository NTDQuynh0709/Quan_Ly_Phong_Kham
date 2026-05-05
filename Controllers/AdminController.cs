using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using web.Models;

namespace web.Controllers
{
    public class AdminController : Controller
    {
        private readonly ClinicDbEntities1 db = new ClinicDbEntities1();

        private bool IsAdmin()
        {
            var role = Session["UserRole"]?.ToString();
            return role == "Admin";
        }

        public ActionResult Index()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Tổng quan hệ thống";
            ViewBag.Ctrl = "Admin";

            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);

            ViewBag.TotalDoctors = db.Doctors.Count(d => d.IsActive);
            ViewBag.TotalPatients = db.Patients.Count();
            ViewBag.TotalAppointmentsToday = db.Appointments.Count(a => DbFunctions.TruncateTime(a.AppointmentDate) == today);
            ViewBag.PendingAppointments = db.Appointments.Count(a => a.Status == "Pending");
            ViewBag.ConfirmedAppointments = db.Appointments.Count(a => a.Status == "Confirmed");
            ViewBag.DoneAppointments = db.Appointments.Count(a => a.Status == "Done");
            ViewBag.CancelledAppointments = db.Appointments.Count(a => a.Status == "Cancelled");

            ViewBag.TodayRevenue = db.Appointments
                .Where(a => DbFunctions.TruncateTime(a.AppointmentDate) == today && a.Status == "Done")
                .Select(a => (decimal?)a.TotalFee)
                .Sum() ?? 0;

            ViewBag.MonthRevenue = db.Appointments
                .Where(a => a.AppointmentDate >= monthStart && a.Status == "Done")
                .Select(a => (decimal?)a.TotalFee)
                .Sum() ?? 0;

            ViewBag.TodayAppointments = db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.Doctor.Department)
                .Where(a => DbFunctions.TruncateTime(a.AppointmentDate) == today)
                .OrderBy(a => a.TimeSlot)
                .Take(8)
                .ToList();

            ViewBag.NewPatients = db.Patients
                .OrderByDescending(p => p.CreatedAt)
                .Take(6)
                .ToList();

            return View();
        }

        // ===== QUẢN LÝ CHUYÊN KHOA =====

        [HttpGet]
        public ActionResult DepartmentList(string q = "")
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            ViewBag.Title = "Quản lý chuyên khoa";
            ViewBag.Ctrl = "Department";
            ViewBag.Query = q;

            var query = db.Departments.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(d =>
                    d.DepartmentName.Contains(q) ||
                    (d.Description != null && d.Description.Contains(q)));
            }

            var list = query.OrderBy(d => d.Id).ToList();

            var ids = list.Select(d => d.Id).ToList();
            var counts = db.Doctors
                .Where(x => x.DepartmentId.HasValue && ids.Contains(x.DepartmentId.Value))
                .GroupBy(x => x.DepartmentId.Value)
                .Select(g => new { DeptId = g.Key, Count = g.Count() })
                .ToDictionary(x => x.DeptId, x => x.Count);
            ViewBag.DoctorCountByDept = counts;

            return View(list);
        }

        [HttpGet]
        public ActionResult DepartmentCreate()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            ViewBag.Title = "Thêm chuyên khoa";
            ViewBag.Ctrl = "Department";
            return View(new Department());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DepartmentCreate(Department model)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            ViewBag.Title = "Thêm chuyên khoa";
            ViewBag.Ctrl = "Department";

            if (string.IsNullOrWhiteSpace(model.DepartmentName))
                ModelState.AddModelError("DepartmentName", "Vui lòng nhập tên chuyên khoa.");
            else if (db.Departments.Any(d => d.DepartmentName == model.DepartmentName.Trim()))
                ModelState.AddModelError("DepartmentName", "Tên chuyên khoa đã tồn tại.");

            if (!ModelState.IsValid) return View(model);

            var dept = new Department
            {
                DepartmentName = model.DepartmentName.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim()
            };
            db.Departments.Add(dept);
            db.SaveChanges();

            TempData["Success"] = "Đã thêm chuyên khoa \"" + dept.DepartmentName + "\".";
            return RedirectToAction("DepartmentList");
        }

        [HttpGet]
        public ActionResult DepartmentEdit(int? id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            if (!id.HasValue) return RedirectToAction("DepartmentList");

            var dept = db.Departments.Find(id.Value);
            if (dept == null) return HttpNotFound();

            ViewBag.Title = "Cập nhật chuyên khoa";
            ViewBag.Ctrl = "Department";
            return View(dept);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DepartmentEdit(Department model)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            ViewBag.Title = "Cập nhật chuyên khoa";
            ViewBag.Ctrl = "Department";

            var entity = db.Departments.Find(model.Id);
            if (entity == null) return HttpNotFound();

            if (string.IsNullOrWhiteSpace(model.DepartmentName))
                ModelState.AddModelError("DepartmentName", "Vui lòng nhập tên chuyên khoa.");
            else if (db.Departments.Any(d => d.Id != model.Id && d.DepartmentName == model.DepartmentName.Trim()))
                ModelState.AddModelError("DepartmentName", "Tên chuyên khoa đã tồn tại.");

            if (!ModelState.IsValid) return View(model);

            entity.DepartmentName = model.DepartmentName.Trim();
            entity.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            db.SaveChanges();

            TempData["Success"] = "Đã cập nhật chuyên khoa.";
            return RedirectToAction("DepartmentList");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DepartmentDelete(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var dept = db.Departments.Find(id);
            if (dept == null)
            {
                TempData["Error"] = "Không tìm thấy chuyên khoa.";
                return RedirectToAction("DepartmentList");
            }

            bool hasDoctor = db.Doctors.Any(d => d.DepartmentId == id);
            if (hasDoctor)
            {
                TempData["Error"] = "Không thể xóa: vẫn còn bác sĩ thuộc chuyên khoa này.";
                return RedirectToAction("DepartmentList");
            }

            db.Departments.Remove(dept);
            db.SaveChanges();
            TempData["Success"] = "Đã xóa chuyên khoa \"" + dept.DepartmentName + "\".";
            return RedirectToAction("DepartmentList");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}