using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }

        public string FullName { get; set; } = null!;

        public string UserName { get; set; } = null!;

        public DateTime DateOfBirth { get; set; }

        public string PhoneNumber { get; set; } = null!;

        public string Address { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string PasswordHash { get; set; } = null!;

        public string? AvatarUrl { get; set; }

        public bool Sex { get; set; }

        public DateTime? LastLogin { get; set; }

        public byte Status { get; set; }

        public int LevelId { get; set; }

        public virtual ICollection<ClassUser> ClassUsers { get; set; } = new List<ClassUser>();

        public virtual ICollection<ExamActivityLog> ExamActivityLogs { get; set; } = new List<ExamActivityLog>();

        public virtual Level Level { get; set; } = null!;

        public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();

        public virtual ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    }
}
