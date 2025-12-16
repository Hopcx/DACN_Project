using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class QuestionLevel
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public bool? Status { get; set; }

        public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
    }
}
