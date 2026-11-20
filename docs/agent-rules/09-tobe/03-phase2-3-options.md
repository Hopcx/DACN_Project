# Phase 2–3 — Q8 đã chốt và phương án cho Q10–Q14

Trạng thái: **TOÀN BỘ ĐÃ CHỐT (20/11/2026)**: Q8, Q10a, Q11a, Q12, Q13a, Q14 theo phương án (a); các câu con Q8a–f, Q10b, Q11b, Q11c, Q13b, Q13c, Q13d theo **phương án đề xuất** ghi trong từng mục (Q8a: phương án chính — lịch không làm tính 0; Q10b: (a); Q11b: (a); Q13c: (a)). Đọc kèm [01-asis-gap-decisions.md](01-asis-gap-decisions.md) (Q1–Q19) và [02-design-plan-tests-risks.md](02-design-plan-tests-risks.md) §4.5.
Mỗi câu có 2–3 phương án; **(Đề xuất)** là phương án mình khuyên dùng, cân nhắc quy mô đồ án (1 máy chủ, SQL Server, dữ liệu nhỏ), độ an toàn và chi phí làm. Người dùng chọn trước khi lập kế hoạch slice.

Bối cảnh đã có (kiểm trong code):
- Snapshot bài thi/luyện tập v2 (T4–T6) lưu câu, đáp án, điểm, lời giải; **không** lưu chương/tag/media.
- Chấm tất cả-hoặc-không (Q3 — chốt cho các loại câu hiện có). Thi thử không tính điểm lớp (Q7). Chưa xóa dữ liệu tự động (Q15).
- Bộ ghi Excel tự viết (`QuestionWorkbook`, `AccountWorkbook`): **một sheet, mọi ô là chuỗi, tối đa cột Z** — cần mở rộng cho báo cáo.
- `Questions.DocumentPath`: API vẫn nhận/trả chuỗi tự do từ client, không gắn file thật — Phase 2 thay bằng media có kiểm soát.
- `Exams.MaximmumMark` khác nhau giữa các bài; `Exams.ScoreMethodId` có trong schema; Tổng quan giảng viên (P4) hiện tính trung bình mọi lượt.
- FE gửi access token qua header `Authorization: Bearer` (không có cookie phiên cho API) — ảnh hưởng tới cách tải media bằng `<img>`/`<audio>`.

---

## Q8 — So sánh, xếp hạng (ĐÃ CHỐT: trong cùng một lớp)

Phạm vi: xếp hạng học sinh **trong một lớp** (không toàn hệ thống, không nhóm tự do). Q8a–f **ĐÃ CHỐT (20/11/2026) theo cột Đề xuất** (Q8a: phương án chính, lịch không làm tính 0):

| # | Câu hỏi con | Phương án | Đề xuất |
|---|---|---|---|
| Q8a | Điểm dùng để xếp? | (1) Từng lịch thi; (2) Điểm tổng các lịch; (3) Cả hai | **(3)**. Điểm **quy về thang 10** (`điểm / MaximmumMark × 10`) để so được giữa các bài khác thang. Điểm tổng = trung bình trên **mọi lịch thi đã kết thúc của lớp**, lịch học viên không làm tính **0** (tránh lợi thế khi bỏ thi) — hoặc phương án phụ: chỉ tính lịch đã làm kèm cột "số lịch đã làm" |
| Q8b | Nhiều lượt trong một lịch? | Cao nhất / mới nhất / theo `ScoreMethodId` của bài thi | **Theo `ScoreMethodId` của bài thi** nếu đang được cấu hình (khớp ý người ra đề); nếu chưa dùng thì **cao nhất**. Khi làm slice, đồng bộ cách tính với Tổng quan giảng viên (P4) để hai màn không lệch nhau. Lượt bị hủy bỏ qua |
| Q8c | Dữ liệu nào được tính? | Chỉ bài thi / thêm Luyện tập–Thi thử | **Chỉ bài thi** — đúng Q7 |
| Q8d | Ai xem gì? | — | **Giảng viên của lớp + Admin**: cả bảng (kể cả nhãn Top/Bottom). **Học viên**: chỉ điểm và hạng của mình + sĩ số có điểm; **không** thấy tên/điểm bạn khác, không thấy danh sách Top/Bottom. Hạng của học viên chỉ hiện khi lớp có **≥ 5 học viên có điểm** (lớp nhỏ thì hạng gần như lộ điểm bạn) |
| Q8e | Hòa điểm? | Đồng hạng 1-1-3 / ai nhanh hơn xếp trên | **Đồng hạng 1-1-3** |
| Q8f | Khi nào hiện? | Ngay khi có điểm / sau khi lịch kết thúc và cho xem kết quả | **Sau khi lịch kết thúc và `AllowViewResult`** (cùng điều kiện mở review) |

