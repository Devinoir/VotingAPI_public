using System.Collections.Generic;

namespace VotingAPI.Models
{
    public class VoteRequest
    {
        public List<int> Ids { get; set; }
        public string AuthCode { get; set; }
    }
}
