# Task 02 security contract (checkout `dev/Hop`, baseline `b8d453a1`)

The older endpoint catalogue describes the prior `master` snapshot and is not the authorization contract for this checkout.

| Routes | Access |
|---|---|
| `POST /web/auth/login`, `POST /web/auth/refresh` | Explicit anonymous access; existing JSON token contract remains |
| `GET /web/auth/me`, `/WeatherForecast` | Authenticated; forecast remains a sample endpoint |
| `/web/users`, `/web/levels`, `/web/permissions`, `/web/user-permissions`, `/web/classes`, `/web/class-users`, `/web/logs`, `/web/exam-activity-logs` | Admin level claim `level_id=1` |
| `/web/exams`, `/web/exam-details`, `/web/exam-detail-questions` | `permission=1` |
| `/web/questions`, `/web/question-levels`, `/web/question-types`, `/web/answers` | `permission=2`; answers include `isCorrect` and are for question managers only |
| `/web/subjects` | `permission=3` |
| `/web/rooms`, `/web/exam-schedules`, `/web/class-exam-schedules` | `permission=4` |
| `GET /web/submissions`, `GET /web/submissions/{id}`, `GET /web/answer-submissions`, `GET /web/answer-submissions/{id}` | Admin level claim `level_id=1` |
| Submission and answer-submission POST/PUT/DELETE | Routes closed pending server-owned finalize and audited correction use cases |

All other mapped controller actions require authentication through the fallback policy. Missing authentication challenges with 401; authenticated principals without the required claim are forbidden with 403. Student data is not exposed through these administrative list/detail routes. A student result route will need an ownership check against the authenticated user before it is added.

`UserResponseDto` no longer contains `PasswordHash`. `AnswerForAttemptDto` contains only answer ID, question ID, and content; it has no student route yet. No database migration is required by this task.

Open decisions: Teacher resource scope, public registration and its default role, student eligibility/result release, and whether administrators need an audited score correction operation. Existing refresh token rotation and storage behavior remain as described in B07 and require a coordinated auth lifecycle change.
