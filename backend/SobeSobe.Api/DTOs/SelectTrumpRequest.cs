using System.ComponentModel.DataAnnotations;
using SobeSobe.Core.Enums;

namespace SobeSobe.Api.DTOs;

public class SelectTrumpRequest
{
    [Required]
    public TrumpSuit TrumpSuit { get; set; }

    [Required]
    public bool SelectedBeforeDealing { get; set; }
}