---

## Q10 — Chấm câu điền ngắn và các loại câu mới (Phase 2)

**Q10a — Điền ngắn:** ĐÃ CHỐT (20/11/2026): phương án (a).

| Phương án | Mô tả | Ưu | Nhược |
|---|---|---|---|
| **(a) Tự động, danh sách đáp án chấp nhận (Đề xuất)** | 1–10 đáp án chấp nhận mỗi câu; so sau chuẩn hóa: trim, gộp khoảng trắng, không phân biệt hoa/thường; tùy chọn theo câu "bỏ qua dấu tiếng Việt" và "so số" (`0,5` = `0.5`, sai số tùy chọn). Chấm ngay, đưa vào snapshot | Đơn giản, dùng được cho Luyện tập/Thi thử/Kiểm tra từng câu | Viết đúng ý khác chữ có thể bị chấm sai |
| (b) Tự động + giảng viên duyệt lại | Thêm hàng chờ để giảng viên sửa đúng/sai, tính lại điểm | Công bằng hơn | Đổi điểm đã công bố, ảnh hưởng xếp hạng, cần audit/thông báo |
| (c) So gần đúng (Levenshtein/regex) | Chấp nhận sai chính tả nhẹ | Bớt chấm oan | Khó đoán, dễ chấm đúng nhầm |

**Q10b — Cách chấm cho các loại câu mới** (Q3 chỉ chốt cho loại hiện có — đây là quyết định mới). ĐÃ CHỐT (20/11/2026): (a).

| Phương án | Mô tả | Đề xuất |
|---|---|---|
| **(a) Tất cả-hoặc-không cho mọi loại mới** | Ghép cặp đúng hết các cặp, Đúng/Sai nhiều mệnh đề đúng hết mới có điểm | ✔ nhất quán Q3, `AttemptScoring` giữ một quy tắc |
| (b) Chấm từng phần cho ghép cặp/nhiều mệnh đề | Điểm = số cặp đúng / tổng | Công bằng hơn nhưng phải đổi `AttemptScoring`, báo cáo, mô tả điểm |

**Đoạn dùng chung (`QuestionGroups`)**: câu con thuộc nhóm; chọn ngẫu nhiên lấy **cả nhóm hoặc không**. Với mã đề có số câu cố định (`Exams.NumberOfQuestions`): một nhóm đếm bằng số câu con; công cụ tạo mã đề chỉ chọn nhóm vừa số câu còn thiếu (báo lỗi nếu không ghép đủ). Luyện tập: số câu yêu cầu có thể vượt nhẹ để không cắt nhóm (báo trước cho học viên).

---

## Q11 — Lưu và phục vụ media (ảnh, âm thanh) (Phase 2)

**Q11a — Nơi lưu:** ĐÃ CHỐT (20/11/2026): phương án (a).

| Phương án | Mô tả | Ưu | Nhược |
|---|---|---|---|
| **(a) Đĩa máy chủ + bảng metadata (Đề xuất)** | Bảng `MediaFiles` (Guid, loại phát hiện từ magic bytes, kích thước, SHA-256, tên gốc, người tải, thời điểm). File ở thư mục cấu hình **ngoài wwwroot**, tên = Guid. Truy cập qua `IMediaStorage` để sau đổi sang object storage | Đơn giản, DB không phình | **Phải sao lưu thư mục media riêng**: thêm vào runbook cutover/backup (DB và thư mục cùng thời điểm) |
| (b) Trong SQL Server (`varbinary(max)`) | File nằm trong DB | Một bản backup đủ, diễn tập cutover hiện có tự bao gồm | DB phình nhanh với audio, backup/restore chậm |
| (c) Object storage (Blob/S3/MinIO) | Dịch vụ ngoài, URL ký tạm | Mở rộng tốt | Cần hạ tầng, khóa truy cập; thừa cho đồ án |

**Q11b — Cách trình duyệt tải media có kiểm quyền** (vì `<img>`/`<audio>` không gửi được header Bearer). ĐÃ CHỐT (20/11/2026): (a).

