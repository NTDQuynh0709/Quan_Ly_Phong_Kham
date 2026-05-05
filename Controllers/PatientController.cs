using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using web.Models;

namespace web.Controllers
{
    public class PatientController : Controller
    {
        private readonly ClinicDbEntities1 db = new ClinicDbEntities1();

        private bool IsReceptionistOrAdmin()
        {
            var role = Session["UserRole"]?.ToString();
            return role == "Admin" || role == "Receptionist";
        }

        public ActionResult Index(string q = "")
        {
            if (!IsReceptionistOrAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Quản lý bệnh nhân";
            ViewBag.Ctrl = "Patient";
            ViewBag.Query = q;

            var query = db.Patients.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(p =>
                    p.FullName.Contains(q) ||
                    p.CCCD.Contains(q) ||
                    p.Phone.Contains(q) ||
                    (p.Email != null && p.Email.Contains(q)));
            }

            var patients = query
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            ViewBag.TotalCount = db.Patients.Count();
            ViewBag.FoundCount = patients.Count;

            return View(patients);
        }

        [HttpGet]
        public ActionResult Create()
        {
            if (!IsReceptionistOrAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Tiếp nhận bệnh nhân mới";
            ViewBag.Ctrl = "Patient";
            return View(new Patient());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Patient model)
        {
            if (!IsReceptionistOrAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Tiếp nhận bệnh nhân mới";
            ViewBag.Ctrl = "Patient";

            if (string.IsNullOrWhiteSpace(model.FullName))
                ModelState.AddModelError("FullName", "Vui lòng nhập họ tên.");

            if (string.IsNullOrWhiteSpace(model.CCCD))
                ModelState.AddModelError("CCCD", "Vui lòng nhập CCCD.");
            else if (!IsValidCCCD(model.CCCD))
                ModelState.AddModelError("CCCD", "CCCD phải gồm đúng 12 chữ số.");
            else if (db.Patients.Any(p => p.CCCD == model.CCCD.Trim()))
                ModelState.AddModelError("CCCD", "CCCD đã tồn tại trong hệ thống.");

            if (string.IsNullOrWhiteSpace(model.Phone))
                ModelState.AddModelError("Phone", "Vui lòng nhập số điện thoại.");
            else if (!IsValidPhone(model.Phone))
                ModelState.AddModelError("Phone", "Số điện thoại phải gồm 10 chữ số, đầu 03/05/07/08/09.");
            else if (db.Patients.Any(p => p.Phone == model.Phone.Trim()))
                ModelState.AddModelError("Phone", "Số điện thoại đã tồn tại trong hệ thống.");

            if (model.DateOfBirth.HasValue &&
                (model.DateOfBirth.Value.Date >= DateTime.Today || model.DateOfBirth.Value.Year < 1900))
            {
                ModelState.AddModelError("DateOfBirth", "Ngày sinh không hợp lệ.");
            }

            if (!ModelState.IsValid)
                return View(model);

            var patient = new Patient
            {
                FullName = model.FullName.Trim(),
                DateOfBirth = model.DateOfBirth,
                Gender = string.IsNullOrWhiteSpace(model.Gender) ? null : model.Gender.Trim(),
                CCCD = model.CCCD.Trim(),
                Phone = model.Phone.Trim(),
                Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim(),
                CreatedAt = DateTime.Now
            };
            db.Patients.Add(patient);
            db.SaveChanges();

            TempData["Success"] = "Đã tiếp nhận bệnh nhân \"" + patient.FullName + "\" (mã BN #" + patient.Id + ").";
            return RedirectToAction("Detail", new { id = patient.Id });
        }

        [HttpGet]
        public ActionResult Detail(int? id)
        {
            if (!IsReceptionistOrAdmin())
                return RedirectToAction("Login", "Account");

            if (!id.HasValue)
                return RedirectToAction("Index");

            var patient = db.Patients.FirstOrDefault(p => p.Id == id.Value);
            if (patient == null)
                return HttpNotFound();

            ViewBag.Title = "Chi tiết bệnh nhân";
            ViewBag.Ctrl = "Patient";

            ViewBag.History = db.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Doctor.Department)
                .Where(a => a.PatientId == id.Value)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.TimeSlot)
                .ToList();

            ViewBag.Records = db.MedicalRecords
                .Include(m => m.Appointment)
                .Include(m => m.Appointment.Doctor)
                .Where(m => m.Appointment.PatientId == id.Value)
                .OrderByDescending(m => m.Appointment.AppointmentDate)
                .ThenByDescending(m => m.Appointment.TimeSlot)
                .ToList();

            return View(patient);
        }

