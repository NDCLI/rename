# Batch File Renamer - Ứng Dụng Đổi Tên Tệp Hàng Loạt (.NET 8 WPF)

## 📌 Giới Thiệu
**Batch File Renamer** là ứng dụng desktop Windows hiện đại được xây dựng bằng C# .NET 8 và WPF theo kiến trúc MVVM. Ứng dụng cung cấp giải pháp đổi tên tệp hàng loạt trực quan, linh hoạt và đảm bảo an toàn dữ liệu tuyệt đối (2-Phase Transactional Renaming & Lịch sử Hoàn tác Undo).

---

## 🚀 Các Tính Năng Nổi Bật

### 1. 📂 Giao Diện & Lựa Chọn Tệp
- **Chọn thư mục linh hoạt**: Hỗ trợ duyệt thư mục và tùy chọn quét đệ quy toàn bộ thư mục con (`Include Subdirectories`).
- **Bộ lọc định dạng**: Lọc theo một hoặc nhiều phần mở rộng (ví dụ: `.jpg, .png, .mp4, .docx` hoặc để trống để quét tất cả).
- **Chọn / Bỏ chọn**: Cho phép bật/tắt từng tệp hoặc chọn tất cả / bỏ chọn tất cả / đảo vùng chọn.
- **Sắp xếp tự nhiên (Natural Sorting)**: Sắp xếp theo Tên (`StrCmpLogicalW` - file1, file2, file10), Ngày tạo, Ngày sửa, Dung lượng, hoặc Đường dẫn.
- **Kéo thả điều chỉnh thứ tự (Drag & Drop)**: Kéo thả trực tiếp các dòng trong danh sách để thay đổi thứ tự thủ công; hệ thống sẽ tự động cập nhật lại số thứ tự và ngày tương ứng theo thời gian thực.
- **Bảo toàn phần mở rộng**: Giữ nguyên định dạng gốc của từng tệp (`.jpg`, `.PNG`, `.docx`...).

### 2. ⚡ Trình Tạo Mẫu Tên (Template Engine) & Xem Trước Tức Thời
- **Các ô nhập trực quan**:
  - **Tên chính (`{name}`)**: Nhập chuỗi tên chính (ví dụ: `Re-ID Hoa`).
  - **Ngày bắt đầu & Bước ngày (`DayStep`)**: Tự động tính toán chuyển tháng (31/08 -> 01/09), chuyển năm (31/12 -> 01/01), và năm nhuận (28/02 -> 29/02/2028).
  - **Ngôn ngữ ngày**: Mặc định Tiếng Anh (`en-US` để tạo `Aug 14, 2026`) hoặc Tiếng Việt (`vi-VN` để tạo `Thg8 14, 2026`).
  - **Số thứ tự (`{n}`) & Bước nhảy số**: Bắt đầu từ số bất kỳ và tăng theo bước tùy chọn.
- **Trình soạn thảo mẫu nâng cao**:
  - Hỗ trợ cú pháp: `{name}`, `{date:format}` (ví dụ `{date:MMM d, yyyy}`, `{date:dd-MM-yyyy}`), `{n:format}` (ví dụ `{n:000}`, `{n:00}`), `{orig}` (tên gốc).
  - Tự do kết hợp văn bản cố định, dấu cách, dấu ngoặc `()`, `[]`, dấu gạch ngang, tiếng Việt có dấu.
  - Các nút chèn nhanh Token (`+ {name}`, `+ {date:MMM d, yyyy}`, `+ {n:000}`...).
  - **Instant Live Preview**: Bảng xem trước cập nhật tên mới và trạng thái ngay khi có bất kỳ thay đổi nào.

### 3. 🛡️ Đổi Tên 2 Giai Đoạn & An Toàn Tuyệt Đối
- **Phát hiện xung đột trước khi thực thi**:
  - Chặn tên rỗng, ký tự cấm của Windows (`\ / : * ? " < > |` và ký tự điều khiển).
  - Chặn các tên thiết bị cấm của Windows (`CON, PRN, AUX, NUL, COM1-9, LPT1-9`).
  - Chặn đường dẫn đích quá dài (> 260 ký tự).
  - Chặn trùng lặp tên trong cùng phiên đổi tên.
  - Chặn ghi đè lên file đã tồn tại trên đĩa (nằm ngoài danh sách batch).
