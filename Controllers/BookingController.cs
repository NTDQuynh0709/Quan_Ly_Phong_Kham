using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using web.Models;

namespace web.Controllers
{
    public class BookingController : Controller
    {
        private readonly ClinicDbEntities1 db = new ClinicDbEntities1();

        private static readonly List<string> DefaultTimeSlots = new List<string>
        {
            "07:30", "08:00", "08:30", "09:00", "09:30",
            "10:00", "10:30", "11:00",
            "13:30", "14:00", "14:30", "15:00", "15:30", "16:00"
        };

        [HttpGet]
        public ActionResult Create(int? doctorId)
        {
            ViewBag.Title = "Đặt lịch khám";
            ViewBag.Nav = "booking";

            LoadBookingData(doctorId, null);

            if (doctorId.HasValue)
            {
                var doctor = db.Doctors
                               .Include(d => d.Department)
                               .FirstOrDefault(d => d.Id == doctorId.Value && d.IsActive);

                ViewBag.SelectedDoctor = doctor;

                // Cần set FormDoctorId để option trong <select> được đánh dấu selected,
                // tránh JS loadDoctorInfo() thấy value rỗng rồi ghi đè card bằng placeholder.
                if (doctor != null)
                    ViewBag.FormDoctorId = doctor.Id;
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(
            string fullName,
            DateTime? dateOfBirth,
            string gender,
            string cccd,
            string phone,
            string email,
            string address,
            int? doctorId,
            DateTime? appointmentDate,
            string timeSlot,
            string note)
        {
            ViewBag.Title = "Đặt lịch khám";
            ViewBag.Nav = "booking";

            LoadBookingData(doctorId, timeSlot);

            ViewBag.FormFullName = fullName;
            ViewBag.FormDateOfBirth = dateOfBirth;
            ViewBag.FormGender = gender;
            ViewBag.FormCCCD = cccd;
            ViewBag.FormPhone = phone;
            ViewBag.FormEmail = email;
            ViewBag.FormAddress = address;
            ViewBag.FormDoctorId = doctorId;
            ViewBag.FormAppointmentDate = appointmentDate;
            ViewBag.FormTimeSlot = timeSlot;
            ViewBag.FormNote = note;

            if (string.IsNullOrWhiteSpace(fullName))
                ModelState.AddModelError("fullName", "Vui lòng nhập họ tên bệnh nhân.");

            if (string.IsNullOrWhiteSpace(cccd))
                ModelState.AddModelError("cccd", "Vui lòng nhập CCCD.");
            else if (!IsValidCCCD(cccd))
                ModelState.AddModelError("cccd", "CCCD phải gồm đúng 12 chữ số.");

            if (string.IsNullOrWhiteSpace(phone))
                ModelState.AddModelError("phone", "Vui lòng nhập số điện thoại.");
            else if (!IsValidPhone(phone))
                ModelState.AddModelError("phone", "Số điện thoại phải gồm 10 chữ số và đúng đầu số hợp lệ.");

            if (dateOfBirth.HasValue)
            {
                if (dateOfBirth.Value.Date >= DateTime.Today || dateOfBirth.Value.Year < 1900)
                    ModelState.AddModelError("dateOfBirth", "Ngày sinh không hợp lệ.");
            }

            if (!doctorId.HasValue || doctorId.Value <= 0)
                ModelState.AddModelError("doctorId", "Vui lòng chọn bác sĩ.");

            if (!appointmentDate.HasValue)
                ModelState.AddModelError("appointmentDate", "Vui lòng chọn ngày khám.");
            else if (appointmentDate.Value.Date < DateTime.Today)
                ModelState.AddModelError("appointmentDate", "Ngày khám không được nhỏ hơn ngày hiện tại.");
            else if (appointmentDate.Value.DayOfWeek == DayOfWeek.Sunday)
                ModelState.AddModelError("appointmentDate", "Phòng khám không làm việc Chủ nhật. Vui lòng chọn ngày khác.");

            if (string.IsNullOrWhiteSpace(timeSlot))
                ModelState.AddModelError("timeSlot", "Vui lòng chọn giờ khám.");
            else if (!DefaultTimeSlots.Contains(timeSlot))
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

            // Kiểm tra bác sĩ có làm việc vào ngày khám không
            if (doctor != null && appointmentDate.HasValue
                && appointmentDate.Value.DayOfWeek != DayOfWeek.Sunday
                && !web.Helpers.DoctorScheduleHelper.IsDoctorWorkingOnDay(doctor.WorkSchedule, appointmentDate.Value))
            {
                var dowVi = web.Helpers.DoctorScheduleHelper.DayOfWeekViet(appointmentDate.Value);
                ModelState.AddModelError("appointmentDate",
                    "Bác sĩ không làm việc vào " + dowVi + ". Lịch làm việc: " + (doctor.WorkSchedule ?? "chưa cập nhật"));
            }

            ViewBag.SelectedDoctor = doctor;

            Patient patient = null;
            if (!string.IsNullOrWhiteSpace(cccd) && IsValidCCCD(cccd))
            {
                patient = db.Patients.FirstOrDefault(p => p.CCCD == cccd.Trim());
            }

            if (patient == null)
            {
                if (!string.IsNullOrWhiteSpace(phone) && IsValidPhone(phone))
                {
                    bool phoneExists = db.Patients.Any(p => p.Phone == phone.Trim());
                    if (phoneExists)
                        ModelState.AddModelError("phone", "Số điện thoại đã tồn tại trong hệ thống. Vui lòng kiểm tra lại CCCD hoặc dùng số khác.");
                }
            }
            else
            {
                // CCCD đã tồn tại — kiểm tra thông tin nhập có KHỚP với hồ sơ đã có
                // (phòng chống người khác biết CCCD đặt lịch ghi đè dữ liệu gốc)
                var inputName = (fullName ?? "").Trim();
                var inputPhone = (phone ?? "").Trim();

                bool nameMatch = string.Equals(
                    NormalizeName(patient.FullName),
                    NormalizeName(inputName),
                    StringComparison.OrdinalIgnoreCase);

                if (!nameMatch)
                {
                    ModelState.AddModelError("fullName",
                        "CCCD này đã được đăng ký trong hệ thống với họ tên khác. Nếu thông tin chưa đúng, vui lòng liên hệ phòng khám để cập nhật (không thể chỉnh sửa qua trang đặt lịch).");
                }

                if (IsValidPhone(inputPhone) && patient.Phone != inputPhone)
                {
                    ModelState.AddModelError("phone",
                        "CCCD này đã đăng ký với số điện thoại khác. Vui lòng dùng SĐT đã đăng ký hoặc liên hệ phòng khám để cập nhật.");
                }
            }

            if (!ModelState.IsValid)
                return View();

            var apptDate = appointmentDate.Value.Date;

            bool isBusy = db.Appointments.Any(a =>
                a.DoctorId == doctorId.Value &&
                DbFunctions.TruncateTime(a.AppointmentDate) == apptDate &&
                a.TimeSlot == timeSlot &&
                (a.Status == "Pending" || a.Status == "Confirmed" || a.Status == "Done"));

            if (isBusy)
            {
                ModelState.AddModelError("timeSlot", "Khung giờ này đã có bệnh nhân đặt. Vui lòng chọn giờ khác.");
                return View();
            }

            if (patient == null)
            {
                // Bệnh nhân mới — tạo hồ sơ
                patient = new Patient
                {
                    FullName = (fullName ?? "").Trim(),
                    DateOfBirth = dateOfBirth,
                    Gender = string.IsNullOrWhiteSpace(gender) ? null : gender.Trim(),
                    CCCD = cccd.Trim(),
                    Phone = phone.Trim(),
                    Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                    Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
                    CreatedAt = DateTime.Now
                };

                db.Patients.Add(patient);
                db.SaveChanges();
            }
            // Ngược lại: bệnh nhân đã có → CHỈ dùng lại hồ sơ, KHÔNG ghi đè bất kỳ trường nào
            // (tránh rủi ro người biết CCCD ghi đè dữ liệu cá nhân của người khác)
            // Muốn cập nhật thông tin bệnh nhân → phải đến phòng khám gặp Lễ tân/Admin

            decimal serviceFee = doctor != null ? doctor.ConsultationFee : 0;
            decimal discountAmount = 0;
            decimal totalFee = serviceFee - discountAmount;

            var appointment = new Appointment
            {
                PatientId = patient.Id,
                DoctorId = doctorId.Value,
                AppointmentDate = apptDate,
                TimeSlot = timeSlot.Trim(),
                Status = "Pending",
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                ServiceFee = serviceFee,
                DiscountAmount = discountAmount,
                TotalFee = totalFee,
                CreatedAt = DateTime.Now
            };

            db.Appointments.Add(appointment);
            db.SaveChanges();

            TempData["BookingSuccess"] = "Đặt lịch thành công! Mã lịch hẹn của bạn là BK-" + appointment.Id.ToString("D5") + ". Vui lòng lưu lại để tra cứu sau.";
            return RedirectToAction("Lookup", new { keyword = patient.CCCD });
        }

        [HttpGet]
        public ActionResult Lookup(string keyword)
        {
            ViewBag.Title = "Tra cứu lịch hẹn";
            ViewBag.Nav = "lookup";
            ViewBag.Keyword = keyword;
            // Mặc định: KHÔNG cho xem kết quả khám chi tiết
            ViewBag.AllowViewResult = false;

            if (string.IsNullOrWhiteSpace(keyword))
            {
                ViewBag.PatientInfo = null;
                return View(new List<Appointment>());
            }

            keyword = keyword.Trim();

            // Tra cứu theo mã lịch hẹn dạng BK-xxxxx hoặc BKxxxxx
            var bkMatch = System.Text.RegularExpressions.Regex.Match(
                keyword, @"^BK[-\s]*(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (bkMatch.Success)
            {
                int apptId;
                if (int.TryParse(bkMatch.Groups[1].Value, out apptId))
                {
                    var single = db.Appointments
                        .Include(a => a.Doctor)
                        .Include(a => a.Doctor.Department)
                        .Include(a => a.Patient)
                        .Include(a => a.MedicalRecords)
                        .FirstOrDefault(a => a.Id == apptId);

                    if (single != null)
                    {
                        ViewBag.PatientInfo = single.Patient;
                        // Tra bằng mã BK xác thực lịch cụ thể → CHO PHÉP xem kết quả khám
                        ViewBag.AllowViewResult = true;
                        return View(new List<Appointment> { single });
                    }
                }

                ViewBag.PatientInfo = null;
                ViewBag.LookupMessage = "Không tìm thấy lịch hẹn với mã đã nhập.";
                return View(new List<Appointment>());
            }

            var patient = db.Patients.FirstOrDefault(p => p.CCCD == keyword || p.Phone == keyword);

            if (patient == null)
            {
                ViewBag.PatientInfo = null;
                ViewBag.LookupMessage = "Không tìm thấy bệnh nhân với CCCD, số điện thoại hoặc mã lịch đã nhập.";
                return View(new List<Appointment>());
            }

            // Tra bằng CCCD/SĐT → chỉ xem danh sách lịch, KHÔNG cho xem kết quả khám chi tiết
            var appointments = db.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Doctor.Department)
                .Include(a => a.Patient)
                .Include(a => a.MedicalRecords)
                .Where(a => a.PatientId == patient.Id)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.TimeSlot)
                .ToList();

            ViewBag.PatientInfo = patient;
            return View(appointments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LookupPost(string keyword)
        {
            return RedirectToAction("Lookup", new { keyword = keyword });
        }

        [HttpGet]
        public JsonResult GetDoctorInfo(int id)
        {
            var doctor = db.Doctors
                           .Include(d => d.Department)
                           .FirstOrDefault(d => d.Id == id && d.IsActive);

            if (doctor == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Không tìm thấy bác sĩ."
                }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                success = true,
                id = doctor.Id,
                fullName = doctor.FullName,
                specialty = doctor.Specialty,
                departmentName = doctor.Department != null ? doctor.Department.DepartmentName : "",
                degree = doctor.Degree,
                yearsOfExperience = doctor.YearsOfExperience,
                consultationFee = doctor.ConsultationFee,
                consultationFeeText = string.Format("{0:N0} đ", doctor.ConsultationFee),
                workSchedule = doctor.WorkSchedule,
                imageUrl = web.Helpers.DoctorAvatarHelper.GetAvatarUrl(doctor.ImageUrl, doctor.FullName),
                fallbackUrl = web.Helpers.DoctorAvatarHelper.GetAvatarDataUri(doctor.FullName)
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetBusySlots(int doctorId, string date)
        {
            DateTime bookingDate;
            if (doctorId <= 0 || string.IsNullOrWhiteSpace(date) ||
                !DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out bookingDate))
            {
                return Json(new
                {
                    success = false,
                    busySlots = new string[0]
                }, JsonRequestBehavior.AllowGet);
            }

            var busySlots = db.Appointments
                .Where(a =>
                    a.DoctorId == doctorId &&
                    DbFunctions.TruncateTime(a.AppointmentDate) == bookingDate.Date &&
                    (a.Status == "Pending" || a.Status == "Confirmed" || a.Status == "Done"))
                .Select(a => a.TimeSlot)
                .ToList();

            return Json(new
            {
                success = true,
                busySlots = busySlots
            }, JsonRequestBehavior.AllowGet);
        }

        private void LoadBookingData(int? selectedDoctorId = null, string selectedTimeSlot = null)
        {
            var doctors = db.Doctors
                            .Include(d => d.Department)
                            .Where(d => d.IsActive)
                            .OrderBy(d => d.FullName)
                            .ToList();

            ViewBag.Doctors = new SelectList(doctors, "Id", "FullName", selectedDoctorId);
            ViewBag.DoctorsRaw = doctors;
            ViewBag.TimeSlots = new SelectList(DefaultTimeSlots, selectedTimeSlot);

            ViewBag.Departments = new SelectList(
                db.Departments.OrderBy(x => x.DepartmentName).ToList(),
                "Id",
                "DepartmentName"
            );
        }

        // Chuẩn hóa họ tên để so sánh linh hoạt: bỏ dấu, gộp khoảng trắng, lowercase
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var s = name.Trim();
            // Gộp nhiều khoảng trắng thành 1
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");
            // Bỏ dấu tiếng Việt
            s = s.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in s)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC)
                     .Replace('đ', 'd').Replace('Đ', 'D')
                     .ToLowerInvariant();
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