using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.ViewModels
{
    public class ErrorResponse
    {
        public bool Success { get; set; }

        public string ErrorCode { get; set; }

        public string Message { get; set; }
    }
}
