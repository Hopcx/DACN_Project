using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class ClassUser
    {
        [Key]
        public int Id { get; set; }

        public int ClassId { get; set; }

        public Guid UserId { get; set; }

        public byte? Status { get; set; }

        public virtual Class Class { get; set; } = null!;

        public virtual User User { get; set; } = null!;
    }
}
