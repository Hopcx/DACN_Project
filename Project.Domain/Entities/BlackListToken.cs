using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class BlackListToken
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(255)]
        public string Token { get; set; }
        public DateTime ExpiryDate { get; set; }
        public DateTime BlacklistAt { get; set; } = DateTime.Now;
    }
}
