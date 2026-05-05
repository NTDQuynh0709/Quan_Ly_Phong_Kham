

🏥 HỆ THỐNG QUẢN LÝ PHÒNG KHÁM (CLINIC MANAGEMENT SYSTEM)

 📌 1. Giới thiệu

Hệ thống quản lý phòng khám là một ứng dụng web được xây dựng nhằm hỗ trợ các phòng khám trong việc quản lý hoạt động khám chữa bệnh một cách hiệu quả, chính xác và khoa học.

Trong thực tế, nhiều phòng khám vẫn sử dụng phương pháp quản lý thủ công hoặc lưu trữ dữ liệu rời rạc, dẫn đến khó khăn trong việc tra cứu, cập nhật và đảm bảo tính chính xác của thông tin. Hệ thống này được xây dựng nhằm giải quyết các vấn đề trên thông qua việc số hóa toàn bộ quy trình vận hành.

---

🎯 2. Mục tiêu hệ thống

* Tin học hóa quy trình quản lý phòng khám
* Giảm thiểu sai sót trong lưu trữ và xử lý dữ liệu
* Tăng tốc độ tra cứu và cập nhật thông tin
* Hỗ trợ quản lý tập trung và hiệu quả
* Nâng cao trải nghiệm cho bệnh nhân và nhân viên

---

👥 3. Đối tượng sử dụng

| Vai trò                   | Mô tả                                      |
| ------------------------- | ------------------------------------------ |
| **Admin**                 | Quản lý toàn bộ hệ thống                   |
| **Doctor**                | Thực hiện khám bệnh và quản lý bệnh án     |
| **Receptionist**          | Tiếp nhận bệnh nhân và điều phối lịch khám |
| **Patient (Public User)** | Đặt lịch khám và tra cứu thông tin         |

---

⚙️ 4. Công nghệ sử dụng

| Thành phần      | Công nghệ                        |
| --------------- | -------------------------------- |
| Backend         | ASP.NET MVC, C#                  |
| Frontend        | HTML, CSS, JavaScript, Bootstrap |
| Database        | SQL Server                       |
| IDE             | Visual Studio                    |
| Version Control | GitHub                           |

---

🚀 5. Chức năng hệ thống

🔹 5.1 Phân hệ Quản trị viên (Admin)

* Quản lý tài khoản người dùng (CRUD)
* Phân quyền theo vai trò (Admin, Doctor, Receptionist)
* Quản lý chuyên khoa
* Quản lý bác sĩ và nhân viên
* Quản lý bệnh nhân
* Theo dõi lịch khám
* Xem và kiểm tra hồ sơ bệnh án
* Thống kê hoạt động phòng khám

---

🔹 5.2 Phân hệ Bác sĩ (Doctor)

* Xem lịch khám được phân công
* Xem thông tin bệnh nhân
* Thực hiện khám bệnh
* Nhập và lưu hồ sơ bệnh án
* Cập nhật chẩn đoán và phương án điều trị
* Tra cứu lịch sử khám bệnh

---

🔹 5.3 Phân hệ Lễ tân (Receptionist)

* Tiếp nhận bệnh nhân tại quầy
* Tạo mới hồ sơ bệnh nhân
* Tìm kiếm và cập nhật thông tin bệnh nhân
* Tạo lịch khám
* Xác nhận lịch khám online
* Hủy hoặc chỉnh sửa lịch khám
* Xem danh sách lịch khám theo ngày

---

🔹 5.4 Phân hệ Bệnh nhân (Public Portal)

* Xem thông tin phòng khám
* Xem danh sách bác sĩ và chuyên khoa
* Đặt lịch khám trực tuyến
* Lựa chọn bác sĩ, ngày và giờ khám
* Tra cứu lịch hẹn
* Xem thông tin liên hệ

---

📊 6. Quy trình nghiệp vụ chính

1. Bệnh nhân đặt lịch khám online
2. Lễ tân xác nhận lịch khám
3. Bác sĩ tiếp nhận và khám bệnh
4. Bác sĩ lập hồ sơ bệnh án
5. Admin theo dõi và quản lý toàn bộ dữ liệu

---

📂 7. Cấu trúc thư mục dự án

```
/ClinicManagementSystem
│── Controllers/        # Xử lý logic nghiệp vụ
│── Models/             # Định nghĩa dữ liệu
│── Views/              # Giao diện người dùng
│── wwwroot/            # Tài nguyên tĩnh (CSS, JS, Images)
│── Scripts/            # File script hỗ trợ
│── Content/            # Style và layout
│── App_Data/           # Dữ liệu hệ thống
│── Database/           # File SQL
│── README.md
```

---

🛠️ 8. Hướng dẫn cài đặt

🔹 Bước 1: Clone project

```bash
git clone https://github.com/your-repo/clinic-management.git
```

---

🔹 Bước 2: Mở project

* Mở bằng **Visual Studio**

---

🔹 Bước 3: Cấu hình Database

Chỉnh sửa file `Web.config`:

```xml
<connectionStrings>
  <add name="DefaultConnection"
       connectionString="Server=.;Database=ClinicDB;Trusted_Connection=True;"
       providerName="System.Data.SqlClient" />
</connectionStrings>
```

---

🔹 Bước 4: Khởi tạo Database

* Mở SQL Server
* Chạy file trong thư mục `/Database`

---

🔹 Bước 5: Chạy hệ thống

* Nhấn **F5** trong Visual Studio

---

🧪 9. Tài khoản demo

| Vai trò      | Username     | Password |
| ------------ | ------------ | -------- |
| Admin        | admin        | 123456   |
| Doctor       | doctor       | 123456   |
| Receptionist | receptionist | 123456   |

