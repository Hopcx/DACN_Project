# Endpoint To-be T1–T18 (cập nhật 25/11/2026)

Bổ sung cho `04-current-endpoints.md`. Mọi route có prefix `/web` và trả envelope `ApiResponse { success, message, data }` (xem `01-http.md`); riêng file tải về và media trả nhị phân.
- **Mã lỗi:** nằm ở `message` khi `success = false`. Đa số là mã trần (`MEDIA_IN_USE`). Một số mã đứng đầu kèm mô tả, dạng `"CODE: mô tả…"` (ví dụ `GROUP_INCOMPLETE`, `NOT_ENOUGH_QUESTIONS_FOR_GROUPS`); FE nên so theo tiền tố.
- **Quyền:** theo policy ASP.NET.
  - `Student` = level 4 hoặc Admin.
  - `Teacher` = level 3 hoặc Admin.
  - `AdminManagement` = level 1.
  - `QuestionManagement` = Admin hoặc quyền 2.
  - `ExamManagement` = Admin hoặc quyền 1.
  - `SubjectManagement`, `QuestionOrSubjectManagement`: xem `05-task2-security.md`.
- **Đối chiếu tự động:** smoke `TOBE_ENDPOINT_DOC` (trong `tests/DependencyInjectionSmoke`) kiểm rằng mọi route `[Http*]` của các controller dưới đây đều có dòng `` `METHOD /web/...` `` trong file này. Chiều ngược lại, mọi gạch đầu dòng bắt đầu bằng route cũng phải ứng với route thật. Thêm hoặc bỏ route thì sửa file này.

## Quy ước chung
- **Snapshot bài làm** (lượt thi/phiên luyện tập) chụp lúc bắt đầu, phiên bản hiện tại là **v7**:
  - v2: lời giải.
  - v3: chương/tag.
  - v4: media.
  - v5: điền ngắn.
  - v6: ghép cặp/nhiều mệnh đề.
  - v7: đoạn dùng chung.
- **Snapshot cũ vẫn đọc được.** Sửa ngân hàng câu hỏi sau khi bắt đầu không làm đổi bài làm.
- **DTO làm bài** không bao giờ chứa đáp án đúng, cờ `isCorrect`, `sourceQuestionId`, `point`, đáp án chấp nhận của câu điền ngắn hay cặp đúng của câu ghép cặp.
  - Loại 4: `options` rỗng.
  - Loại 5: `options` là vế trái; `matches` là vế phải, có Id riêng và đã xáo lúc chụp.
  - Loại 6: `options` là các mệnh đề.
  - Đáp án chỉ xuất hiện ở trang xem lại (sau khi được mở) hoặc ở kết quả "kiểm tra từng câu" của câu đã chốt.
- **Câu trả lời** có dạng `{ questionId, selectedAnswerIds[], text?, items? }`.
  - Loại 4 dùng `text` (≤ 200 ký tự).
  - Loại 5 dùng `items[{ optionId, matchId }]`; loại 6 dùng `items[{ optionId, value }]`.
  - Không gửi trường thừa; trường null không ghi vào JSON.
- **Chấm điểm:** tất cả-hoặc-không cho mọi loại (Q3, Q10b), dùng chung hàm `AnswerGrading`.
- **Media:** URL `/web/media/{id}?t=…` ký HMAC cho đúng người xem và đúng media, hết hạn sau `Media:UrlMinutes` (mặc định 10). Đây là URL bearer: ai có URL đều xem được cho tới khi hết hạn.
- **Đoạn dùng chung (T18):** câu con luôn đi cùng cả đoạn. DTO có `groups[{ id, title, content, media[] }]` và mỗi câu con có `groupId`.

## Ngân hàng câu hỏi (T1–T3, T14, T16–T18)
- `GET /web/questions` — QuestionManagement.
  - Lọc: `textSearch`, `subjectId`, `questionTypeId`, `questionLevelId`, `chapterId`, `tagIds` (lặp tham số).
  - Mỗi câu có `chapterId`, `explanation`, `tagIds`, `mediaCount`, `shortAnswer*` (loại 4), `answers[].matchContent` (loại 5), `groupId`/`groupOrder`/`groupTitle` (T18), `isUsedInExam`.
