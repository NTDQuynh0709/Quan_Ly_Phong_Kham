using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using web.Helpers;
using web.Models;

namespace web.Controllers
{
    public class AccountController : Controller
    {
        private readonly ClinicDbEntities1 db = new ClinicDbEntities1();

        [HttpGet]
        public ActionResult Login()
        {
            if (Session["UserId"] != null && Session["UserRole"] != null)
            {
                return RedirectByRole(Session["UserRole"].ToString());
            }

            ViewBag.Title = "Đăng nhập";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string username, string password)
        {
            ViewBag.Title = "Đăng nhập";

            if (string.IsNullOrWhiteSpace(username))
            {
                ModelState.AddModelError("username", "Vui lòng nhập tên đăng nhập.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("password", "Vui lòng nhập mật khẩu.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.InputUsername = username;
                return View();
            }

            username = username.Trim();
            password = password.Trim();

            var user = db.Users.FirstOrDefault(u => u.Username == username && u.IsActive);

            if (user == null || !PasswordHelper.Verify(password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Sai tên đăng nhập, mật khẩu hoặc tài khoản đã bị khóa.");
                ViewBag.InputUsername = username;
                return View();
            }

            // Auto-upgrade: nếu mật khẩu cũ đang là plaintext, hash lại và lưu
            if (!PasswordHelper.IsHashed(user.PasswordHash))
            {
                user.PasswordHash = PasswordHelper.Hash(password);
                db.SaveChanges();
            }

            Session["UserId"] = user.Id;
            Session["UserRole"] = user.Role;
            Session["UserName"] = user.FullName;

            return RedirectByRole(user.Role);
        }

        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login");
        }

        private ActionResult RedirectByRole(string role)
        {
            switch ((role ?? "").Trim())
            {
                case "Admin":
                    return RedirectToAction("Index", "Admin");
                case "Receptionist":
                    return RedirectToAction("Index", "Appointment");
                case "Doctor":
                    return RedirectToAction("MySchedule", "DoctorPortal");
                default:
                    return RedirectToAction("Index", "Home");
            }
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

namespace web.Helpers
{
    public static class PasswordHelper
    {
        public static string Hash(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";

            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(plain));
                var sb = new StringBuilder(64);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static bool IsHashed(string stored)
        {
            if (string.IsNullOrEmpty(stored) || stored.Length != 64) return false;
            foreach (char c in stored)
            {
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!ok) return false;
            }
            return true;
        }

        public static bool Verify(string input, string stored)
        {
            if (string.IsNullOrEmpty(stored)) return false;
            if (IsHashed(stored)) return Hash(input) == stored;
            return input == stored;
        }
    }

    public static class StatusHelper
    {
        public static string StatusText(string status)
        {
            switch (status)
            {
                case "Pending": return "Chờ xác nhận";
                case "Confirmed": return "Đã xác nhận";
                case "Done": return "Hoàn thành";
                case "Cancelled": return "Đã hủy";
                default: return status ?? "";
            }
        }

        public static string StatusClass(string status)
        {
            switch (status)
            {
                case "Confirmed": return "bs-confirmed";
                case "Done": return "bs-done";
                case "Cancelled": return "bs-cancelled";
                default: return "bs-pending";
            }
        }
    }

    public static class DoctorScheduleHelper
    {
        private static readonly System.Text.RegularExpressions.Regex RangeRx =
            new System.Text.RegularExpressions.Regex(@"Thứ\s*(\d)\s*-\s*Thứ\s*(\d)");
        private static readonly System.Text.RegularExpressions.Regex SingleRx =
            new System.Text.RegularExpressions.Regex(@"Thứ\s*(\d)");

        // Format hỗ trợ (ngăn cách nhiều đoạn bằng dấu ';'):
        //   "Thứ 2 - Thứ 6 | 07:30 - 17:00"                            - chỉ T2-T6
        //   "Thứ 2 - Thứ 6 | 07:30 - 17:00; Thứ 7 | 07:30 - 12:00"     - T2-T6 + T7 sáng
        // Chủ nhật luôn trả về false (phòng khám nghỉ CN).
        public static bool IsDoctorWorkingOnDay(string workSchedule, System.DateTime date)
        {
            if (date.DayOfWeek == System.DayOfWeek.Sunday) return false;
            if (string.IsNullOrWhiteSpace(workSchedule)) return true;

            int viDay = (int)date.DayOfWeek + 1; // Mon=2 ... Sat=7

            var workingDays = new System.Collections.Generic.HashSet<int>();
            var segments = workSchedule.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (var raw in segments)
            {
                var seg = raw.Trim();
                var rMatch = RangeRx.Match(seg);
                if (rMatch.Success)
                {
                    int s = int.Parse(rMatch.Groups[1].Value);
                    int e = int.Parse(rMatch.Groups[2].Value);
                    for (int i = s; i <= e; i++) workingDays.Add(i);
                }
                else
                {
                    var sMatch = SingleRx.Match(seg);
                    if (sMatch.Success)
                    {
                        workingDays.Add(int.Parse(sMatch.Groups[1].Value));
                    }
                }
            }

            if (workingDays.Count == 0) return true;
            return workingDays.Contains(viDay);
        }

        public static string DayOfWeekViet(System.DateTime date)
        {
            switch (date.DayOfWeek)
            {
                case System.DayOfWeek.Monday: return "Thứ 2";
                case System.DayOfWeek.Tuesday: return "Thứ 3";
                case System.DayOfWeek.Wednesday: return "Thứ 4";
                case System.DayOfWeek.Thursday: return "Thứ 5";
                case System.DayOfWeek.Friday: return "Thứ 6";
                case System.DayOfWeek.Saturday: return "Thứ 7";
                default: return "Chủ nhật";
            }
        }
    }

    // Sinh avatar SVG (data-URI) từ tên bác sĩ — dùng thay cho ảnh thật để đảm bảo
    // toàn bộ bác sĩ hiển thị đồng nhất về phong cách, không phụ thuộc file ảnh upload.
    public static class DoctorAvatarHelper
    {
        private static readonly string[][] GradientPairs = new[]
        {
            new[] { "#3b82f6", "#1e3a8a" },
            new[] { "#06b6d4", "#155e75" },
            new[] { "#10b981", "#065f46" },
            new[] { "#8b5cf6", "#5b21b6" },
            new[] { "#ec4899", "#9d174d" },
            new[] { "#f43f5e", "#881337" },
            new[] { "#f97316", "#9a3412" },
            new[] { "#6366f1", "#3730a3" },
            new[] { "#14b8a6", "#134e4a" },
            new[] { "#0ea5e9", "#0c4a6e" }
        };

        public static string GetInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "?";
            var parts = fullName.Trim().Split(
                new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return FirstLetter(parts[0]).ToString().ToUpperInvariant();
            var first = FirstLetter(parts[0]);
            var last = FirstLetter(parts[parts.Length - 1]);
            return (first.ToString() + last.ToString()).ToUpperInvariant();
        }

        private static char FirstLetter(string word)
        {
            if (string.IsNullOrEmpty(word)) return '?';
            var normalized = word.Normalize(System.Text.NormalizationForm.FormD);
            foreach (var ch in normalized)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    if (ch == 'đ') return 'd';
                    if (ch == 'Đ') return 'D';
                    return ch;
                }
            }
            return '?';
        }

        private static string[] GetGradient(string seed)
        {
            if (string.IsNullOrEmpty(seed)) seed = "default";
            int hash = 0;
            foreach (var c in seed) hash = (hash * 31 + c) & int.MaxValue;
            return GradientPairs[hash % GradientPairs.Length];
        }

        public static string GetAvatarDataUri(string fullName)
        {
            var initials = GetInitials(fullName);
            var grad = GetGradient(fullName ?? "");
            var gradId = "g" + System.Math.Abs((fullName ?? "x").GetHashCode())
                .ToString(System.Globalization.CultureInfo.InvariantCulture);

            var svg =
                "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 200'>" +
                "<defs><linearGradient id='" + gradId + "' x1='0' y1='0' x2='1' y2='1'>" +
                "<stop offset='0%' stop-color='" + grad[0] + "'/>" +
                "<stop offset='100%' stop-color='" + grad[1] + "'/>" +
                "</linearGradient></defs>" +
                "<rect width='200' height='200' fill='url(#" + gradId + ")'/>" +
                "<text x='100' y='118' font-family='Segoe UI, Roboto, Inter, system-ui, sans-serif' " +
                "font-size='86' font-weight='700' fill='#fff' text-anchor='middle' letter-spacing='1'>" +
                System.Web.HttpUtility.HtmlEncode(initials) +
                "</text></svg>";

            var b64 = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
            return "data:image/svg+xml;base64," + b64;
        }

        // Ưu tiên ảnh thật từ ImageUrl (file local ~/Content/images/doctors/...).
        // Kiểm tra file có thực sự tồn tại trên disk — nếu không, trả thẳng SVG chữ cái đầu.
        // Thêm cache-buster theo mtime của file để mỗi lần thay ảnh URL đổi, browser tải lại.
        public static string GetAvatarUrl(string imageUrl, string fullName)
        {
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                var trimmed = imageUrl.Trim();
                if (trimmed.StartsWith("~/"))
                {
                    try
                    {
                        var physical = System.Web.Hosting.HostingEnvironment.MapPath(trimmed);
                        if (!string.IsNullOrEmpty(physical) && System.IO.File.Exists(physical))
                        {
                            var absolute = System.Web.VirtualPathUtility.ToAbsolute(trimmed);
                            var mtime = System.IO.File.GetLastWriteTimeUtc(physical).Ticks;
                            return absolute + "?v=" + mtime.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                    }
                    catch { /* fall through → dùng initials */ }
                }
                else
                {
                    return trimmed;
                }
            }
            return GetAvatarDataUri(fullName);
        }
    }
}
