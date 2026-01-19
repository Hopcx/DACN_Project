using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Configurations
{
    // UserPermissionConfiguration
    public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
    {
        public void Configure(EntityTypeBuilder<UserPermission> entity)
        {
            entity.HasIndex(e => e.PermissionId);
            entity.HasIndex(e => e.UserId);

            entity.HasOne(d => d.User)
                  .WithMany(p => p.UserPermissions)
                  .HasForeignKey(d => d.UserId);

            //entity.HasOne(d => d.Permission)
            //      .WithMany(p => p.UserPermissions)
            //      .HasForeignKey(d => d.PermissionId);
                entity.HasOne(d => d.Permission)
          .WithMany()
          .HasForeignKey(d => d.PermissionId);

        }
    }

}
