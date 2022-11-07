using System.Drawing;

namespace VotingAPI.Models
{
    public class User
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Kostuem { get; set; }
        public string Bild { get; set; }
        public int Stimmen { get; set; }
        public int CodeId { get; set; }
    }
}
