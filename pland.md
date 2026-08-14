App Windows đổi tên file hàng loạt

Tóm tắt

Xây dựng ứng dụng desktop C# bằng .NET 8 và WPF. Người dùng chọn thư mục, sắp xếp file, tạo mẫu tên gồm tên chính/ngày/số thứ tự, xem trước toàn bộ kết quả rồi mới đổi tên.

Ví dụ:

Mẫu: {name} ({date:MMM d, yyyy})

Tên chính: Re-ID Hoa

Ngày bắt đầu: 14/08/2026

Bước tăng: 1 ngày

Kết quả: Re-ID Hoa (Aug 14, 2026), Re-ID Hoa (Aug 15, 2026)...

Khi hết tháng hoặc năm, lịch tự chuyển chính xác, kể cả năm nhuận.

Thay đổi chính

Giao diện và lựa chọn file

Chọn một thư mục và có tùy chọn quét cả thư mục con.

Mặc định hiển thị mọi file; hỗ trợ lọc theo một hoặc nhiều phần mở rộng.

Cho phép chọn/bỏ chọn từng file.

Sắp xếp tự nhiên theo tên, ngày tạo, ngày sửa, kích thước hoặc đường dẫn.

Cho phép kéo thả để điều chỉnh thứ tự thủ công.

Hiển thị tên cũ, tên mới, đường dẫn, trạng thái và cảnh báo.

Giữ nguyên phần mở rộng của từng file.

Trình tạo mẫu tên

Cung cấp các ô nhập trực quan và một ô sửa mẫu nâng cao.

Hỗ trợ tối thiểu:{name}: tên chính do người dùng nhập.

{date:format}: ngày theo định dạng .NET, ví dụ MMM d, yyyy, dd-MM-yyyy.

{n} hoặc {n:000}: số thứ tự và số chữ số.

Văn bản cố định, dấu cách, ngoặc và dấu gạch tùy ý.



Có ngày bắt đầu, bước tăng theo số ngày nguyên dương, số thứ tự bắt đầu và bước tăng số.

Mặc định tên tháng bằng tiếng Anh để tạo đúng Aug; cho phép chọn ngôn ngữ Việt/Anh.

Cập nhật bản xem trước ngay khi mẫu, thứ tự hoặc dữ liệu đầu vào thay đổi.

Đổi tên an toàn và lịch sử

Kiểm tra trước tên rỗng, ký tự cấm của Windows, đường dẫn quá dài, tên trùng, file không còn tồn tại và quyền ghi.

Nếu có bất kỳ xung đột nào, chặn toàn bộ thao tác và đánh dấu rõ file liên quan.

Đổi tên theo hai giai đoạn qua tên tạm duy nhất để xử lý an toàn trường hợp các tên hoán đổi cho nhau.

Nếu lỗi giữa chừng, tự khôi phục các file đã đổi trong phiên đó và báo kết quả cụ thể.

Lưu nhiều phiên lịch sử dưới dạng JSON trong thư mục dữ liệu ứng dụng của người dùng, gồm thời gian và ánh xạ đường dẫn cũ–mới.

Màn hình lịch sử cho phép chọn một phiên để hoàn tác; chỉ hoàn tác khi trạng thái file hiện tại vẫn an toàn, không ghi đè file khác.

Lịch sử chỉ lưu đường dẫn và kết quả thao tác, không sao chép nội dung file.

Kiến trúc và giao diện nội bộ

Dùng WPF theo MVVM, tách giao diện, dựng tên, kiểm tra hợp lệ, thực thi đổi tên và lưu lịch sử.

Các kiểu dữ liệu chính:RenameItem: đường dẫn cũ/mới, thứ tự, trạng thái chọn và lỗi.

RenameTemplateOptions: mẫu, tên chính, ngày bắt đầu, bước ngày, số bắt đầu, bước số và ngôn ngữ.

RenameSession: mã phiên, thời gian và danh sách ánh xạ cũ–mới.



Dịch vụ chính:TemplateEngine dựng và kiểm tra mẫu.

RenamePlanner tạo bản xem trước và phát hiện xung đột.

RenameExecutor đổi tên hai giai đoạn và phục hồi khi lỗi.

HistoryStore lưu, đọc và hoàn tác nhiều phiên.



Không cần cơ sở dữ liệu hoặc kết nối Internet.

Kiểm thử và tiêu chí nghiệm thu

Kiểm thử định dạng ví dụ Re-ID Hoa (Aug 14, 2026).

Kiểm thử chuyển ngày qua cuối tháng, cuối năm và ngày 29/2.

Kiểm thử bước ngày lớn hơn 1 và số thứ tự có đệm số 0.

Kiểm thử tên tiếng Việt có dấu, khoảng trắng và nhiều loại phần mở rộng.

Kiểm thử sắp xếp tự nhiên, kéo thả, lọc phần mở rộng và quét thư mục con.

Xác nhận app chặn tên trùng, ký tự cấm và đường dẫn đích đã tồn tại.

Kiểm thử đổi tên hoán đổi, lỗi giữa chừng và tự khôi phục.

Kiểm thử lưu nhiều phiên lịch sử, khởi động lại app và hoàn tác một phiên hợp lệ.

Build bản Release và đóng gói thành ứng dụng Windows x64 tự chứa, chạy được khi máy chưa cài .NET.

Giả định đã chốt

Chỉ đổi tên file, không đổi tên thư mục và không di chuyển file.

Phần mở rộng luôn được giữ nguyên và không được nhập trong mẫu.

Thứ tự trong bản xem trước chính là thứ tự dùng để tính ngày và số.

Bước ngày là số ngày nguyên dương; lịch của .NET tự xử lý chuyển tháng, năm và năm nhuận.

Trước khi thực hiện, app yêu cầu xác nhận số lượng file nhưng không hỏi lại riêng từng file.

