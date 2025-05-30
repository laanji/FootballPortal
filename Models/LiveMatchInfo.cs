namespace FootballPortal.Models
{
    public class LiveMatchInfo
    {
        public int Id { get; set; }
        public string Status { get; set; } = "";
        public int GoalsHome { get; set; }
        public int GoalsAway { get; set; }
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public HashSet<int> TeamIds { get; set; } = new();
    }

}