        [HttpGet]
        public ActionResult Edit(int? id)
        {
            if (!IsReceptionistOrAdmin())
                return RedirectToAction("Login", "Account");

            if (!id.HasValue)
                return RedirectToAction("Index");

            var patient = db.Patients.FirstOrDefault(p => p.Id == id.Value);
            if (patient == null)
                return HttpNotFound();

            ViewBag.Title = "Cập nhật bệnh nhân";
            ViewBag.Ctrl = "Patient";
            return View(patient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Patient model)
        {
            if (!IsReceptionistOrAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Cập nhật bệnh nhân";
            ViewBag.Ctrl = "Patient";

            var patient = db.Patients.FirstOrDefault(p => p.Id == model.Id);
            if (patient == null)
                return HttpNotFound();

            if (string.IsNullOrWhiteSpace(model.FullName))
                ModelState.AddModelError("FullName", "Vui lòng nhập họ tên.");

            if (string.IsNullOrWhiteSpace(model.CCCD))
                ModelState.AddModelError("CCCD", "Vui lòng nhập CCCD.");
            else if (!IsValidCCCD(model.CCCD))
                ModelState.AddModelError("CCCD", "CCCD phải gồm đúng 12 chữ số.");

            if (string.IsNullOrWhiteSpace(model.Phone))
                ModelState.AddModelError("Phone", "Vui lòng nhập số điện thoại.");
            else if (!IsValidPhone(model.Phone))
                ModelState.AddModelError("Phone", "Số điện thoại phải gồm 10 chữ số và đúng đầu số hợp lệ.");

            if (model.DateOfBirth.HasValue)
            {
                if (model.DateOfBirth.Value.Date >= DateTime.Today || model.DateOfBirth.Value.Year < 1900)
                    ModelState.AddModelError("DateOfBirth", "Ngày sinh không hợp lệ.");
            }

            bool cccdExists = db.Patients.Any(p => p.Id != model.Id && p.CCCD == model.CCCD);
            if (cccdExists)
                ModelState.AddModelError("CCCD", "CCCD đã tồn tại trong hệ thống.");

            bool phoneExists = db.Patients.Any(p => p.Id != model.Id && p.Phone == model.Phone);
            if (phoneExists)
                ModelState.AddModelError("Phone", "Số điện thoại đã tồn tại trong hệ thống.");

            if (!ModelState.IsValid)
                return View(model);

            patient.FullName = (model.FullName ?? "").Trim();
            patient.DateOfBirth = model.DateOfBirth;
            patient.Gender = string.IsNullOrWhiteSpace(model.Gender) ? null : model.Gender.Trim();
            patient.CCCD = (model.CCCD ?? "").Trim();
            patient.Phone = (model.Phone ?? "").Trim();
            patient.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
            patient.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();

            db.SaveChanges();

            TempData["Success"] = "Cập nhật hồ sơ bệnh nhân thành công.";
            return RedirectToAction("Index");
        }

        private bool IsValidPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            phone = phone.Trim();

            if (phone.Length != 10) return false;
            if (!phone.All(char.IsDigit)) return false;

            return phone.StartsWith("03") ||
                   phone.StartsWith("05") ||
                   phone.StartsWith("07") ||
                   phone.StartsWith("08") ||
                   phone.StartsWith("09");
        }

        private bool IsValidCCCD(string cccd)
        {
            if (string.IsNullOrWhiteSpace(cccd)) return false;
            cccd = cccd.Trim();
            return cccd.Length == 12 && cccd.All(char.IsDigit);
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