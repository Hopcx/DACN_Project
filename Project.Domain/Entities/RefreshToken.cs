using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public string Token { get; set; } = null!;

        public DateTime ExpiryDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsRevoked { get; set; }
    }
}
