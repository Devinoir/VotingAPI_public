using System;

namespace VotingAPI.Models
{
    public enum EventState { Registration = 0, Voting = 1, End = 2 }

    public class Event
    {
        public int EventId { get; set; }
        public DateTime RegisterOver { get; set; }
        public DateTime VoteOver { get; set; }

    }
}