- `GET /web/questions/{id}` — QuestionManagement.
- `GET /web/questions/subjects` — QuestionManagement.
- `POST /web/questions` — QuestionManagement. Body là `QuestionCreateDto`; kiểm theo loại:
  - Loại 1: Đúng/Sai.
  - Loại 2: 1 đáp án đúng.
  - Loại 3: ≥ 1 đáp án đúng.
  - Loại 4: 1–10 đáp án chấp nhận, mỗi đáp án ≤ 200 ký tự, không trùng sau chuẩn hóa; `shortAnswerIgnoreDiacritics`, `shortAnswerNumeric`, `shortAnswerTolerance` (0..1e9, ≤ 6 chữ số thập phân, chỉ khi bật so số).
  - Loại 5: 2–10 cặp; vế trái và `matchContent` 1–2000 ký tự, không trùng.
  - Loại 6: 2–10 mệnh đề, `isCorrect` = mệnh đề đúng.
  - Sai → 400.
- `PUT /web/questions/{id}` — QuestionManagement. Thay toàn bộ câu.
  - Câu đã vào đề hoặc đã có bài làm → 409.
  - Câu con của đoạn đổi môn → 400.
- `DELETE /web/questions/{id}` — QuestionManagement. Ẩn câu (Status 255).
  - Câu đã dùng hoặc đang thuộc đoạn → 409.
- `GET /web/questions/import/template` — QuestionManagement. Trả file mẫu `.xlsx` gồm 6 cột + cột G `ShortAnswerOptions`.
- `POST /web/questions/import` — QuestionManagement. Form `file` (.xlsx ≤ 2 MB, ≤ 500 dòng).
  - Cột G dạng `nodiacritics;numeric=0.01` (chỉ loại 4).
  - Loại 5: cột Answers dạng `trái => phải; …`, `CorrectIndexes` để trống.
  - Loại 6: `CorrectIndexes` để trống được (mọi mệnh đề sai).
  - Mã lỗi theo dòng: `TYPE_ID`, `ANSWERS`, `CORRECT_INDEXES`, `SHORT_ANSWER_OPTIONS`, `DUPLICATE_FILE`, `DUPLICATE_DB`…
- `GET /web/questions/export` — QuestionManagement. `subjectId` bắt buộc; `questionTypeId` và `questionLevelId` tùy chọn.
  - Câu thuộc đoạn bị bỏ qua, có header `X-Skipped-Grouped`.
  - Mã lỗi: `NO_QUESTIONS`, `ONLY_GROUPED_QUESTIONS`, `EXPORT_TOO_LARGE`; 409 kèm `errors[]` theo từng câu.
  - Có audit `question.export`.
- `GET /web/question-types` — QuestionManagement.
- `GET /web/question-types/{id}` — QuestionManagement.
- `POST /web/question-types` — QuestionManagement.
- `PUT /web/question-types/{id}` — QuestionManagement. Loại hệ thống 1–6 → 409.
- `DELETE /web/question-types/{id}` — QuestionManagement. Loại hệ thống 1–6 → 409.
- `GET /web/chapters` — QuestionOrSubjectManagement.
- `POST /web/chapters` — SubjectManagement.
- `PUT /web/chapters/{id:int}` — SubjectManagement.
- `DELETE /web/chapters/{id:int}` — SubjectManagement.
- `GET /web/tags` — QuestionManagement.
- `POST /web/tags` — QuestionManagement. Trùng sau chuẩn hóa → 409 kèm Id của tag có sẵn.
- `PUT /web/tags/{id:int}` — AdminManagement.
- `DELETE /web/tags/{id:int}` — AdminManagement.

