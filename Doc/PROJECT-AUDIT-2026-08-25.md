# Báo cáo kiểm tra project Action Wheel

**Ngày kiểm tra:** 25/08/2026  
**Nhánh:** `main` (đồng bộ với `origin/main` tại thời điểm kiểm tra)  
**Phiên bản ứng dụng:** 2.7.0  
**Phạm vi:** kiến trúc, chất lượng mã, build, publish/package, dependency, bảo mật cấu hình và khả năng kiểm thử.

## 1. Kết luận điều hành

Action Wheel là một project desktop có chất lượng khá tốt, kiến trúc rõ ràng và quy trình build/phát hành đáng tin cậy. Project build thành công ở cả Debug và Release, kiểm tra được toàn bộ 141 XAML binding, vượt qua kiểm tra round-trip JSON/XML và tạo thành công gói x64 self-contained. Không phát hiện package NuGet có lỗ hổng đã biết tại thời điểm kiểm tra.

Điểm cần xử lý gấp nhất là trigger chuột dạng tổ hợp. Luồng hiện tại có thể chuyển sự kiện nhấn nút đầu tiên xuống ứng dụng nền nhưng lại nuốt sự kiện nhả tương ứng, khiến một số ứng dụng hiểu rằng nút chuột vẫn đang được giữ. Ngoài ra, restore backup mới chỉ kiểm tra dữ liệu trước khi ghi, chưa đảm bảo rollback khi một bước ghi trung gian thất bại.

**Đánh giá tổng thể: 7,5/10.** Project phù hợp để phát hành, nhưng nên sửa hai vấn đề trên và bổ sung test/CI trước khi mở rộng tính năng.

## 2. Tổng quan kiến trúc

Solution gồm hai assembly chính:

- `ActionWheel.Core/`: business logic thuần `net10.0`, không phụ thuộc WinUI hoặc `user32`. Khu vực này chứa cấu hình, validation, hình học vòng tròn, XML profile, backup và các codec.
- `Action Wheel`: ứng dụng WinUI 3 chạy trên Windows, gồm `Services/`, `Overlay/`, `Settings/` và `ViewModels/`.

Cách tách này tốt vì compiler ngăn business logic vô tình phụ thuộc lớp giao diện. Các trách nhiệm chính cũng được đặt đúng khu vực: hook và Windows integration trong `Services/`, radial menu trong `Overlay/`, UI chỉnh sửa trong `Settings/`, trạng thái trình bày trong `ViewModels/`.

Project có khoảng 62 file C#, 4 file XAML và hơn 11.000 dòng C#. Một số file đã khá lớn, nổi bật là `Overlay/RadialMenu.xaml.cs`, `Settings/SettingsWindow.xaml.cs` và `ViewModels/SettingsViewModel.cs`. Đây chưa phải lỗi, nhưng sẽ làm tăng chi phí review và regression testing khi tiếp tục bổ sung tính năng.

## 3. Kết quả kiểm tra tự động

| Hạng mục | Lệnh | Kết quả |
|---|---|---|
| XAML binding | `.\Build.ps1 -Task VerifyBindings` | Đạt, 141/141 binding resolve |
| Codec round-trip | `.\Build.ps1 -Task VerifyRoundTrip` | Đạt cho `actions.json` và profile XML |
| Debug x64 | `.\Build.ps1` | Đạt, 0 warning, 0 error |
| Release x64 | `.\Build.ps1 -Task Release` | Đạt, 0 warning, 0 error |
| Publish x64 | `.\Build.ps1 -Task Publish -Platform x64` | Đạt, EXE self-contained 283,9 MB |
| Package smoke test | `.\Build.ps1 -Task Package -Platform x64` | Đạt, ZIP và SHA-256 được tạo |
| Package vulnerability | `dotnet list ... --vulnerable --include-transitive` | Không phát hiện vulnerability |
| Format | `dotnet format ... --verify-no-changes` | Không đạt: 6 lỗi whitespace |
| Phân tích mở rộng | `AnalysisMode=All` | Build đạt nhưng báo 296 cảnh báo analyzer |

296 cảnh báo ở chế độ `AnalysisMode=All` không tương đương 296 lỗi sản phẩm. Phần lớn là quy tắc API design, globalization, `IDisposable`, broad catch và P/Invoke dành cho thư viện công khai. Tuy vậy, kết quả cho thấy project chưa có chính sách analyzer rõ ràng để phân biệt rule hữu ích với rule không phù hợp cho ứng dụng desktop nội bộ.

