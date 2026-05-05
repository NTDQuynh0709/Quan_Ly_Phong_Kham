using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using web.Models;

namespace web.Controllers
{
    public class AppointmentController : Controller
    {
        private readonly ClinicDbEntities1 db = new ClinicDbEntities1();

        private static readonly List<string> DefaultTimeSlots = new List<string>
        {
            "07:30", "08:00", "08:30", "09:00", "09:30",
            "10:00", "10:30", "11:00",
            "13:30", "14:00", "14:30", "15:00", "15:30", "16:00"
        };

        private bool IsReceptionistOrAdmin()
        {
            var role = Session["UserRole"]?.ToString();
            return role == "Admin" || role == "Receptionist";
        }

        [HttpGet]
        public ActionResult Index(DateTime? date, string status = "", string q = "")
        {
            if (!IsReceptionistOrAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Quản lý lịch khám";
            ViewBag.Ctrl = "Appointment";

            var selectedDate = date ?? DateTime.Today;

            var query = db.Appointments
                          .Include(a => a.Patient)
                          .Include(a => a.Doctor)
                          .Include(a => a.Doctor.Department)
                          .Where(a => DbFunctions.TruncateTime(a.AppointmentDate) == selectedDate.Date);

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
                    a.Patient.Phone.Contains(q) ||
                    a.Doctor.FullName.Contains(q));
            }

            var appointments = query
                .OrderBy(a => a.TimeSlot)
                .ThenBy(a => a.Doctor.FullName)
                .ToList();

            ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedStatus = status;
            ViewBag.Query = q;

            ViewBag.TotalCount = appointments.Count;
            ViewBag.PendingCount = appointments.Count(x => x.Status == "Pending");
            ViewBag.ConfirmedCount = appointments.Count(x => x.Status == "Confirmed");
            ViewBag.DoneCount = appointments.Count(x => x.Status == "Done");
            ViewBag.CancelledCount = appointments.Count(x => x.Status == "Cancelled");

            return View(appointments);
        }

