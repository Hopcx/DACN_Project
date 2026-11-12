using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class Log
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string Description { get; set; } = null!;

        public byte? Status { get; set; }

        // P4 audit: các cột dưới đây nullable để hàng cũ giữ nguyên; chỉ server ghi, không nhận từ client.
        public Guid? ActorUserId { get; set; }

        public DateTime? CreatedAtUtc { get; set; }

        public string? Action { get; set; }

        public string? EntityType { get; set; }

        public string? EntityId { get; set; }

        public string? CorrelationId { get; set; }

        //public virtual ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    }
}
