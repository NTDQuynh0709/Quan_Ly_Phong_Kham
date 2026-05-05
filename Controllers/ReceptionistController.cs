using System;
using System.Linq;
using System.Web.Mvc;
using web.Helpers;
using web.Models;

namespace web.Controllers
{
    public class ReceptionistController : Controller
    {
        private readonly ClinicDbEntities1 db = new ClinicDbEntities1();

        private bool IsAdmin()
        {
            return Session["UserRole"] != null
                && Session["UserRole"].ToString() == "Admin";
        }

        public ActionResult Index(string q = "")
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Quản lý lễ tân";
            ViewBag.Ctrl = "Receptionist";
            ViewBag.Query = q;

            var query = db.Receptionists.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(r =>
                    r.FullName.Contains(q) ||
                    r.Phone.Contains(q) ||
                    r.Email.Contains(q));
            }

            var list = query
                .OrderBy(r => r.FullName)
                .ToList();

            ViewBag.TotalCount = db.Receptionists.Count();
            ViewBag.ActiveCount = db.Receptionists.Count(r => r.IsActive);
            ViewBag.InactiveCount = db.Receptionists.Count(r => !r.IsActive);

            return View(list);
        }

        public ActionResult Create()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Thêm lễ tân";
            ViewBag.Ctrl = "Receptionist";
            return View(new Receptionist());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(string username, string password, Receptionist receptionist)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Thêm lễ tân";
            ViewBag.Ctrl = "Receptionist";

            if (string.IsNullOrWhiteSpace(username))
                ModelState.AddModelError("username", "Vui lòng nhập tên đăng nhập.");

            if (string.IsNullOrWhiteSpace(password))
                ModelState.AddModelError("password", "Vui lòng nhập mật khẩu.");

            if (string.IsNullOrWhiteSpace(receptionist.FullName))
                ModelState.AddModelError("FullName", "Vui lòng nhập họ tên.");

            if (string.IsNullOrWhiteSpace(receptionist.Phone))
                ModelState.AddModelError("Phone", "Vui lòng nhập số điện thoại.");

            if (!string.IsNullOrWhiteSpace(receptionist.Phone) && !IsValidPhone(receptionist.Phone))
                ModelState.AddModelError("Phone", "Số điện thoại không hợp lệ.");

            if (!string.IsNullOrWhiteSpace(username) && db.Users.Any(u => u.Username == username.Trim()))
                ModelState.AddModelError("username", "Tên đăng nhập đã tồn tại.");

            if (!ModelState.IsValid)
                return View(receptionist);

            var user = new User
            {
                Username = username.Trim(),
                PasswordHash = PasswordHelper.Hash(password.Trim()),
                FullName = receptionist.FullName.Trim(),
                Role = "Receptionist",
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            db.Users.Add(user);
            db.SaveChanges();

            receptionist.UserId = user.Id;
            receptionist.FullName = SafeTrim(receptionist.FullName);
            receptionist.Phone = SafeTrim(receptionist.Phone);
            receptionist.Email = SafeTrim(receptionist.Email);
            receptionist.Gender = SafeTrim(receptionist.Gender);
            receptionist.Address = SafeTrim(receptionist.Address);
            receptionist.IsActive = true;
            receptionist.CreatedAt = DateTime.Now;

            db.Receptionists.Add(receptionist);
            db.SaveChanges();

            TempData["Success"] = "Thêm lễ tân thành công.";
            return RedirectToAction("Index");
        }

        public ActionResult Edit(int id)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var receptionist = db.Receptionists.Find(id);
            if (receptionist == null) return HttpNotFound();

            ViewBag.Title = "Cập nhật lễ tân";
            ViewBag.Ctrl = "Receptionist";
            return View(receptionist);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Receptionist receptionist)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Title = "Cập nhật lễ tân";
            ViewBag.Ctrl = "Receptionist";

            var entity = db.Receptionists.Find(receptionist.Id);
            if (entity == null) return HttpNotFound();

            if (string.IsNullOrWhiteSpace(receptionist.FullName))
                ModelState.AddModelError("FullName", "Vui lòng nhập họ tên.");

            if (string.IsNullOrWhiteSpace(receptionist.Phone))
                ModelState.AddModelError("Phone", "Vui lòng nhập số điện thoại.");

            if (!string.IsNullOrWhiteSpace(receptionist.Phone) && !IsValidPhone(receptionist.Phone))
                ModelState.AddModelError("Phone", "Số điện thoại không hợp lệ.");

            if (!ModelState.IsValid)
                return View(receptionist);

            entity.FullName = SafeTrim(receptionist.FullName);
            entity.Phone = SafeTrim(receptionist.Phone);
            entity.Email = SafeTrim(receptionist.Email);
            entity.Gender = SafeTrim(receptionist.Gender);
            entity.DateOfBirth = receptionist.DateOfBirth;
            entity.Address = SafeTrim(receptionist.Address);
            entity.IsActive = receptionist.IsActive;

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
            TempData["Success"] = "Cập nhật lễ tân thành công.";
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

        private string SafeTrim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}