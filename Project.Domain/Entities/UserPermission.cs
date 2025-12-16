using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class UserPermission
    {
        [Key]
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public int PermissionId { get; set; }

        public virtual Permission Permission { get; set; } = null!;

        public virtual User User { get; set; } = null!;
    }
}
