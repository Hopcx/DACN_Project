using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence
{
    public class ProjectDACNDbContext : DbContext
    {
        public ProjectDACNDbContext()
        {
            
        }
        public ProjectDACNDbContext(DbContextOptions<ProjectDACNDbContext> options) : base(options)
        {
        }
        public virtual DbSet<Answer> Answers { get; set; }

        public virtual DbSet<AnswerSubmission> AnswerSubmissions { get; set; }
        

        public virtual DbSet<Class> Classes { get; set; }

        public virtual DbSet<ClassExamSchedule> ClassExamSchedules { get; set; }

        public virtual DbSet<ClassUser> ClassUsers { get; set; }

        public virtual DbSet<DoingExam> DoingExams { get; set; }

        public virtual DbSet<Exam> Exams { get; set; }

        public virtual DbSet<ExamActivityLog> ExamActivityLogs { get; set; }

        public virtual DbSet<ExamDetail> ExamDetails { get; set; }

        public virtual DbSet<ExamDetailQuestion> ExamDetailQuestions { get; set; }

        public virtual DbSet<ExamSchedule> ExamSchedules { get; set; }

        public virtual DbSet<Level> Levels { get; set; }

        public virtual DbSet<Log> Logs { get; set; }
        

        public virtual DbSet<Permission> Permissions { get; set; }

        public virtual DbSet<Question> Questions { get; set; }

        public virtual DbSet<QuestionAnswer> QuestionAnswers { get; set; }

        public virtual DbSet<QuestionLevel> QuestionLevels { get; set; }

        public virtual DbSet<QuestionType> QuestionTypes { get; set; }

        public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

        public virtual DbSet<Room> Rooms { get; set; }

        public virtual DbSet<ScoreMethod> ScoreMethods { get; set; }

        public virtual DbSet<Subject> Subjects { get; set; }

        public virtual DbSet<Submission> Submissions { get; set; }

        public virtual DbSet<User> Users { get; set; }
        
        public virtual DbSet<UserPermission> UserPermissions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=LAPTOP-CXH2410\SQLEXPRESS;Database=ProjectDACN;Trusted_Connection=True;TrustServerCertificate=True;");// dev/Hop
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Class>()
                .HasIndex(c => c.ClassCode).IsUnique();

            modelBuilder.Entity<User>()
             .HasIndex(u => u.Email).IsUnique();

            modelBuilder.Entity<User>()
           .HasIndex(u => u.PhoneNumber).IsUnique();

            modelBuilder.Entity<User>()
          .HasIndex(u => u.UserName).IsUnique();

            modelBuilder.Entity<Submission>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Submission>()
                .HasOne(s => s.ExamDetail)
                .WithMany()
                .HasForeignKey(s => s.ExamDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Submission>()
                .HasOne(s => s.ExamSchedule)
                .WithMany()
                .HasForeignKey(s => s.ExamScheduleId)
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<Answer>()
               .HasOne(s => s.Question)
               .WithMany()
               .HasForeignKey(s => s.QuestionId)
               .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<QuestionType>()
                .HasData(
                new QuestionType { Id = 1, Name = "Đúng / Sai", Description = "Chọn câu trả lời Đúng hoặc Sai", Status = true },
                new QuestionType { Id = 2, Name = "Chọn đáp án đúng nhất", Description = "Chọn đáp án đúng nhất", Status = true },
                new QuestionType { Id = 3, Name = "Chọn nhiều đáp án đúng", Description = "Chọn các đáp án đúng", Status = true }
                );

            modelBuilder.Entity<Level>()
                .HasData(
                new Level { Id = 1, Name = "Admin", Status = 1 },
                new Level { Id = 2, Name = "Examiner", Status = 1 },
                new Level { Id = 3, Name = "Teacher", Status = 1 },
                new Level { Id = 4, Name = "Student", Status = 1 }
                );

            modelBuilder.Entity<Permission>()
                .HasData(
                new Permission { Id = 1, Name = "Quản lý bài thi", Description = "Quản lý bài thi", Status = 1 },
                new Permission { Id = 2, Name = "Quản lý câu hỏi và đáp án", Description = "Quản lý câu hỏi và đáp án", Status = 1 },
                new Permission { Id = 3, Name = "Quản lý môn học", Description = "Quản lý môn học", Status = 1 },
                new Permission { Id = 4, Name = "Quản lý lịch thi", Description = "Quản lý lịch thi", Status = 1 }
                );
            modelBuilder.Entity<User>()
               .HasData(
                new User { Id = Guid.NewGuid(), Address = "A", FullName = "Nguyen Van A", UserName = "nva", DateOfBirth = DateTime.Now, PhoneNumber = "0987654321", Email = "abcde@gmail.com", PasswordHash = "4297f44b13955235245b2497399d7a93", AvatarUrl = null, LastLogin = null, Status = 1, LevelId = 4, Sex = false },
                new User { Id = Guid.NewGuid(), Address = "A", FullName = "Nguyen Van B", UserName = "nvb", DateOfBirth = DateTime.Now, PhoneNumber = "0987654322", Email = "abscde@gmail.com", PasswordHash = "4297f44b13955235245b2497399d7a93", AvatarUrl = null, LastLogin = null, Status = 1, LevelId = 3, Sex = true },
                new User { Id = Guid.NewGuid(), Address = "A", FullName = "Nguyen Van C", UserName = "nvc", DateOfBirth = DateTime.Now, PhoneNumber = "0987254322", Email = "aabscde@gmail.com", PasswordHash = "4297f44b13955235245b2497399d7a93", AvatarUrl = null, LastLogin = null, Status = 1, LevelId = 2, Sex = true },
                new User { Id = Guid.NewGuid(), Address = "A", FullName = "Nguyen Van D", UserName = "nvd", DateOfBirth = DateTime.Now, PhoneNumber = "0287654322", Email = "absscde@gmail.com", PasswordHash = "4297f44b13955235245b2497399d7a93", AvatarUrl = null, LastLogin = null, Status = 1, LevelId = 1, Sex = true }
               );

            modelBuilder.Entity<Subject>()
                .HasData(
                new Subject { Id = 1, Description = "None", Name = "Ly", Status = 1 }
                );
            /// HERE
            modelBuilder.Entity<Answer>(entity =>
            {
                entity.HasIndex(e => e.QuestionId, "IX_Answers_QuestionId");

                entity.HasOne(d => d.Question).WithMany(p => p.Answers)
                    .HasForeignKey(d => d.QuestionId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            modelBuilder.Entity<AnswerSubmission>(entity =>
            {
                entity.HasIndex(e => e.AnswerId, "IX_AnswerSubmissions_AnswerId");

                entity.HasIndex(e => e.QuestionId, "IX_AnswerSubmissions_QuestionId");

                entity.HasIndex(e => e.SubmissionId, "IX_AnswerSubmissions_SubmissionId");

                entity.HasOne(d => d.Answer).WithMany(p => p.AnswerSubmissions).HasForeignKey(d => d.AnswerId);

                entity.HasOne(d => d.Question).WithMany(p => p.AnswerSubmissions).HasForeignKey(d => d.QuestionId);

                entity.HasOne(d => d.Submission).WithMany(p => p.AnswerSubmissions).HasForeignKey(d => d.SubmissionId);
            });

            modelBuilder.Entity<Class>(entity =>
            {
                entity.HasIndex(e => e.ClassCode, "IX_Classes_ClassCode").IsUnique();

                entity.HasIndex(e => e.SubjectId, "IX_Classes_SubjectId");

                entity.HasOne(d => d.Subject).WithMany(p => p.Classes).HasForeignKey(d => d.SubjectId);
            });

            modelBuilder.Entity<ClassExamSchedule>(entity =>
            {
                entity.ToTable("ClassExamSchedule");

                entity.HasIndex(e => e.ClassId, "IX_ClassExamSchedule_ClassId");

                entity.HasIndex(e => e.ExamScheduleId, "IX_ClassExamSchedule_ExamScheduleId");

                entity.HasOne(d => d.Class).WithMany(p => p.ClassExamSchedules).HasForeignKey(d => d.ClassId);

                entity.HasOne(d => d.ExamSchedule).WithMany(p => p.ClassExamSchedules).HasForeignKey(d => d.ExamScheduleId);
            });

            modelBuilder.Entity<ClassUser>(entity =>
            {
                entity.HasIndex(e => e.ClassId, "IX_ClassUsers_ClassId");

                entity.HasIndex(e => e.UserId, "IX_ClassUsers_UserId");

                entity.HasOne(d => d.Class).WithMany(p => p.ClassUsers).HasForeignKey(d => d.ClassId);

                entity.HasOne(d => d.User).WithMany(p => p.ClassUsers).HasForeignKey(d => d.UserId);
            });

            modelBuilder.Entity<Exam>(entity =>
            {
                entity.HasIndex(e => e.ExamScheduleId, "IX_Exams_ExamScheduleId");

                entity.HasIndex(e => e.ScoreMethodId, "IX_Exams_ScoreMethodId");

                entity.HasIndex(e => e.SubjectId, "IX_Exams_SubjectId");

                entity.HasOne(d => d.ExamSchedule).WithMany(p => p.Exams).HasForeignKey(d => d.ExamScheduleId);

                entity.HasOne(d => d.ScoreMethod).WithMany(p => p.Exams).HasForeignKey(d => d.ScoreMethodId);

                entity.HasOne(d => d.Subject).WithMany(p => p.Exams).HasForeignKey(d => d.SubjectId);
            });

            modelBuilder.Entity<ExamActivityLog>(entity =>
            {
                entity.HasIndex(e => e.ExamDetailId, "IX_ExamActivityLogs_ExamDetailId");

                entity.HasIndex(e => e.ExamId, "IX_ExamActivityLogs_ExamId");

                entity.HasIndex(e => e.UserId, "IX_ExamActivityLogs_UserId");

                entity.HasOne(d => d.ExamDetail).WithMany(p => p.ExamActivityLogs).HasForeignKey(d => d.ExamDetailId);

                entity.HasOne(d => d.Exam).WithMany(p => p.ExamActivityLogs).HasForeignKey(d => d.ExamId);

                entity.HasOne(d => d.User).WithMany(p => p.ExamActivityLogs).HasForeignKey(d => d.UserId);
            });

            modelBuilder.Entity<ExamDetail>(entity =>
            {
                entity.HasIndex(e => e.ExamId, "IX_ExamDetails_ExamId");

                entity.HasOne(d => d.Exam).WithMany(p => p.ExamDetails).HasForeignKey(d => d.ExamId);
            });

            modelBuilder.Entity<ExamDetailQuestion>(entity =>
            {
                entity.HasIndex(e => e.ExamDetailId, "IX_ExamDetailQuestions_ExamDetailId");

                entity.HasIndex(e => e.QuestionId, "IX_ExamDetailQuestions_QuestionId");

                entity.HasOne(d => d.ExamDetail).WithMany(p => p.ExamDetailQuestions).HasForeignKey(d => d.ExamDetailId);

                entity.HasOne(d => d.Question).WithMany(p => p.ExamDetailQuestions).HasForeignKey(d => d.QuestionId);
            });

            modelBuilder.Entity<ExamSchedule>(entity =>
            {
                entity.HasIndex(e => e.RoomId, "IX_ExamSchedules_RoomId");

                entity.HasIndex(e => e.SubjectId, "IX_ExamSchedules_SubjectId");

                entity.HasOne(d => d.Room).WithMany(p => p.ExamSchedules).HasForeignKey(d => d.RoomId);

                entity.HasOne(d => d.Subject).WithMany(p => p.ExamSchedules).HasForeignKey(d => d.SubjectId);
            });

            modelBuilder.Entity<Question>(entity =>
            {
                entity.HasIndex(e => e.QuestionLevelId, "IX_Questions_QuestionLevelId");

                entity.HasIndex(e => e.QuestionTypeId, "IX_Questions_QuestionTypeId");

                entity.HasOne(d => d.QuestionLevel).WithMany(p => p.Questions).HasForeignKey(d => d.QuestionLevelId);

                entity.HasOne(d => d.QuestionType).WithMany(p => p.Questions).HasForeignKey(d => d.QuestionTypeId);
            });

            modelBuilder.Entity<QuestionAnswer>(entity =>
            {
                entity.HasIndex(e => e.AnswerId, "IX_QuestionAnswers_AnswerId");

                entity.HasIndex(e => e.QuestionId, "IX_QuestionAnswers_QuestionId");

                entity.HasOne(d => d.Answer).WithMany(p => p.QuestionAnswers).HasForeignKey(d => d.AnswerId);

                entity.HasOne(d => d.Question).WithMany(p => p.QuestionAnswers).HasForeignKey(d => d.QuestionId);
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.Property(e => e.Token).HasMaxLength(550);
            });

            modelBuilder.Entity<Submission>(entity =>
            {
                entity.HasIndex(e => e.ExamDetailId, "IX_Submissions_ExamDetailId");

                entity.HasIndex(e => e.ExamScheduleId, "IX_Submissions_ExamScheduleId");

                entity.HasIndex(e => e.UserId, "IX_Submissions_UserId");

                entity.HasOne(d => d.ExamDetail).WithMany(p => p.Submissions)
                    .HasForeignKey(d => d.ExamDetailId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(d => d.ExamSchedule).WithMany(p => p.Submissions)
                    .HasForeignKey(d => d.ExamScheduleId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(d => d.User).WithMany(p => p.Submissions).HasForeignKey(d => d.UserId);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Email, "IX_Users_Email").IsUnique();

                entity.HasIndex(e => e.LevelId, "IX_Users_LevelId");

                entity.HasIndex(e => e.PhoneNumber, "IX_Users_PhoneNumber").IsUnique();

                entity.HasIndex(e => e.UserName, "IX_Users_UserName").IsUnique();

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.HasOne(d => d.Level).WithMany(p => p.Users).HasForeignKey(d => d.LevelId);
            });

            modelBuilder.Entity<UserPermission>(entity =>
            {
                entity.HasIndex(e => e.PermissionId, "IX_UserPermissions_PermissionId");

                entity.HasIndex(e => e.UserId, "IX_UserPermissions_UserId");

                //entity.HasOne(d => d.Permission).WithMany(p => p.).HasForeignKey(d => d.PermissionId);

                entity.HasOne(d => d.User).WithMany(p => p.UserPermissions).HasForeignKey(d => d.UserId);
            });

            //OnModelCreatingPartial(modelBuilder);
        }

        
    }
}
