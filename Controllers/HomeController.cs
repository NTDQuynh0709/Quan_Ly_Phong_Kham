using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using web.Models;

namespace web.Controllers
{
    public class HomeController : Controller
    {
        private ClinicDbEntities1 db = new ClinicDbEntities1();

        public ActionResult Index()
        {
            ViewBag.Nav = "home";
            ViewBag.DoctorCount = db.Doctors.Count(d => d.IsActive);
            ViewBag.PatientCount = db.Patients.Count();
            ViewBag.DepartmentCount = db.Departments.Count();
            ViewBag.AppointmentCount = db.Appointments.Count(a => a.Status == "Done");

            ViewBag.Departments = db.Departments
                .OrderBy(d => d.DepartmentName)
                .ToList();

            ViewBag.FeaturedDoctors = db.Doctors
                .Include(d => d.Department)
                .Where(d => d.IsActive)
                .OrderByDescending(d => d.YearsOfExperience ?? 0)
                .Take(4)
                .ToList();

            return View();
        }

        public ActionResult Doctors(string q, int? departmentId, string sort = "name")
        {
            ViewBag.Nav = "doctors";

            var query = db.Doctors
                          .Include(d => d.Department)
                          .Where(d => d.IsActive);

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(d =>
                    d.FullName.Contains(q) ||
                    d.Specialty.Contains(q) ||
                    (d.Department != null && d.Department.DepartmentName.Contains(q)));
            }

            if (departmentId.HasValue && departmentId.Value > 0)
            {
                int deptId = departmentId.Value;
                query = query.Where(d => d.DepartmentId == deptId);
            }

            switch ((sort ?? "").Trim().ToLower())
            {
                case "fee":
                    query = query.OrderBy(d => d.ConsultationFee).ThenBy(d => d.FullName);
                    break;
                case "experience":
                    query = query.OrderByDescending(d => d.YearsOfExperience ?? 0).ThenBy(d => d.FullName);
                    break;
                default:
                    query = query.OrderBy(d => d.FullName);
                    break;
            }

            ViewBag.Query = q;
            ViewBag.DepartmentId = departmentId;
            ViewBag.Sort = sort;

            ViewBag.Departments = new SelectList(
                db.Departments.OrderBy(x => x.DepartmentName).ToList(),
                "Id",
                "DepartmentName",
                departmentId
            );

            return View(query.ToList());
        }

        public ActionResult About()
        {
            ViewBag.Nav = "about";
            ViewBag.Title = "Giới thiệu";
            ViewBag.DoctorCount = db.Doctors.Count(d => d.IsActive);
            ViewBag.DepartmentCount = db.Departments.Count();
            ViewBag.PatientCount = db.Patients.Count();
            ViewBag.AppointmentCount = db.Appointments.Count(a => a.Status == "Done");
            ViewBag.Departments = db.Departments.OrderBy(d => d.DepartmentName).ToList();
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Nav = "contact";
            ViewBag.Title = "Liên hệ";
            return View();
        }

        public ActionResult DoctorDetail(int? id)
        {
            ViewBag.Nav = "doctors";

            if (!id.HasValue)
                return RedirectToAction("Doctors");

            var doctor = db.Doctors
                           .Include(d => d.Department)
                           .FirstOrDefault(d => d.Id == id.Value && d.IsActive);

            if (doctor == null)
                return HttpNotFound();

            ViewBag.RelatedDoctors = db.Doctors
                                       .Include(d => d.Department)
                                       .Where(d => d.IsActive
                                                && d.Id != id.Value
                                                && d.DepartmentId == doctor.DepartmentId)
                                       .OrderBy(d => d.FullName)
                                       .Take(3)
                                       .ToList();

            return View(doctor);
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