using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using web.Helpers;
using web.Models;

namespace web.Controllers
{
    public class DoctorController : Controller
    {
        private readonly ClinicDbEntities1 db = new ClinicDbEntities1();

        private bool IsAdmin()
        {
            return Session["UserRole"] != null
                && Session["UserRole"].ToString() == "Admin";
        }

        public ActionResult Index(string q = "", int? departmentId = null, string status = "", string sort = "")
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Quản lý bác sĩ";
            ViewBag.Ctrl = "Doctor";
            ViewBag.Query = q;
            ViewBag.DepartmentId = departmentId;
            ViewBag.Status = status;
            ViewBag.Sort = sort;

            // Dropdown khoa để filter
            ViewBag.DepartmentList = new SelectList(
                db.Departments.OrderBy(x => x.DepartmentName).ToList(),
                "Id", "DepartmentName", departmentId
            );

            var query = db.Doctors
                .Include(d => d.Department)
                .Include(d => d.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(d =>
                    d.FullName.Contains(q) ||
                    d.Specialty.Contains(q) ||
                    d.Phone.Contains(q) ||
                    d.Email.Contains(q) ||
                    (d.Department != null && d.Department.DepartmentName.Contains(q)));
            }

            if (departmentId.HasValue && departmentId.Value > 0)
            {
                int did = departmentId.Value;
                query = query.Where(d => d.DepartmentId == did);
            }

            if (status == "active")
                query = query.Where(d => d.IsActive);
            else if (status == "inactive")
                query = query.Where(d => !d.IsActive);

            // Sắp xếp
            switch (sort)
            {
                case "fee_asc":
                    query = query.OrderBy(d => d.ConsultationFee).ThenBy(d => d.FullName);
                    break;
                case "fee_desc":
                    query = query.OrderByDescending(d => d.ConsultationFee).ThenBy(d => d.FullName);
                    break;
                case "exp_desc":
                    query = query.OrderByDescending(d => d.YearsOfExperience).ThenBy(d => d.FullName);
                    break;
                default:
                    query = query.OrderBy(d => d.FullName);
                    break;
            }

            var list = query.ToList();

            ViewBag.TotalCount = db.Doctors.Count();
            ViewBag.ActiveCount = db.Doctors.Count(d => d.IsActive);
            ViewBag.InactiveCount = db.Doctors.Count(d => !d.IsActive);
            ViewBag.FilteredCount = list.Count;

            return View(list);
        }

        [HttpGet]
        public ActionResult Create()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Thêm bác sĩ";
            ViewBag.Ctrl = "Doctor";
            LoadDepartments();
            return View(new Doctor());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(string username, string password, Doctor doctor, HttpPostedFileBase imageFile)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Thêm bác sĩ";
            ViewBag.Ctrl = "Doctor";
            LoadDepartments(doctor.DepartmentId);

            if (string.IsNullOrWhiteSpace(username))
                ModelState.AddModelError("username", "Vui lòng nhập tên đăng nhập.");

            if (string.IsNullOrWhiteSpace(password))
                ModelState.AddModelError("password", "Vui lòng nhập mật khẩu.");

            if (string.IsNullOrWhiteSpace(doctor.FullName))
                ModelState.AddModelError("FullName", "Vui lòng nhập họ tên bác sĩ.");

            if (string.IsNullOrWhiteSpace(doctor.Specialty))
                ModelState.AddModelError("Specialty", "Vui lòng nhập chuyên khoa.");

            if (!doctor.DepartmentId.HasValue || doctor.DepartmentId.Value <= 0)
                ModelState.AddModelError("DepartmentId", "Vui lòng chọn phòng ban.");

            if (string.IsNullOrWhiteSpace(doctor.Phone))
                ModelState.AddModelError("Phone", "Vui lòng nhập số điện thoại.");

            if (!string.IsNullOrWhiteSpace(doctor.Phone) && !IsValidPhone(doctor.Phone))
                ModelState.AddModelError("Phone", "Số điện thoại không hợp lệ.");

            if (!string.IsNullOrWhiteSpace(username) && db.Users.Any(u => u.Username == username.Trim()))
                ModelState.AddModelError("username", "Tên đăng nhập đã tồn tại.");

            string uploadError;
            if (!IsValidImageFile(imageFile, out uploadError))
                ModelState.AddModelError("imageFile", uploadError);

            if (!ModelState.IsValid)
                return View(doctor);

