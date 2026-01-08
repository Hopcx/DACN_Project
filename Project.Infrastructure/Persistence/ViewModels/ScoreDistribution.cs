using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.ViewModels
{
    public class ScoreDistributionBy
    {
        public double Score { get; set; }

        public int AccountScore { get; set; } = 0;
    }
    public class ScoreData
    {
        public double Score { get; set; } 
        public int CountScore { get; set; } 
    }

    public class SummaryData
    {
        public double PercentPass { get; set; }
        public double PercentFail { get; set; }
    }

    public class ScoreDistribution
    {
        public List<ScoreData> Data { get; set; }
        public SummaryData Summary { get; set; }
    }
}