- **Giao dịch 2 giai đoạn (Two-Phase Transactional Renaming)**:
  - **Giai đoạn 1**: Đổi toàn bộ các tệp sang tên tạm duy nhất (`GUID`).
  - **Giai đoạn 2**: Đổi từ tên tạm sang tên đích chính thức.
  - *Xử lý hoàn hảo các trường hợp hoán đổi vòng*: File A -> File B và File B -> File A không bao giờ bị lỗi xung đột!
  - *Rollback tự động*: Nếu phát sinh sự cố ở bất kỳ tệp nào, hệ thống tự động khôi phục 100% các tệp về trạng thái ban đầu.

### 4. 📜 Quản Lý Lịch Sử & Hoàn Tác (Undo)
- Lưu trữ mọi phiên đổi tên dưới dạng JSON tại `%APPDATA%\BatchFileRenamer\history.json`.
- Màn hình Lịch sử cho phép xem lại các phiên, danh sách ánh xạ cũ-mới.
- Nút **"Hoàn tác phiên này"**: Kiểm tra an toàn trước khi khôi phục, đảm bảo không ghi đè nếu file đã bị thay đổi hoặc chiếm chỗ.

---

## 📦 Cài Đặt & Chạy Ứng Dụng

### 1. Bản Đóng Gói Sẵn Dùng (Không Cần Cài .NET)
- File thực thi độc lập tại: `publish\BatchFileRenamer.exe`
- Chỉ cần nhấp đúp `BatchFileRenamer.exe` để chạy trực tiếp trên Windows 10/11 x64.

### 2. Chạy Từ Mã Nguồn (Developer)
```bash
# Di chuyển vào thư mục dự án
cd D:\Box\BatchFileRenamer

# Khôi phục và build
dotnet build BatchFileRenamer.sln

# Chạy ứng dụng
dotnet run --project src/BatchFileRenamer

# Chạy toàn bộ 37 Unit Tests
dotnet test BatchFileRenamer.sln
```

---

## 🏗️ Cấu Trúc Dự Án
```
BatchFileRenamer/
├── BatchFileRenamer.sln
├── README.md
├── src/
│   └── BatchFileRenamer/
│       ├── BatchFileRenamer.csproj
│       ├── App.xaml / App.xaml.cs
│       ├── Models/
│       │   └── RenameModels.cs          # RenameItem, RenameTemplateOptions, RenameSession...
│       ├── Services/
│       │   ├── ITemplateEngine.cs & TemplateEngine.cs
│       │   ├── IRenamePlanner.cs & RenamePlanner.cs
│       │   ├── IRenameExecutor.cs & RenameExecutor.cs
│       │   ├── IHistoryStore.cs & HistoryStore.cs
│       │   └── IFileScannerService.cs & FileScannerService.cs
│       ├── Helpers/
│       │   └── NaturalStringComparer.cs # StrCmpLogicalW P/Invoke + Fallback
│       ├── Converters/
│       │   └── AppConverters.cs
│       ├── ViewModels/
│       │   ├── MvvmBase.cs              # ViewModelBase, RelayCommand, AsyncRelayCommand
│       │   ├── MainViewModel.cs
│       │   └── HistoryViewModel.cs
│       └── Views/
│           ├── MainWindow.xaml / MainWindow.xaml.cs
│           └── HistoryWindow.xaml / HistoryWindow.xaml.cs
├── tests/
│   └── BatchFileRenamer.Tests/
│       ├── TemplateEngineTests.cs       # Kiểm thử định dạng, ngày tháng, năm nhuận, bước nhảy
│       ├── NaturalSortingTests.cs       # Kiểm thử sắp xếp tự nhiên
│       ├── RenamePlannerTests.cs        # Kiểm thử phát hiện xung đột và ký tự cấm
│       ├── RenameExecutorTests.cs       # Kiểm thử 2-phase rename, swap, rollback
│       ├── HistoryStoreTests.cs         # Kiểm thử lưu và nạp lịch sử JSON
│       ├── FileScannerTests.cs          # Kiểm thử quét thư mục con & bộ lọc
│       └── MainViewModelTests.cs        # Kiểm thử điều phối ViewModel & tương tác
└── publish/
    └── BatchFileRenamer.exe             # Ứng dụng tự chứa độc lập x64
```
