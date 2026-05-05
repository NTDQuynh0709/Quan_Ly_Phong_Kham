using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using web.Models;

namespace web.Controllers
{
    public class MedicalRecordController : Controller
    {
        private readonly ClinicDbEntities1 db = new ClinicDbEntities1();

        private bool IsDoctor()
        {
            var role = Session["UserRole"]?.ToString();
            return role == "Doctor";
        }

        private bool IsAdmin()
        {
            var role = Session["UserRole"]?.ToString();
            return role == "Admin";
        }

        private Doctor GetCurrentDoctor()
        {
            if (Session["UserId"] == null) return null;

            int userId;
            if (!int.TryParse(Session["UserId"].ToString(), out userId))
                return null;

            return db.Doctors.FirstOrDefault(d => d.UserId == userId && d.IsActive);
        }

        [HttpGet]
        public ActionResult Index(string q = "", DateTime? fromDate = null, DateTime? toDate = null, int? doctorId = null)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Hồ sơ bệnh án";
            ViewBag.Ctrl = "MedicalRecord";
            ViewBag.Query = q;
            ViewBag.FromDate = fromDate.HasValue ? fromDate.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.ToDate = toDate.HasValue ? toDate.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.DoctorId = doctorId;

            ViewBag.Doctors = new SelectList(
                db.Doctors.OrderBy(d => d.FullName).ToList(),
                "Id", "FullName", doctorId
            );

            var recordQuery = db.MedicalRecords
                .Include(m => m.Appointment)
                .Include(m => m.Appointment.Patient)
                .Include(m => m.Appointment.Doctor)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                recordQuery = recordQuery.Where(m =>
                    m.Appointment.Patient.FullName.Contains(q) ||
                    m.Appointment.Patient.CCCD.Contains(q) ||
                    m.Appointment.Patient.Phone.Contains(q) ||
                    (m.Diagnosis != null && m.Diagnosis.Contains(q)));
            }

            if (fromDate.HasValue)
            {
                var f = fromDate.Value.Date;
                recordQuery = recordQuery.Where(m => DbFunctions.TruncateTime(m.Appointment.AppointmentDate) >= f);
            }

            if (toDate.HasValue)
            {
                var t = toDate.Value.Date;
                recordQuery = recordQuery.Where(m => DbFunctions.TruncateTime(m.Appointment.AppointmentDate) <= t);
            }

            if (doctorId.HasValue && doctorId.Value > 0)
            {
                int did = doctorId.Value;
                recordQuery = recordQuery.Where(m => m.Appointment.DoctorId == did);
            }

            var records = recordQuery
                .OrderByDescending(m => m.Appointment.AppointmentDate)
                .ThenByDescending(m => m.Appointment.TimeSlot)
                .ToList();

            var patientIds = records
                .Select(m => m.Appointment.PatientId)
                .Distinct()
                .ToList();

            var patients = db.Patients
                .Where(p => patientIds.Contains(p.Id))
                .OrderBy(p => p.FullName)
                .ToList();

            var recordCounts = records
                .GroupBy(m => m.Appointment.PatientId)
                .ToDictionary(g => g.Key, g => g.Count());

            var lastVisits = records
                .GroupBy(m => m.Appointment.PatientId)
                .ToDictionary(g => g.Key, g => g.Max(x => x.Appointment.AppointmentDate));

            ViewBag.RecordCounts = recordCounts;
            ViewBag.LastVisits = lastVisits;
            ViewBag.TotalRecords = records.Count;
            ViewBag.TotalPatients = patients.Count;

