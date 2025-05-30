using System.ComponentModel.DataAnnotations;

namespace FootballPortal.Models
{
    public class AppUser
    {
        [Key]
        public long ChatId { get; set; }

        public int? FavoriteTeamId { get; set; }

        public int? FavoritePlayerId { get; set; } 

        public bool NotificationsEnabled { get; set; } = true;

        public string? FirstName { get; set; }
    }


}
