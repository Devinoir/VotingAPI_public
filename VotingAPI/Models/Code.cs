namespace VotingAPI.Models
{
    public class Code
    {
        public int CodeId { get; set; }
        public string AuthCode { get; set; }
        public bool HasVoted { get; set; }
        public int EventId { get; set; }
        public bool IsAdmin { get; set; }
    }
}