## 4. Phát hiện chi tiết

### F-01 — Cao: trigger chuột tổ hợp có thể làm “kẹt” trạng thái nút trong ứng dụng nền

**Vị trí:** `Services/MouseHook.cs`, khu vực xử lý `WM_LBUTTONDOWN`, `WM_RBUTTONDOWN` và hai sự kiện `BUTTONUP` (khoảng dòng 279–333).

Ví dụ với `HoldRightPressLeft`:

1. Người dùng nhấn chuột phải trước.
2. Sự kiện `WM_RBUTTONDOWN` không khớp nhánh kích hoạt ngay và được chuyển tiếp đến ứng dụng nền.
3. Người dùng nhấn chuột trái; chord được nhận diện và mã đặt `_swallowLeftUp` cùng `_swallowRightUp` thành `true`.
4. Cả hai sự kiện nhả sau đó bị hook nuốt.

Kết quả là ứng dụng nền có thể nhận `RBUTTONDOWN` nhưng không nhận `RBUTTONUP`. Với trigger ngược lại, chuột trái gặp vấn đề tương tự. Ảnh hưởng có thể gồm trạng thái drag/capture bất thường, menu chuột phải không nhất quán hoặc ứng dụng tin rằng nút vẫn đang giữ.

**Khuyến nghị:** tách logic chord thành state machine có thể kiểm thử. Xác định rõ cặp down/up nào được chuyển tiếp và bảo đảm mỗi down được chuyển tiếp luôn có một up cân bằng. Bổ sung test cho cả hai thứ tự, timeout, di chuyển vượt ngưỡng và trường hợp chỉ nhấn nút đầu tiên.

### F-02 — Trung bình: restore backup có thể áp dụng một phần dù trả về thất bại

**Vị trí:** `ActionWheel.Core/ConfigBackup.cs`, khoảng dòng 263–295.

Archive được giải nén vào staging và validate trước khi ghi, đây là thiết kế tốt. Tuy nhiên giai đoạn commit ghi lần lượt:

1. `actions.json`;
2. `preferences.json`;
3. `active-profile.txt`;
4. từng profile XML;
5. các icon.

Nếu bước thứ hai hoặc các bước sau thất bại, những file đã ghi trước đó không được phục hồi. Hàm trả về `false`, nhưng trạng thái thực tế đã thay đổi một phần. Trường hợp này có thể xảy ra do lỗi quyền, antivirus, hết dung lượng hoặc tên profile không hợp lệ trên Windows.

**Khuyến nghị:** tạo snapshot/backup của toàn bộ đích trước khi commit, ghi vào staging cùng volume rồi swap theo nhóm, hoặc triển khai rollback best-effort khi bất kỳ bước nào thất bại. Thông báo lỗi nên nói rõ nếu rollback không hoàn tất.

### F-03 — Trung bình: phạm vi kiểm thử còn quá hẹp và chưa có CI workflow

`Tools/RoundTripCheck` chỉ kiểm tra hai đường round-trip codec. Chưa có test tự động cho:

- `ShortcutKeys` và validation action;
- `RingGeometry` trên nhiều DPI/monitor boundary;
- `ConfigBackup`, zip-slip, rollback và archive lỗi;
- `StartupCommand`, màu, SVG tint và profile name;
- state machine của trigger chuột;
- lựa chọn profile và persistence.

Thư mục `.github/` chỉ có issue/PR template, không có workflow. Điều này không đồng nhất với mô tả trong `Build.ps1` và `CONTRIBUTING.md` rằng các lệnh build cũng chạy trong CI.

**Khuyến nghị:** thêm project xUnit cho `ActionWheel.Core`; refactor phần quyết định sự kiện hook thành logic thuần để test không cần desktop session; thêm GitHub Actions chạy restore, binding verification, round-trip và build. Publish/package có thể chạy ở workflow release hoặc nightly để tránh thời gian CI quá dài.

### F-04 — Thấp: metadata và tài liệu phiên bản chưa đồng bộ

- `Action Wheel.csproj` và `ActionWheel-Setup.iss` dùng phiên bản 2.7.0.
- `Package.appxmanifest` vẫn khai báo `2.4.0.0`.
- `CONTRIBUTING.md` vẫn liệt kê Windows App SDK 1.8, trong khi project tham chiếu 2.3.1.
- Copyright trong project vẫn ghi năm 2025.

Ứng dụng hiện chạy unpackaged nên manifest cũ chưa làm hỏng executable đã publish, nhưng dễ gây sai metadata nếu bật lại MSIX hoặc dùng manifest cho pipeline khác.

