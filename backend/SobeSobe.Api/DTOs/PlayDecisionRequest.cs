using System.ComponentModel.DataAnnotations;

namespace SobeSobe.Api.DTOs;

public class PlayDecisionRequest
{
    [Required]
    public bool WillPlay { get; set; }
}