## Media (T14, T15)
- `POST /web/media` — QuestionManagement. Form `file`.
  - Server kiểm magic bytes. Ảnh JPEG/PNG/WebP ≤ 2 MB, ≤ 4096 × 4096 px, không nhận ảnh động. Audio MP3/M4A/OGG ≤ 10 MB.
  - File trùng nội dung (cùng SHA-256) dùng lại bản cũ (`duplicate = true`) và làm mới mốc tải lên.
  - Mã lỗi: `MEDIA_TOO_LARGE` (413), `MEDIA_EMPTY`, `MEDIA_TYPE_NOT_ALLOWED` (sai loại, ảnh động, M4A có video), `MEDIA_CORRUPT`, `MEDIA_DIMENSIONS_TOO_LARGE`.
- `GET /web/media/{id:guid}` — không cần đăng nhập, nhưng phải có `?t=` hợp lệ; token được kiểm ở mọi request, kể cả request Range.
  - Header: `nosniff`, `private, no-store`, CSP sandbox, `same-site`.
  - Mã lỗi: `MEDIA_TOKEN_REQUIRED` (401), 403 khi token sai hoặc hết hạn.
- `GET /web/questions/{questionId:int}/media` — QuestionManagement. Trả `{ locked, maxPerQuestion, maxAudioPerQuestion, items[] }`.
- `POST /web/questions/{questionId:int}/media` — QuestionManagement. Body `{ mediaId }`.
  - Tối đa 5 file, trong đó ≤ 1 audio: `MEDIA_LIMIT_REACHED`, `MEDIA_AUDIO_LIMIT_REACHED`.
  - `MEDIA_ALREADY_ATTACHED`; câu bị khóa → `QUESTION_LOCKED`.
- `DELETE /web/questions/{questionId:int}/media/{mediaId:guid}` — QuestionManagement.
- `GET /web/admin/media/orphans` — AdminManagement. Phân trang.
  - Media mồ côi là media không được câu hỏi, đoạn hay snapshot nào tham chiếu, và đã tải lên ≥ 24 giờ.
- `DELETE /web/admin/media/{id:guid}` — AdminManagement.
  - Mã lỗi: `MEDIA_IN_USE` (409), `MEDIA_TOO_RECENT` (409), `MEDIA_NOT_FOUND` (404).
  - Xóa dòng và file; có audit `media.delete`.

## Đoạn dùng chung (T18)
- `GET /web/question-groups` — QuestionManagement. Tham số `subjectId` và `includeHidden`. Mỗi đoạn có `questionCount`, `mediaCount`, `locked`.
- `GET /web/question-groups/{id:int}` — QuestionManagement. Trả câu con theo thứ tự, media (URL ký) và `locked`.
- `POST /web/question-groups` — QuestionManagement. Body `{ subjectId, title (≤ 200), content (≤ 8000), questionIds[2..10] }`.
  - Câu cùng môn, chưa xóa.
  - Mã lỗi: `QUESTION_IN_OTHER_GROUP` (409), `QUESTION_LOCKED` (409).
- `PUT /web/question-groups/{id:int}` — QuestionManagement. Thay toàn bộ thành viên theo thứ tự.
  - Đoạn có câu con đã vào đề hoặc đã có bài làm → `GROUP_LOCKED` (409).
- `PUT /web/question-groups/{id:int}/status` — QuestionManagement. Body `{ status: 0|1 }`; luôn được, kể cả khi đoạn bị khóa.
- `DELETE /web/question-groups/{id:int}` — QuestionManagement. Câu con trở thành câu lẻ; `GROUP_LOCKED`.
- `POST /web/question-groups/{id:int}/media` — QuestionManagement. Giới hạn như media của câu hỏi; `GROUP_LOCKED`.
- `DELETE /web/question-groups/{id:int}/media/{mediaId:guid}` — QuestionManagement.
- Chạy đồng thời gây deadlock SQL → 409 `CONCURRENCY_CONFLICT`.