| Phương án | Mô tả | Đề xuất |
|---|---|---|
| **(a) URL ký tạm** | API trả kèm câu hỏi một URL `GET /web/media/{id}?t=<token>`; token HMAC ký bởi server, gắn `userId + mediaId + hạn 10 phút`, không dùng lại được cho media khác. Server kiểm token ở **mọi** request (kể cả từng request Range của audio) | ✔ hỗ trợ Range/stream audio, không lộ access token vào URL |
| (b) Tải qua axios → blob URL | FE gọi API với Bearer, nhận file, tạo `blob:` URL | Đơn giản, không cần token riêng; nhưng tải trọn file trước khi phát, không Range |
| (c) Cookie riêng cho media | Cookie HttpOnly đường dẫn `/web/media` | Phải xử lý CSRF/SameSite, lệch mô hình xác thực hiện tại |

**Quy tắc chung**: kiểm **magic bytes**, không tin đuôi/MIME client; `Content-Type` lấy theo loại đã phát hiện; header `X-Content-Type-Options: nosniff`, `Cache-Control: private, no-store`, `Content-Disposition: inline`; chỉ người có quyền câu hỏi (2) tải lên; học viên chỉ tải media của câu nằm trong lượt/phiên của mình (kiểm bằng bảng liên kết, xem Q11c).

**Q11c — Liên kết và xóa media** (giữ Q15: chưa xóa tự động). ĐÃ CHỐT (20/11/2026):
- Snapshot **v3** thêm `mediaIds` cho từng câu (và nhóm), kèm bảng `MediaReferences (MediaId, SourceKind, SourceId)` ghi khi chụp snapshot → kiểm quyền và đếm tham chiếu bằng SQL, không quét JSON.
- Media không còn câu nào dùng **không bị xóa tự động** (Q15); Admin có trang/endpoint liệt kê media mồ côi để xóa tay. Media còn tham chiếu từ snapshot không xóa được.

---

## Q12 — Giới hạn định dạng, dung lượng, thời lượng (Phase 2) — ĐÃ CHỐT (20/11/2026): (a)

| Phương án | Ảnh | Âm thanh | Ghi chú |
|---|---|---|---|
| **(a) Mặc định vừa phải, cấu hình được (Đề xuất)** | JPEG, PNG, WebP; ≤ 2 MB; kích thước đọc từ header file ≤ 4096×4096 (chặn "bom giải nén" trước khi trình duyệt giải mã) | MP3, M4A (AAC), OGG; ≤ 10 MB | `appsettings` `Media:*`; **không nhận SVG/GIF động** |
| (b) Chặt | JPEG, PNG; ≤ 1 MB | MP3; ≤ 5 MB | Máy chủ/băng thông yếu |
| (c) Rộng | thêm GIF; ≤ 5 MB | thêm WAV; ≤ 30 MB | Dễ phình lưu trữ |

Lưu ý kỹ thuật:
- **Thời lượng audio**: kiểm chính xác cần bộ đọc file (thêm thư viện). Đề xuất **chỉ giới hạn dung lượng** ở lát đầu (10 MB MP3 ≈ 10 phút ở 128 kbps); kiểm thời lượng để sau nếu cần.
- **EXIF (vị trí GPS trong ảnh)**: xóa EXIF cần thư viện xử lý ảnh (chú ý giấy phép, vd. ImageSharp). Đề xuất: lát đầu **cảnh báo người tải** và để làm sau; hoặc chọn thư viện ngay nếu yêu cầu riêng tư cao.
- **Giới hạn số lần nghe**: đếm ở server không đáng tin khi phục vụ bằng Range (một lần nghe = nhiều request, trình duyệt có thể cache). **Không đề xuất** ở Phase 2; nếu bắt buộc thì phải dùng phiên phát có token dùng một lần.

---

## Q13 — Định nghĩa "yếu", "top", "bottom" (Phase 3)

**Q13a — Phân tích riêng của học viên (3.7)** — theo chương/tag, chỉ học viên đó xem. ĐÃ CHỐT (20/11/2026): phương án (a).

| Phương án | Mô tả | Đề xuất |
|---|---|---|
| **(a) Ngưỡng tỉ lệ đúng, có số câu tối thiểu** | 90 ngày gần nhất, gồm **bài thi + luyện tập + thi thử của chính học viên** (dữ liệu của họ, chỉ họ xem); xếp loại khi có **≥ 5 câu**: **Yếu** < 50%, **Cần cải thiện** 50–< 70%, **Khá** 70–< 85%, **Tốt** ≥ 85%; ngưỡng cấu hình được | ✔ |
| (b) So với trung bình lớp | Yếu khi thấp hơn trung bình lớp ≥ 15 điểm % | Cần dữ liệu lớp đủ lớn |
| (c) Chỉ bài thi | Như (a) bỏ luyện tập/thi thử | Ít dữ liệu |