**Khuyến nghị:** đặt version ở một nguồn chung hoặc thêm bước kiểm tra consistency vào `Build.ps1`.

### F-05 — Thấp: thiếu baseline formatter/analyzer

`dotnet format --verify-no-changes` phát hiện whitespace thừa trong `Overlay/RadialMenu.xaml.cs` và cách xuống dòng chưa chuẩn trong `Services/ActionDispatcher.cs`. Project không có `.editorconfig` hoặc cấu hình analyzer riêng.

**Khuyến nghị:** thêm `.editorconfig`, bật formatter trong CI và chọn một tập rule nhỏ có giá trị thực tế. Không nên bật toàn bộ `AnalysisMode=All` rồi sửa máy móc 296 cảnh báo; nên ưu tiên lỗi correctness, resource lifetime, interop return code và globalization tại nơi dữ liệu được lưu/so sánh.

### F-06 — Thấp: restore ZIP chưa giới hạn tài nguyên

`ConfigBackup.TryImport` kiểm tra zip-slip bằng cách chuẩn hóa destination path, nhưng giải nén entry mà không giới hạn số lượng hoặc tổng kích thước. Một backup không tin cậy có thể làm đầy thư mục tạm hoặc ổ đĩa trước khi validation hoàn tất.

**Khuyến nghị:** đặt giới hạn số entry, kích thước từng entry và tổng dung lượng giải nén; chỉ chấp nhận các entry name/extension nằm trong định dạng backup đã định nghĩa.

### F-07 — Thông tin: dependency có bản mới hơn

Không phát hiện package có vulnerability đã biết. NuGet báo `Microsoft.WindowsAppSDK` 2.4.0 mới hơn bản 2.3.1 đang dùng; một số dependency bắc cầu cũng có phiên bản mới hơn.

Không nên nâng cấp tự động. Project phụ thuộc mạnh vào WinUI transparency, native window style và single-file publish, do đó cần chạy lại package smoke test và kiểm tra giao diện thủ công sau khi nâng cấp.

## 5. Điểm mạnh

- Phân tầng `ActionWheel.Core`/WinUI được compiler thực thi, không chỉ là quy ước thư mục.
- Config được validate trước khi sử dụng; file lỗi không bị ghi đè bằng defaults.
- Ghi file dùng temporary file, flush-to-disk và backup.
- XML import chặn DTD và resolver, giảm nguy cơ XXE.
- Backup import có kiểm tra zip-slip trước khi extract.
- Hook callback chặn exception vượt qua reverse P/Invoke, tránh process fail-fast.
- Các vùng Win32, transparency, focus và performance có comment giải thích nguyên nhân rất tốt.
- Build script thống nhất restore, build, binding check, codec check, publish, package và smoke test.
- Repository sạch, generated output được ignore đúng cách.

## 6. Thứ tự xử lý đề xuất

### Ưu tiên 1 — trước release tiếp theo

1. Sửa và kiểm thử trigger `HoldRightPressLeft`/`HoldLeftPressRight`.
2. Đồng bộ version manifest, dependency requirement và copyright.
3. Thêm regression test cho trigger state machine.

### Ưu tiên 2 — tăng độ tin cậy dữ liệu

1. Thiết kế rollback hoặc transaction cho full backup restore.
2. Giới hạn tài nguyên khi giải nén backup.
3. Thêm unit test cho config/profile/backup và các utility trong Core.

### Ưu tiên 3 — cải thiện quy trình

1. Thêm GitHub Actions.
2. Thêm `.editorconfig` và formatter check.
3. Chọn analyzer baseline phù hợp.
4. Xem xét tách các file UI/code-behind lớn theo trách nhiệm khi tiếp tục phát triển.

## 7. Giới hạn của lần kiểm tra

Lần kiểm tra này chạy build, publish và package smoke test trên x64. Chưa build/publish x86 hoặc ARM64; chưa chạy Inno Setup installer; chưa kiểm thử tương tác thủ công trên nhiều monitor/DPI, Windows 10, Windows 11, màn hình khóa hoặc ứng dụng elevated. Các hành vi cần xác nhận thủ công gồm mở vòng bằng mọi trigger, Esc/click-outside dismissal, kéo chọn, hold action, group menu, tray, startup, profile switching và persistence sau khi khởi động lại.

Không có mã nguồn ứng dụng nào được thay đổi trong quá trình đánh giá; file này là tài liệu kết quả kiểm tra.