        [HttpGet]
        public ActionResult Create()
        {
            if (!IsReceptionistOrAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Tạo lịch khám";
            ViewBag.Ctrl = "Appointment";

            LoadDepartments();
            LoadDoctors();
            LoadTimeSlots();
            LoadPatientsForLookup();

            return View();
        }

        private void LoadPatientsForLookup()
        {
            // Nạp danh sách bệnh nhân để autocomplete datalist
            ViewBag.PatientsForLookup = db.Patients
                .OrderBy(p => p.FullName)
                .Select(p => new {
                    p.Id,
                    p.FullName,
                    p.CCCD,
                    p.Phone,
                    p.DateOfBirth,
                    p.Gender
                })
                .ToList();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(string patientKeyword, int? departmentId, int? doctorId, DateTime? appointmentDate, string timeSlot, string note)
        {
            if (!IsReceptionistOrAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Tạo lịch khám";
            ViewBag.Ctrl = "Appointment";

            LoadDepartments(departmentId);
            LoadDoctors(doctorId);
            LoadTimeSlots(timeSlot);
            LoadPatientsForLookup();

            ViewBag.PatientKeyword = patientKeyword;
            ViewBag.AppointmentDate = appointmentDate;
            ViewBag.Note = note;

            if (string.IsNullOrWhiteSpace(patientKeyword))
                ModelState.AddModelError("patientKeyword", "Vui lòng nhập CCCD, số điện thoại hoặc tên bệnh nhân.");

            if (!doctorId.HasValue || doctorId.Value <= 0)
                ModelState.AddModelError("doctorId", "Vui lòng chọn bác sĩ.");

            if (!appointmentDate.HasValue)
                ModelState.AddModelError("appointmentDate", "Vui lòng chọn ngày khám.");

            if (appointmentDate.HasValue && appointmentDate.Value.Date < DateTime.Today)
                ModelState.AddModelError("appointmentDate", "Ngày khám không được nhỏ hơn ngày hiện tại.");

            if (appointmentDate.HasValue && appointmentDate.Value.DayOfWeek == DayOfWeek.Sunday)
                ModelState.AddModelError("appointmentDate", "Phòng khám không làm việc Chủ nhật.");

            if (string.IsNullOrWhiteSpace(timeSlot))
                ModelState.AddModelError("timeSlot", "Vui lòng chọn giờ khám.");

            if (!string.IsNullOrWhiteSpace(timeSlot) && !DefaultTimeSlots.Contains(timeSlot))
                ModelState.AddModelError("timeSlot", "Khung giờ khám không hợp lệ.");

            // Chặn đặt slot đã qua giờ trong hôm nay
            if (appointmentDate.HasValue
                && !string.IsNullOrWhiteSpace(timeSlot)
                && DefaultTimeSlots.Contains(timeSlot)
                && appointmentDate.Value.Date == DateTime.Today)
            {
                TimeSpan slotTime;
                if (TimeSpan.TryParse(timeSlot, out slotTime))
                {
                    var slotDateTime = appointmentDate.Value.Date.Add(slotTime);
                    if (slotDateTime <= DateTime.Now)
                        ModelState.AddModelError("timeSlot", "Khung giờ này đã qua, vui lòng chọn giờ khám sau thời điểm hiện tại.");
                }
            }

            var doctor = doctorId.HasValue
                ? db.Doctors.Include(d => d.Department).FirstOrDefault(d => d.Id == doctorId.Value && d.IsActive)
                : null;

            if (doctorId.HasValue && doctor == null)
                ModelState.AddModelError("doctorId", "Bác sĩ không tồn tại hoặc đã ngưng hoạt động.");

            // Chặn đặt nếu bác sĩ không làm việc vào ngày đó
            if (doctor != null && appointmentDate.HasValue
                && appointmentDate.Value.DayOfWeek != DayOfWeek.Sunday
                && !web.Helpers.DoctorScheduleHelper.IsDoctorWorkingOnDay(doctor.WorkSchedule, appointmentDate.Value))
            {
                var dowVi = web.Helpers.DoctorScheduleHelper.DayOfWeekViet(appointmentDate.Value);
                ModelState.AddModelError("appointmentDate",
                    "Bác sĩ không làm việc vào " + dowVi + ". Lịch làm việc: " + (doctor.WorkSchedule ?? "chưa cập nhật"));
            }

            Patient patient = null;

            if (!string.IsNullOrWhiteSpace(patientKeyword))
            {
                string keyword = patientKeyword.Trim();

                patient = db.Patients.FirstOrDefault(p => p.CCCD == keyword || p.Phone == keyword);

                if (patient == null)
                {
                    var matchedByName = db.Patients
                        .Where(p => p.FullName.Contains(keyword))
                        .OrderBy(p => p.FullName)
                        .ToList();

                    if (matchedByName.Count == 1)
                    {
                        patient = matchedByName.First();
                    }
                    else if (matchedByName.Count > 1)
                    {
                        ModelState.AddModelError("patientKeyword", "Có nhiều bệnh nhân trùng tên. Vui lòng nhập CCCD hoặc số điện thoại để chọn đúng.");
                    }
                }

                if (patient == null
                    && (!ModelState.ContainsKey("patientKeyword")
                        || ModelState["patientKeyword"].Errors.Count == 0))
                {
                    ModelState.AddModelError("patientKeyword", "Không tìm thấy bệnh nhân. Vui lòng tạo hồ sơ bệnh nhân trước.");
                }
            }

            if (!ModelState.IsValid)
                return View();

            // Guard thêm: nếu vì lý do gì patient vẫn null → chặn trước khi NRE
            if (patient == null)
            {
                ModelState.AddModelError("patientKeyword", "Không tìm thấy bệnh nhân.");
                return View();
            }

            var apptDate = appointmentDate.Value.Date;

            if (IsSlotBusy(doctorId.Value, apptDate, timeSlot, null))
            {
                ModelState.AddModelError("timeSlot", "Khung giờ này đã có bệnh nhân đặt. Vui lòng chọn giờ khác.");
                return View();
            }

            decimal serviceFee = doctor != null ? doctor.ConsultationFee : 0;
            decimal discountAmount = 0;
            decimal totalFee = serviceFee - discountAmount;

            var appointment = new Appointment
            {
                PatientId = patient.Id,
                DoctorId = doctorId.Value,
                AppointmentDate = apptDate,
                TimeSlot = timeSlot.Trim(),
                Status = "Confirmed",
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                ServiceFee = serviceFee,
                DiscountAmount = discountAmount,
                TotalFee = totalFee,
                CreatedAt = DateTime.Now
            };

            db.Appointments.Add(appointment);
            db.SaveChanges();

            TempData["Success"] = "Tạo lịch khám thành công.";
            return RedirectToAction("Index", new { date = apptDate.ToString("yyyy-MM-dd") });
        }

        [HttpGet]
        public ActionResult Edit(int? id)
        {
            if (!IsReceptionistOrAdmin())
                return RedirectToAction("Login", "Account");

            if (!id.HasValue)
                return RedirectToAction("Index");

            var appointment = db.Appointments
                                .Include(a => a.Patient)
                                .Include(a => a.Doctor)
                                .Include(a => a.Doctor.Department)
                                .FirstOrDefault(a => a.Id == id.Value);

            if (appointment == null)
                return HttpNotFound();

            ViewBag.Title = "Sửa lịch khám";
            ViewBag.Ctrl = "Appointment";

            LoadDoctors(appointment.DoctorId);
            LoadTimeSlots(appointment.TimeSlot);

            ViewBag.PatientDisplay = appointment.Patient.FullName + " - " + appointment.Patient.CCCD + " - " + appointment.Patient.Phone;
            ViewBag.SelectedStatus = appointment.Status;

            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, int? doctorId, DateTime? appointmentDate, string timeSlot, string note, string status)
        {
            if (!IsReceptionistOrAdmin())
                return RedirectToAction("Login", "Account");

            var appointment = db.Appointments
                                .Include(a => a.Patient)
                                .Include(a => a.Doctor)
                                .FirstOrDefault(a => a.Id == id);

            if (appointment == null)
                return HttpNotFound();

            ViewBag.Title = "Sửa lịch khám";
            ViewBag.Ctrl = "Appointment";

            LoadDoctors(doctorId ?? appointment.DoctorId);
            LoadTimeSlots(timeSlot ?? appointment.TimeSlot);
            ViewBag.PatientDisplay = appointment.Patient.FullName + " - " + appointment.Patient.CCCD + " - " + appointment.Patient.Phone;
            ViewBag.SelectedStatus = status ?? appointment.Status;

            if (appointment.Status == "Done")
            {
                ModelState.AddModelError("", "Lịch khám đã hoàn thành, không thể sửa.");
                return View(appointment);
            }

            if (!doctorId.HasValue || doctorId.Value <= 0)
                ModelState.AddModelError("doctorId", "Vui lòng chọn bác sĩ.");

            if (!appointmentDate.HasValue)
                ModelState.AddModelError("appointmentDate", "Vui lòng chọn ngày khám.");

            if (appointmentDate.HasValue && appointmentDate.Value.Date < DateTime.Today)
                ModelState.AddModelError("appointmentDate", "Ngày khám không được nhỏ hơn ngày hiện tại.");

            if (appointmentDate.HasValue && appointmentDate.Value.DayOfWeek == DayOfWeek.Sunday)
                ModelState.AddModelError("appointmentDate", "Phòng khám không làm việc Chủ nhật.");

            if (string.IsNullOrWhiteSpace(timeSlot))
                ModelState.AddModelError("timeSlot", "Vui lòng chọn giờ khám.");

            if (!string.IsNullOrWhiteSpace(timeSlot) && !DefaultTimeSlots.Contains(timeSlot))
                ModelState.AddModelError("timeSlot", "Khung giờ khám không hợp lệ.");

            // Chặn đặt slot đã qua giờ trong hôm nay
            if (appointmentDate.HasValue
                && !string.IsNullOrWhiteSpace(timeSlot)
                && DefaultTimeSlots.Contains(timeSlot)
                && appointmentDate.Value.Date == DateTime.Today)
            {
                TimeSpan slotTime;
                if (TimeSpan.TryParse(timeSlot, out slotTime))
                {
                    var slotDateTime = appointmentDate.Value.Date.Add(slotTime);
                    if (slotDateTime <= DateTime.Now)
                        ModelState.AddModelError("timeSlot", "Khung giờ này đã qua, vui lòng chọn giờ khám sau thời điểm hiện tại.");
                }
            }

            var doctor = doctorId.HasValue
                ? db.Doctors.FirstOrDefault(d => d.Id == doctorId.Value && d.IsActive)
                : null;

            if (doctorId.HasValue && doctor == null)
                ModelState.AddModelError("doctorId", "Bác sĩ không tồn tại hoặc đã ngưng hoạt động.");

            if (doctor != null && appointmentDate.HasValue
                && appointmentDate.Value.DayOfWeek != DayOfWeek.Sunday
                && !web.Helpers.DoctorScheduleHelper.IsDoctorWorkingOnDay(doctor.WorkSchedule, appointmentDate.Value))
            {
                var dowVi = web.Helpers.DoctorScheduleHelper.DayOfWeekViet(appointmentDate.Value);
                ModelState.AddModelError("appointmentDate",
                    "Bác sĩ không làm việc vào " + dowVi + ". Lịch làm việc: " + (doctor.WorkSchedule ?? "chưa cập nhật"));
            }

            string[] allowedStatuses = new[] { "Pending", "Confirmed", "Cancelled" };
            if (string.IsNullOrWhiteSpace(status) || !allowedStatuses.Contains(status))
                ModelState.AddModelError("status", "Trạng thái không hợp lệ.");

            if (!ModelState.IsValid)
                return View(appointment);

            var apptDate = appointmentDate.Value.Date;

            if (IsSlotBusy(doctorId.Value, apptDate, timeSlot, id))
            {
                ModelState.AddModelError("timeSlot", "Khung giờ này đã có bệnh nhân đặt. Vui lòng chọn giờ khác.");
                return View(appointment);
            }

            appointment.DoctorId = doctorId.Value;
            appointment.AppointmentDate = apptDate;
            appointment.TimeSlot = timeSlot.Trim();
            appointment.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            appointment.Status = status.Trim();

            decimal serviceFee = doctor != null ? doctor.ConsultationFee : 0;
            appointment.ServiceFee = serviceFee;
            appointment.DiscountAmount = 0;
            appointment.TotalFee = serviceFee;

            db.SaveChanges();

            TempData["Success"] = "Cập nhật lịch khám thành công.";
            return RedirectToAction("Index", new { date = apptDate.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Confirm(int id, string returnDate, string returnStatus, string returnQ)
        {
            if (!IsReceptionistOrAdmin())
                return RedirectToAction("Login", "Account");

            var appointment = db.Appointments
                                .Include(a => a.Patient)
                                .Include(a => a.Doctor)
                                .FirstOrDefault(a => a.Id == id);

            if (appointment == null)
            {
                TempData["Error"] = "Không tìm thấy lịch hẹn.";
                return RedirectToIndex(returnDate, returnStatus, returnQ);
            }

            if (appointment.Status == "Cancelled")
            {
                TempData["Error"] = "Lịch hẹn đã bị hủy, không thể xác nhận.";
                return RedirectToIndex(returnDate, returnStatus, returnQ);
            }

            if (appointment.Status == "Done")
            {
                TempData["Error"] = "Lịch hẹn đã hoàn thành, không thể xác nhận lại.";
                return RedirectToIndex(returnDate, returnStatus, returnQ);
            }

            appointment.Status = "Confirmed";
            db.SaveChanges();

            TempData["Success"] = "Đã xác nhận lịch hẹn thành công.";
            return RedirectToIndex(returnDate, returnStatus, returnQ);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(int id, string returnDate, string returnStatus, string returnQ)
        {
            if (!IsReceptionistOrAdmin())
                return RedirectToAction("Login", "Account");

            var appointment = db.Appointments
                                .Include(a => a.Patient)
                                .Include(a => a.Doctor)
                                .FirstOrDefault(a => a.Id == id);

            if (appointment == null)
            {
                TempData["Error"] = "Không tìm thấy lịch hẹn.";
                return RedirectToIndex(returnDate, returnStatus, returnQ);
            }

            if (appointment.Status == "Done")
            {
                TempData["Error"] = "Lịch hẹn đã hoàn thành, không thể hủy.";
                return RedirectToIndex(returnDate, returnStatus, returnQ);
            }

            // Không cho hủy lịch đã qua giờ hẹn
            DateTime scheduledAt = appointment.AppointmentDate.Date;
            TimeSpan slotTime;
            if (TimeSpan.TryParse(appointment.TimeSlot, out slotTime))
                scheduledAt = scheduledAt.Add(slotTime);

            if (scheduledAt < DateTime.Now)
            {
                TempData["Error"] = "Lịch hẹn đã quá giờ, không thể hủy.";
                return RedirectToIndex(returnDate, returnStatus, returnQ);
            }

            appointment.Status = "Cancelled";
            db.SaveChanges();

            TempData["Success"] = "Đã hủy lịch hẹn thành công.";
            return RedirectToIndex(returnDate, returnStatus, returnQ);
        }

        // Action MarkDone đã được loại bỏ vì sai nghiệp vụ y khoa:
        // Admin/Lễ tân không có quyền đánh dấu lịch "Hoàn thành" mà không có bệnh án chuyên môn.
        // Chỉ Bác sĩ đánh dấu Done qua MedicalRecord.Edit (sau khi lập bệnh án).

        private bool IsSlotBusy(int doctorId, DateTime appointmentDate, string timeSlot, int? ignoreAppointmentId)
        {
            var query = db.Appointments.Where(a =>
                a.DoctorId == doctorId &&
                DbFunctions.TruncateTime(a.AppointmentDate) == appointmentDate &&
                a.TimeSlot == timeSlot &&
                (a.Status == "Pending" || a.Status == "Confirmed" || a.Status == "Done"));

            if (ignoreAppointmentId.HasValue)
                query = query.Where(a => a.Id != ignoreAppointmentId.Value);

            return query.Any();
        }

        private ActionResult RedirectToIndex(string returnDate, string returnStatus, string returnQ)
        {
            DateTime parsedDate;
            if (!DateTime.TryParse(returnDate, out parsedDate))
                parsedDate = DateTime.Today;

            return RedirectToAction("Index", new
            {
                date = parsedDate.ToString("yyyy-MM-dd"),
                status = returnStatus,
                q = returnQ
            });
        }

        private void LoadDoctors(int? selectedDoctorId = null)
        {
            var doctors = db.Doctors
                            .Include(d => d.Department)
                            .Where(d => d.IsActive)
                            .OrderBy(d => d.FullName)
                            .ToList();

            ViewBag.Doctors = new SelectList(doctors, "Id", "FullName", selectedDoctorId);

            // Cho JS filter "Khoa → Bác sĩ" ở view: id, departmentId của từng bác sĩ
            ViewBag.DoctorsByDept = doctors
                .Select(d => new { id = d.Id, deptId = d.DepartmentId })
                .ToList();
        }

        private void LoadDepartments(int? selectedDepartmentId = null)
        {
            var departments = db.Departments
                                .OrderBy(d => d.DepartmentName)
                                .ToList();

            ViewBag.Departments = new SelectList(departments, "Id", "DepartmentName", selectedDepartmentId);
        }

        private void LoadTimeSlots(string selectedTimeSlot = null)
        {
            ViewBag.TimeSlots = new SelectList(DefaultTimeSlots, selectedTimeSlot);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();

            base.Dispose(disposing);
        }
    }
}