**Q13b — Báo cáo lớp cho giảng viên/Admin** — **chỉ dữ liệu bài thi** (Q7). ĐÃ CHỐT (20/11/2026):
- Top/bottom: **20%** cao nhất/thấp nhất theo điểm tổng Q8a (làm tròn lên, tối thiểu 1), chỉ khi lớp có **≥ 5 học viên có điểm**; chỉ giảng viên/Admin thấy.
- Theo chương: tỉ lệ đúng của lớp và số học viên "Yếu" — tính trên **bài thi** của lớp.
- Câu hay sai: tỉ lệ đúng < 40% với ≥ 10 lượt trả lời; kèm đáp án nhiễu chọn nhiều nhất.

**Q13c — Giảng viên có được xem dữ liệu tự luyện (Luyện tập/Thi thử) của học viên không?** ĐÃ CHỐT (20/11/2026): (a) Không.
- (a) **Không** (Đề xuất): tự luyện là riêng của học viên, giảng viên chỉ thấy bài thi.
- (b) Có, chỉ số tổng hợp (số phiên, tỉ lệ đúng), không xem từng bài.

**Q13d — Lấy chương/tag của câu khi tổng hợp** (ĐÃ CHỐT 20/11/2026 theo đề xuất): snapshot hiện không lưu chương/tag. Đề xuất snapshot **v3 lưu `chapterId`, `tagIds` cho từng câu** (đi cùng v3 của Q11c) để báo cáo đúng tại thời điểm làm bài; dữ liệu cũ (v1/v2) nối theo `SourceQuestionId` với ngân hàng hiện tại và ghi rõ "theo phân loại hiện tại".

---

## Q14 — Xuất báo cáo (Phase 3) — ĐÃ CHỐT (20/11/2026): (a) Excel .xlsx

| Phương án | Mô tả | Ưu | Nhược |
|---|---|---|---|
| **(a) Excel .xlsx (Đề xuất)** | Mở rộng bộ ghi Excel có sẵn: **nhiều sheet, ô kiểu số (điểm, tỉ lệ sắp xếp/tính được), cột vượt Z**; ghi audit mỗi lần xuất (như export câu hỏi P4) | Giảng viên quen dùng, đã có hạ tầng | Phải mở rộng bộ ghi (có unit test) |
| (b) CSV UTF-8 có BOM | Một bảng một file | Đơn giản | Không nhiều sheet |
| (c) PDF | Báo cáo in sẵn | Đẹp để nộp | Cần thư viện, dàn trang |

Cột đề xuất cho (a):
- **"Xếp hạng lớp"**: STT, Hạng, Username, Họ tên, Email, Số lịch đã làm / tổng số lịch, Điểm từng lịch (thang 10, mỗi lịch một cột), Điểm tổng, Số lần đạt, Nhóm (Top/Bottom/—).
- **"Theo chương"** (bài thi): Chương, Số câu đã làm, Tỉ lệ đúng, Số học viên "Yếu".
- **"Câu hay sai"**: Mã câu, Nội dung (cắt 200 ký tự), Chương, Mức độ, Số lượt, Tỉ lệ đúng, Đáp án nhiễu chọn nhiều nhất.
- **"Thông tin"**: Lớp, Môn, thời điểm xuất (giờ Việt Nam), người xuất, phạm vi dữ liệu, cách tính điểm (Q8a/Q8b).

Quyền: giảng viên của lớp và Admin; học viên không xuất. Không đưa đáp án đúng vào báo cáo lớp.

---

## Thứ tự đề xuất sau khi chốt

1. **Phase 3 trước** (dùng dữ liệu đã có): Q8 xếp hạng lớp → Q13 phân tích học viên + báo cáo lớp → Q14 xuất Excel (kèm mở rộng bộ ghi Excel).
2. **Phase 2 sau**: snapshot v3 (chương/tag/media) + Q11/Q12 hạ tầng media → ảnh/âm thanh trong câu trắc nghiệm → Q10 điền ngắn → ghép cặp, Đúng/Sai nhiều mệnh đề, đoạn dùng chung.