## Bài thi và mã đề
- `GET /web/exams` — ExamManagement.
- `GET /web/exams/{id}` — ExamManagement.
- `POST /web/exams` — ExamManagement.
- `PUT /web/exams/{id}` — ExamManagement.
- `DELETE /web/exams/{id}` — ExamManagement.
- `GET /web/exams/subjects` — ExamManagement.
- `GET /web/exams/{id:int}/question-options` — ExamManagement. Có `groupId`/`groupOrder`/`groupTitle`; ẩn câu con của đoạn đang ẩn hoặc đoạn còn câu con bị ẩn.
- `GET /web/exams/{id:int}/variants` — ExamManagement.
- `GET /web/exams/{id:int}/variants/{variantId:int}` — ExamManagement.
- `POST /web/exams/{id:int}/variants` — ExamManagement. Body `{ code, status, questionIds[], randomSelections[{ questionLevelId?, count }] }`.
  - Tổng số câu phải bằng `NumberOfQuestions`.
  - Mã lỗi T18:
    - `GROUP_INCOMPLETE` (400): chọn tay thiếu câu con của đoạn.
    - `NOT_ENOUGH_QUESTIONS_FOR_GROUPS` (409): không ghép được đúng số câu từ câu lẻ và đoạn; một đoạn khớp luật mức độ chỉ khi mọi câu con cùng mức độ.
  - Câu con được xếp liền nhau. Deadlock → `CONCURRENCY_CONFLICT`.
- `PUT /web/exams/{id:int}/variants/{variantId:int}` — ExamManagement. Như trên; mã đề đã công khai hoặc đã có lượt thi → 409.

## Làm bài thi (học viên, T4, T8, T15–T18)
- `POST /web/exam-schedules/{scheduleId:int}/attempts` — Student. Bắt đầu hoặc tiếp tục lượt thi.
  - Chụp snapshot v7 và ghi `MediaReferences` (gồm media của đoạn).
  - Trả trạng thái lượt gồm `questions`, `groups`, `savedAnswers` và `revision`.
- `GET /web/attempts/mine` — Student.
- `GET /web/attempts/{id:guid}` — Student, chỉ chủ lượt. Câu hỏi, `groups` và URL media chỉ có khi đang làm.
- `PUT /web/attempts/{id:guid}/answers` — Student. Body `{ revision, answers[] }`. Sai dạng → 400; lệch revision → `CONCURRENCY_CONFLICT`.
- `POST /web/attempts/{id:guid}/submit` — Student. Idempotent.
- `GET /web/attempts/{id:guid}/review` — Student.
  - Chỉ khi lượt đã chốt, không bị hủy, `AllowViewResult` và lịch đã kết thúc. Ngược lại: `ATTEMPT_VOIDED`, `ATTEMPT_NOT_FINALIZED`, `RESULT_NOT_RELEASED`, `REVIEW_NOT_RELEASED`.
  - Mỗi câu có `selectedAnswerIds`, `isCorrect`, `options[].isCorrect`/`matchId`/`matchContent`, `text`/`acceptedAnswers` (loại 4), `items`/`correctItems`/`matches` (loại 5–6), `explanation`, `media` và `groupId`; kèm `groups`.
- `GET /web/attempts/{id:guid}/visibility-count` — Student.
- `POST /web/attempts/{id:guid}/visibility-hidden` — Student.
- `GET /web/review-flags` — Student. Đánh dấu câu khi xem lại (T8). Query `sourceKind=exam|practice&sourceId`; trả `questionIds` theo thứ tự snapshot.
- `PUT /web/review-flags` — Student. Body `{ sourceKind, sourceId, questionId }`; idempotent. Với `practice`, lấy app lock theo học viên như khi bỏ phiên.
- `DELETE /web/review-flags` — Student. Query `sourceKind&sourceId&questionId`; idempotent.
  - Mã lỗi chung: 400 khi `sourceKind`/`sourceId` sai; `REVIEW_FLAG_SOURCE_NOT_FOUND` (404) khi không phải chủ nguồn, lượt thi ngoài lớp đang hoạt động, hoặc phiên đã bị bỏ; `REVIEW_FLAG_QUESTION_INVALID` (400, chỉ PUT) khi câu không có trong snapshot.

## Luyện tập và thi thử (T5–T7, T9, T16–T18)
- `GET /web/student/practice/options` — Student. Trả các môn của lớp đã duyệt.
  - `questionCount` của môn, chương, mức độ, tag được đếm theo đơn vị (đoạn chỉ tính khi đủ câu con).
  - `hasGroups` cho biết môn có đoạn dùng chung.
