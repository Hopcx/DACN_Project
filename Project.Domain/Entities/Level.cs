using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class Level
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public byte? Status { get; set; }

        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }
}
