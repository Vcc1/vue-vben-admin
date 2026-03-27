using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.Components
{
    public interface IHasEntityConfiguration<T> where T : class
    {
        abstract static void Configure(EntityTypeBuilder<T> entityBuilder);
    }

}