            return View(patients);
        }

        [HttpGet]
        public ActionResult PatientHistory(int? patientId)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            if (!patientId.HasValue)
                return RedirectToAction("Index");

            var patient = db.Patients.FirstOrDefault(p => p.Id == patientId.Value);
            if (patient == null)
                return HttpNotFound();

            var records = db.MedicalRecords
                .Include(m => m.Appointment)
                .Include(m => m.Appointment.Patient)
                .Include(m => m.Appointment.Doctor)
                .Include(m => m.Appointment.Doctor.Department)
                .Where(m => m.Appointment.PatientId == patientId.Value)
                .OrderByDescending(m => m.Appointment.AppointmentDate)
                .ThenByDescending(m => m.Appointment.TimeSlot)
                .ToList();

            ViewBag.Title = "Lịch sử hồ sơ khám";
            ViewBag.Ctrl = "MedicalRecord";
            ViewBag.Patient = patient;

            ViewBag.TotalVisits = records.Count;
            ViewBag.FirstVisit = records.Any() ? (DateTime?)records.Min(r => r.Appointment.AppointmentDate) : null;
            ViewBag.LastVisit = records.Any() ? (DateTime?)records.Max(r => r.Appointment.AppointmentDate) : null;
            ViewBag.TotalFee = records.Sum(r => (decimal?)r.Appointment.TotalFee) ?? 0;

            ViewBag.DepartmentList = records
                .Where(r => r.Appointment.Doctor.Department != null)
                .Select(r => r.Appointment.Doctor.Department.DepartmentName)
                .Distinct()
                .ToList();

            ViewBag.DoctorList = records
                .Select(r => r.Appointment.Doctor.FullName)
                .Distinct()
                .ToList();

            // Nạp audit log cho TẤT CẢ bệnh án của bệnh nhân này
            // Sắp xếp theo thứ tự thời gian THUẬN (cũ → mới): Sửa → Chốt → Phụ lục 1 → Phụ lục 2...
            var recordIds = records.Select(r => r.Id).ToList();
            var allLogs = db.MedicalRecordLogs
                .Where(l => recordIds.Contains(l.MedicalRecordId))
                .OrderBy(l => l.CreatedAt)
                .ToList();
            ViewBag.LogsByRecord = allLogs
                .GroupBy(l => l.MedicalRecordId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return View(records);
        }

        [HttpGet]
        public ActionResult Edit(int? id)
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            if (!id.HasValue)
                return RedirectToAction("MySchedule", "DoctorPortal");

            var doctor = GetCurrentDoctor();
            if (doctor == null)
                return RedirectToAction("Login", "Account");

            var appointment = db.Appointments
                                .Include(a => a.Patient)
                                .Include(a => a.Doctor)
                                .Include(a => a.Doctor.Department)
                                .FirstOrDefault(a => a.Id == id.Value && a.DoctorId == doctor.Id);

            if (appointment == null)
                return HttpNotFound();

            var medicalRecord = db.MedicalRecords.FirstOrDefault(m => m.AppointmentId == appointment.Id);

            // Chỉ được lập bệnh án mới vào đúng ngày khám.
            // Bệnh án đã tồn tại vẫn cho mở để bác sĩ xem/đối chiếu các ngày sau.
            if (medicalRecord == null)
            {
                var apptDate = appointment.AppointmentDate.Date;
                if (apptDate > DateTime.Today)
                {
                    TempData["Error"] = "Chưa đến ngày hẹn (" + apptDate.ToString("dd/MM/yyyy") + "). Chỉ có thể lập bệnh án vào đúng ngày khám.";
                    return RedirectToAction("MySchedule", "DoctorPortal");
                }
                if (apptDate < DateTime.Today)
                {
                    TempData["Error"] = "Lịch hẹn ngày " + apptDate.ToString("dd/MM/yyyy") + " đã quá hạn, không thể lập bệnh án. Bệnh nhân được tính là không đến.";
                    return RedirectToAction("MySchedule", "DoctorPortal");
                }
            }

            ViewBag.Title = "Lập bệnh án";
            ViewBag.Ctrl = "DoctorPortal";
            ViewBag.Appointment = appointment;

            if (medicalRecord == null)
            {
                medicalRecord = new MedicalRecord
                {
                    AppointmentId = appointment.Id
                };
                ViewBag.Logs = new List<MedicalRecordLog>();
            }
            else
            {
                // Nạp audit log theo thứ tự thời gian THUẬN (cũ → mới)
                ViewBag.Logs = db.MedicalRecordLogs
                    .Where(l => l.MedicalRecordId == medicalRecord.Id)
                    .OrderBy(l => l.CreatedAt)
                    .ToList();
            }

            return View(medicalRecord);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(MedicalRecord model)
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            var doctor = GetCurrentDoctor();
            if (doctor == null)
                return RedirectToAction("Login", "Account");

            var appointment = db.Appointments
                                .Include(a => a.Patient)
                                .Include(a => a.Doctor)
                                .Include(a => a.Doctor.Department)
                                .FirstOrDefault(a => a.Id == model.AppointmentId && a.DoctorId == doctor.Id);

            if (appointment == null)
                return HttpNotFound();

            if (string.IsNullOrWhiteSpace(model.Diagnosis))
                ModelState.AddModelError("Diagnosis", "Vui lòng nhập chẩn đoán.");

            if (string.IsNullOrWhiteSpace(model.Treatment))
                ModelState.AddModelError("Treatment", "Vui lòng nhập hướng điều trị.");

            ViewBag.Title = "Lập bệnh án";
            ViewBag.Ctrl = "DoctorPortal";
            ViewBag.Appointment = appointment;

            if (!ModelState.IsValid)
            {
                var existingForView = db.MedicalRecords.FirstOrDefault(m => m.AppointmentId == model.AppointmentId);
                ViewBag.HasSavedRecord = existingForView != null;
                ViewBag.Logs = existingForView != null
                    ? db.MedicalRecordLogs.Where(l => l.MedicalRecordId == existingForView.Id).OrderBy(l => l.CreatedAt).ToList()
                    : new List<MedicalRecordLog>();
                return View(model);
            }

            var medicalRecord = db.MedicalRecords.FirstOrDefault(m => m.AppointmentId == model.AppointmentId);

            // Cơ chế mới: Lưu = Chốt luôn. Bệnh án đã tồn tại = đã khóa.
            // Mọi điều chỉnh sau đó phải đi qua AddAddendum (ghi chú bổ sung).
            if (medicalRecord != null)
            {
                TempData["Error"] = "Bệnh án đã được lưu và khóa. Mọi điều chỉnh phải thêm dưới dạng ghi chú bổ sung.";
                return RedirectToAction("Edit", new { id = appointment.Id });
            }

            // Chặn lập bệnh án nếu không phải đúng ngày khám (phòng trường hợp gõ URL trực tiếp).
            var apptDatePost = appointment.AppointmentDate.Date;
            if (apptDatePost > DateTime.Today)
            {
                TempData["Error"] = "Chưa đến ngày hẹn (" + apptDatePost.ToString("dd/MM/yyyy") + "). Chỉ có thể lập bệnh án vào đúng ngày khám.";
                return RedirectToAction("MySchedule", "DoctorPortal");
            }
            if (apptDatePost < DateTime.Today)
            {
                TempData["Error"] = "Lịch hẹn ngày " + apptDatePost.ToString("dd/MM/yyyy") + " đã quá hạn, không thể lập bệnh án. Bệnh nhân được tính là không đến.";
                return RedirectToAction("MySchedule", "DoctorPortal");
            }

            int userId = int.Parse(Session["UserId"].ToString());
            string userName = Session["UserName"]?.ToString() ?? doctor.FullName;

            // Tạo bệnh án mới — Lưu lần đầu = Chốt luôn (IsLocked=true)
            medicalRecord = new MedicalRecord
            {
                AppointmentId = model.AppointmentId,
                Symptoms = string.IsNullOrWhiteSpace(model.Symptoms) ? null : model.Symptoms.Trim(),
                Diagnosis = string.IsNullOrWhiteSpace(model.Diagnosis) ? null : model.Diagnosis.Trim(),
                Treatment = string.IsNullOrWhiteSpace(model.Treatment) ? null : model.Treatment.Trim(),
                Prescription = string.IsNullOrWhiteSpace(model.Prescription) ? null : model.Prescription.Trim(),
                Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim(),
                BloodPressure = string.IsNullOrWhiteSpace(model.BloodPressure) ? null : model.BloodPressure.Trim(),
                HeartRate = model.HeartRate,
                Temperature = model.Temperature,
                Height = model.Height,
                Weight = model.Weight,
                IsLocked = true,
                LockedAt = DateTime.Now,
                LockedByUserId = userId,
                LastModifiedAt = DateTime.Now,
                LastModifiedByUserId = userId,
                CreatedAt = DateTime.Now
            };

            db.MedicalRecords.Add(medicalRecord);
            db.SaveChanges();

            // Ghi log "Lock" cho thao tác lưu hồ sơ ban đầu (đánh dấu thời điểm kết thúc khám)
            db.MedicalRecordLogs.Add(new MedicalRecordLog
            {
                MedicalRecordId = medicalRecord.Id,
                LogType = "Lock",
                Reason = "Lưu hồ sơ & kết thúc khám",
                CreatedByUserId = userId,
                CreatedByName = userName,
                CreatedAt = DateTime.Now
            });

            if (appointment.Status != "Cancelled")
                appointment.Status = "Done";

            db.SaveChanges();

            TempData["Success"] = "Đã lưu hồ sơ bệnh án và kết thúc lịch khám.";
            return RedirectToAction("Edit", new { id = appointment.Id });
        }

        // Thêm ghi chú bổ sung (addendum) cho bệnh án đã chốt.
        // addendumType: nhãn loại bổ sung (med/lab/diag/treat/other) — gộp vào trường Reason theo dạng "[Loại] lý do thực".
        // addendumOtherLabel: chỉ dùng khi type = "other", BS tự ghi nhãn cụ thể (vd "Dị ứng thuốc").
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddAddendum(int id, string addendumType, string addendumOtherLabel, string addendumText, string reason)
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            var doctor = GetCurrentDoctor();
            if (doctor == null)
                return RedirectToAction("Login", "Account");

            var appointment = db.Appointments.FirstOrDefault(a => a.Id == id && a.DoctorId == doctor.Id);
            if (appointment == null) return HttpNotFound();

            var medicalRecord = db.MedicalRecords.FirstOrDefault(m => m.AppointmentId == id);
            if (medicalRecord == null || !medicalRecord.IsLocked)
            {
                TempData["Error"] = "Chỉ bệnh án đã lưu mới được thêm ghi chú bổ sung.";
                return RedirectToAction("Edit", new { id = id });
            }

            if (string.IsNullOrWhiteSpace(addendumText))
            {
                TempData["Error"] = "Vui lòng nhập nội dung bổ sung.";
                return RedirectToAction("Edit", new { id = id });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Vui lòng nhập lý do bổ sung.";
                return RedirectToAction("Edit", new { id = id });
            }

            // Nếu chọn "Ghi chú khác" thì BẮT BUỘC nhập nhãn cụ thể
            if (string.Equals((addendumType ?? "").Trim(), "other", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(addendumOtherLabel))
            {
                TempData["Error"] = "Vui lòng ghi cụ thể loại ghi chú khác là gì.";
                return RedirectToAction("Edit", new { id = id });
            }

            int userId = int.Parse(Session["UserId"].ToString());
            string userName = Session["UserName"]?.ToString() ?? doctor.FullName;

            string typeLabel = MapAddendumTypeLabel(addendumType, addendumOtherLabel);
            string finalReason = string.IsNullOrEmpty(typeLabel)
                ? reason.Trim()
                : "[" + typeLabel + "] " + reason.Trim();

            db.MedicalRecordLogs.Add(new MedicalRecordLog
            {
                MedicalRecordId = medicalRecord.Id,
                LogType = "Addendum",
                AddendumText = addendumText.Trim(),
                Reason = finalReason,
                CreatedByUserId = userId,
                CreatedByName = userName,
                CreatedAt = DateTime.Now
            });

            db.SaveChanges();

            TempData["Success"] = "Đã thêm ghi chú bổ sung vào bệnh án.";
            return RedirectToAction("Edit", new { id = id });
        }

        private static string MapAddendumTypeLabel(string type, string otherLabel = null)
        {
            switch ((type ?? "").Trim().ToLowerInvariant())
            {
                case "med":   return "Thêm/đổi/ngưng thuốc";
                case "lab":   return "Bổ sung kết quả xét nghiệm";
                case "diag":  return "Điều chỉnh chẩn đoán";
                case "treat": return "Thay đổi hướng điều trị";
                case "other":
                    return string.IsNullOrWhiteSpace(otherLabel)
                        ? "Ghi chú khác"
                        : "Ghi chú khác - " + otherLabel.Trim();
                default:      return "";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}