- `POST /web/student/practice` — Student. Body `{ subjectId, questionCount 1–50, chapterIds?, questionLevelIds?, tagIds?, revealMode: end|perQuestion, kind: practice|mock, durationMinutes? }`.
  - Thi thử: bắt buộc `durationMinutes` 5–180, chỉ có `revealMode = end`.
  - Số câu thực tế có thể vượt yêu cầu để không cắt đoạn, tối đa 59.
  - Mã lỗi: `PRACTICE_NOT_ENOUGH_QUESTIONS` (409, kèm `available`), `QUESTION_GROUPS_CHANGED` (409), giới hạn số phiên đang mở (409).
- `GET /web/student/practice/mine` — Student.
- `GET /web/student/practice/{id:guid}` — Student, chỉ chủ phiên. Trả `questions`, `groups`, `answers` và `checked`.
- `PUT /web/student/practice/{id:guid}/answers` — Student. Câu đã chốt bị đổi → `PRACTICE_QUESTION_LOCKED`.
- `POST /web/student/practice/{id:guid}/questions/{questionId:guid}/check` — Student. Chỉ cho `perQuestion`, nếu không → `PRACTICE_CHECK_NOT_ALLOWED`.
  - Body `{ revision, selectedAnswerIds?, text?, items? }`; trống → `INVALID_SELECTION`.
  - Kết quả có `isCorrect`, `correctAnswerIds`, `acceptedAnswers`, `correctItems` và `explanation`.
- `POST /web/student/practice/{id:guid}/finish` — Student.
- `GET /web/student/practice/{id:guid}/review` — Student. Chỉ khi phiên đã nộp.
- `POST /web/student/practice/{id:guid}/retry` — Student. Chụp lại cùng các câu gốc từ ngân hàng hiện tại; giữ đoạn khi còn đủ câu con.
- `DELETE /web/student/practice/{id:guid}` — Student. Bỏ phiên đang làm và xóa `MediaReferences` của phiên.

## Lớp, xếp hạng, phân tích, báo cáo (T10–T13)
- `GET /web/student/classes` — Student.
- `POST /web/student/classes/join` — Student.
- `DELETE /web/student/classes/{classId:int}` — Student.
- `GET /web/student/classes/{classId:int}/ranking` — Student. Chỉ thấy điểm và hạng của mình; hạng chỉ hiện khi lớp có ≥ 5 học viên có điểm.
- `GET /web/student/analysis` — Student. Phân tích theo chương/tag từ outcome của chính học viên; tham số `days` 1–365.
- `GET /web/teacher/classes` — Teacher.
- `GET /web/teacher/classes/{classId:int}/members` — Teacher.
- `GET /web/teacher/classes/{classId:int}/students/lookup` — Teacher.
- `POST /web/teacher/classes/{classId:int}/members` — Teacher.
- `POST /web/teacher/classes/{classId:int}/members/{membershipId:int}/approve` — Teacher.
- `DELETE /web/teacher/classes/{classId:int}/members/{membershipId:int}` — Teacher.
- `GET /web/teacher/classes/{classId:int}/schedules` — Teacher.
- `GET /web/teacher/classes/{classId:int}/results` — Teacher.
- `GET /web/teacher/classes/{classId:int}/ranking` — Teacher.
  - Điểm quy về thang 10, đồng hạng kiểu 1-1-3, nhóm Top/Bottom 20%. Lịch không làm tính 0.
  - Giới hạn 500 học viên, 200 lịch: `CLASS_TOO_LARGE`.
- `GET /web/teacher/classes/{classId:int}/analysis` — Teacher. Báo cáo lớp:
  - Theo chương: học viên "Yếu".
  - Câu hay sai: tỉ lệ đúng < 40% với ≥ 10 lượt, kèm đáp án nhiễu hoặc câu trả lời sai phổ biến (loại 4).
- `GET /web/teacher/classes/{classId:int}/report.xlsx` — Teacher. File 4 sheet, ≤ 10 MB (`EXPORT_TOO_LARGE`); có audit `class.report.export`.
