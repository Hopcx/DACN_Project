using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class Room
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public int Capacity { get; set; }

        public string Address { get; set; } = null!;

        public bool? Status { get; set; }

        public virtual ICollection<ExamSchedule> ExamSchedules { get; set; } = new List<ExamSchedule>();
    }
}