            var user = new User
            {
                Username = username.Trim(),
                PasswordHash = PasswordHelper.Hash(password.Trim()),
                FullName = doctor.FullName.Trim(),
                Role = "Doctor",
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            db.Users.Add(user);
            db.SaveChanges();

            doctor.UserId = user.Id;
            doctor.FullName = SafeTrim(doctor.FullName);
            doctor.Specialty = SafeTrim(doctor.Specialty);
            doctor.Phone = SafeTrim(doctor.Phone);
            doctor.Email = SafeTrim(doctor.Email);
            doctor.Gender = SafeTrim(doctor.Gender);
            doctor.Address = SafeTrim(doctor.Address);
            doctor.Degree = SafeTrim(doctor.Degree);
            doctor.Biography = SafeTrim(doctor.Biography);
            doctor.WorkSchedule = SafeTrim(doctor.WorkSchedule);
            doctor.IsActive = true;
            doctor.CreatedAt = DateTime.Now;

            string uploadedPath = SaveDoctorImage(imageFile);
            if (!string.IsNullOrEmpty(uploadedPath))
                doctor.ImageUrl = uploadedPath;

            db.Doctors.Add(doctor);
            db.SaveChanges();

            TempData["Success"] = "Thêm bác sĩ thành công.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Edit(int? id)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            if (!id.HasValue)
                return RedirectToAction("Index");

            var doctor = db.Doctors.Find(id.Value);
            if (doctor == null)
                return HttpNotFound();

            ViewBag.Title = "Cập nhật bác sĩ";
            ViewBag.Ctrl = "Doctor";
            LoadDepartments(doctor.DepartmentId);

            return View(doctor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Doctor doctor, HttpPostedFileBase imageFile)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Cập nhật bác sĩ";
            ViewBag.Ctrl = "Doctor";
            LoadDepartments(doctor.DepartmentId);

            var entity = db.Doctors.Find(doctor.Id);
            if (entity == null)
                return HttpNotFound();

            if (string.IsNullOrWhiteSpace(doctor.FullName))
                ModelState.AddModelError("FullName", "Vui lòng nhập họ tên bác sĩ.");

            if (string.IsNullOrWhiteSpace(doctor.Specialty))
                ModelState.AddModelError("Specialty", "Vui lòng nhập chuyên khoa.");

            if (!doctor.DepartmentId.HasValue || doctor.DepartmentId.Value <= 0)
                ModelState.AddModelError("DepartmentId", "Vui lòng chọn phòng ban.");

            if (string.IsNullOrWhiteSpace(doctor.Phone))
                ModelState.AddModelError("Phone", "Vui lòng nhập số điện thoại.");

            if (!string.IsNullOrWhiteSpace(doctor.Phone) && !IsValidPhone(doctor.Phone))
                ModelState.AddModelError("Phone", "Số điện thoại không hợp lệ.");

            string uploadErrorEdit;
            if (!IsValidImageFile(imageFile, out uploadErrorEdit))
                ModelState.AddModelError("imageFile", uploadErrorEdit);

            if (!ModelState.IsValid)
                return View(doctor);

            entity.FullName = SafeTrim(doctor.FullName);
            entity.Specialty = SafeTrim(doctor.Specialty);
            entity.DepartmentId = doctor.DepartmentId;
            entity.Phone = SafeTrim(doctor.Phone);
            entity.Email = SafeTrim(doctor.Email);
            entity.Gender = SafeTrim(doctor.Gender);
            entity.DateOfBirth = doctor.DateOfBirth;
            entity.Address = SafeTrim(doctor.Address);
            entity.Degree = SafeTrim(doctor.Degree);
            entity.YearsOfExperience = doctor.YearsOfExperience;
            entity.ConsultationFee = doctor.ConsultationFee < 0 ? 0 : doctor.ConsultationFee;
            entity.Biography = SafeTrim(doctor.Biography);
            entity.WorkSchedule = SafeTrim(doctor.WorkSchedule);
            entity.IsActive = doctor.IsActive;

            string newImagePath = SaveDoctorImage(imageFile);
            if (!string.IsNullOrEmpty(newImagePath))
            {
                entity.ImageUrl = newImagePath;
            }
            else if (!string.IsNullOrWhiteSpace(doctor.ImageUrl))
            {
                entity.ImageUrl = SafeTrim(doctor.ImageUrl);
            }

            if (entity.UserId.HasValue)
            {
                var user = db.Users.Find(entity.UserId.Value);
                if (user != null)
                {
                    user.FullName = entity.FullName;
                    user.IsActive = entity.IsActive;
                }
            }

            db.SaveChanges();
            TempData["Success"] = "Cập nhật bác sĩ thành công.";
            return RedirectToAction("Index");
        }

        private void LoadDepartments(int? selectedId = null)
        {
            ViewBag.DeptId = new SelectList(
                db.Departments.OrderBy(x => x.DepartmentName).ToList(),
                "Id",
                "DepartmentName",
                selectedId
            );
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

        private string SafeTrim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private bool IsValidImageFile(HttpPostedFileBase file, out string error)
        {
            error = null;
            if (file == null || file.ContentLength == 0) return true;

            const long maxSize = 3 * 1024 * 1024;
            if (file.ContentLength > maxSize)
            {
                error = "Ảnh phải nhỏ hơn 3MB.";
                return false;
            }

            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            string[] allowed = { ".jpg", ".jpeg", ".png", ".webp" };
            if (Array.IndexOf(allowed, ext) < 0)
            {
                error = "Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.";
                return false;
            }

            return true;
        }

        private string SaveDoctorImage(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0) return null;

            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            string fileName = "doctor_" + DateTime.Now.Ticks + ext;

            string folder = Server.MapPath("~/Content/images/doctors/");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string fullPath = Path.Combine(folder, fileName);
            file.SaveAs(fullPath);

            return "~/Content/images/doctors/" + fileName;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}