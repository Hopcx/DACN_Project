using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class Class
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string ClassCode { get; set; } = null!;

        public string? Description { get; set; }

        public int Capacity { get; set; }

        public Guid TeacherId { get; set; }

        public int? SubjectId { get; set; }

        public byte? Status { get; set; }

        public virtual ICollection<ClassExamSchedule> ClassExamSchedules { get; set; } = new List<ClassExamSchedule>();

        public virtual ICollection<ClassUser> ClassUsers { get; set; } = new List<ClassUser>();

        public virtual Subject? Subject { get; set; }
    }
}
